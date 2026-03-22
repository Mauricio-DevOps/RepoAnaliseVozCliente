using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using POCLeituradeVozCliente.Models;
using POCLeituradeVozCliente.Services;

namespace POCLeituradeVozCliente.Controllers
{
    public class HomeController : Controller
    {
        private const int BatchSize = 2;
        private const string SessionNextRowKey = "PocVozDoCliente.NextRow";
        private const string SessionAnalysesKey = "PocVozDoCliente.Analyses";
        private const string SessionTotalFeedbacksKey = "PocVozDoCliente.TotalFeedbacks";
        private const int TechnicalSummaryMaxTokens = 800;
        private readonly ILogger<HomeController> _logger;
        private readonly IFeedbackAnalysisService _feedbackAnalysisService;
        private readonly IOpenAiAudioTranscriptionService _openAiAudioTranscriptionService;
        private readonly IIaAnalysisClient _iaAnalysisClient;

        public HomeController(
            ILogger<HomeController> logger,
            IFeedbackAnalysisService feedbackAnalysisService,
            IOpenAiAudioTranscriptionService openAiAudioTranscriptionService,
            IIaAnalysisClient iaAnalysisClient)
        {
            _logger = logger;
            _feedbackAnalysisService = feedbackAnalysisService;
            _openAiAudioTranscriptionService = openAiAudioTranscriptionService;
            _iaAnalysisClient = iaAnalysisClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult PocVozDoCliente()
        {
            return View(BuildViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PocVozDoCliente(CancellationToken cancellationToken)
        {
            try
            {
                var nextRow = HttpContext.Session.GetInt32(SessionNextRowKey) ?? 1;
                var accumulatedAnalyses = GetSessionAnalyses();
                var summary = await _feedbackAnalysisService.ProcessAsync(nextRow, BatchSize, cancellationToken);

                accumulatedAnalyses.AddRange(summary.Analyses);
                SaveSessionAnalyses(accumulatedAnalyses);
                HttpContext.Session.SetInt32(SessionNextRowKey, summary.NextRowToProcess);
                HttpContext.Session.SetInt32(SessionTotalFeedbacksKey, summary.TotalFeedbacksInFile);

                return View(new PocVozDoClienteViewModel
                {
                    IsSuccess = true,
                    StatusMessage = summary.ProcessedInBatch > 0
                        ? $"{summary.ProcessedInBatch} feedback(s) processado(s) nesta execucao."
                        : "Nao ha mais feedbacks para processar.",
                    TotalFeedbacksInFile = summary.TotalFeedbacksInFile,
                    TotalProcessed = accumulatedAnalyses.Count,
                    NextRowToProcess = summary.NextRowToProcess,
                    HasMoreFeedbacks = summary.HasMoreFeedbacks,
                    Analyses = accumulatedAnalyses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a POC Voz do Cliente.");

                return View(new PocVozDoClienteViewModel
                {
                    IsSuccess = false,
                    StatusMessage = $"Nao foi possivel processar os feedbacks: {ex.Message}",
                    Analyses = GetSessionAnalyses(),
                    NextRowToProcess = HttpContext.Session.GetInt32(SessionNextRowKey) ?? 1,
                    HasMoreFeedbacks = !HasProcessingFinished()
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessarAudio(IFormFile? audioFile, CancellationToken cancellationToken)
        {
            if (audioFile is null || audioFile.Length == 0)
            {
                var emptyFileModel = BuildViewModel();
                emptyFileModel.IsAudioSuccess = false;
                emptyFileModel.AudioStatusMessage = "Grave um audio antes de enviar para transcricao.";
                return View("PocVozDoCliente", emptyFileModel);
            }

            var fileExtension = Path.GetExtension(audioFile.FileName);
            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                fileExtension = ".webm";
            }

            var fileName = string.IsNullOrWhiteSpace(audioFile.FileName)
                ? $"gravacao-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{fileExtension}"
                : Path.GetFileName(audioFile.FileName);

            try
            {
                await using var tempStream = new MemoryStream();
                await audioFile.CopyToAsync(tempStream, cancellationToken);
                tempStream.Position = 0;

                var transcription = await _openAiAudioTranscriptionService.TranscribeAsync(
                    tempStream,
                    fileName,
                    audioFile.ContentType,
                    cancellationToken);
                var technicalSummary = await _iaAnalysisClient.AnalyzeAsync(
                    AudioTechnicalSummaryPrompt,
                    transcription,
                    TechnicalSummaryMaxTokens,
                    cancellationToken);
                var analysis = await _feedbackAnalysisService.AnalyzeFeedbackAsync(transcription, 0, cancellationToken);
                analysis.TranscriptionText = transcription;

                var model = BuildViewModel();
                model.IsAudioSuccess = true;
                model.AudioStatusMessage = "Audio transcrito e analisado com sucesso.";
                model.AudioTranscription = transcription;
                model.AudioTechnicalSummary = technicalSummary;
                model.AudioAnalysis = analysis;

                return View("PocVozDoCliente", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar audio na POC Voz do Cliente.");

                var errorModel = BuildViewModel();
                errorModel.IsAudioSuccess = false;
                errorModel.AudioStatusMessage = $"Nao foi possivel processar o audio: {ex.Message}";
                return View("PocVozDoCliente", errorModel);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private PocVozDoClienteViewModel BuildViewModel()
        {
            var analyses = GetSessionAnalyses();

            return new PocVozDoClienteViewModel
            {
                Analyses = analyses,
                TotalProcessed = analyses.Count,
                NextRowToProcess = HttpContext.Session.GetInt32(SessionNextRowKey) ?? 1,
                TotalFeedbacksInFile = HttpContext.Session.GetInt32(SessionTotalFeedbacksKey) ?? 0,
                HasMoreFeedbacks = !HasProcessingFinished()
            };
        }

        private List<FeedbackAnalysisResult> GetSessionAnalyses()
        {
            var json = HttpContext.Session.GetString(SessionAnalysesKey);
            return string.IsNullOrWhiteSpace(json)
                ? new List<FeedbackAnalysisResult>()
                : JsonSerializer.Deserialize<List<FeedbackAnalysisResult>>(json) ?? new List<FeedbackAnalysisResult>();
        }

        private void SaveSessionAnalyses(List<FeedbackAnalysisResult> analyses)
        {
            HttpContext.Session.SetString(SessionAnalysesKey, JsonSerializer.Serialize(analyses));
        }

        private bool HasProcessingFinished()
        {
            var totalFeedbacks = HttpContext.Session.GetInt32(SessionTotalFeedbacksKey);
            var nextRow = HttpContext.Session.GetInt32(SessionNextRowKey) ?? 1;

            return totalFeedbacks.HasValue && nextRow > totalFeedbacks.Value;
        }

        private const string AudioTechnicalSummaryPrompt = """
Voce e um analista corporativo especialista em documentacao executiva.
Recebera uma transcricao de audio em portugues e deve gerar um resumo tecnico profissional, claro e objetivo.

REGRAS
- Use linguagem corporativa e tecnica.
- Organize os pontos principais do que foi tratado.
- Nao invente informacoes que nao estejam na transcricao.
- Destaque contexto, problema principal, impactos percebidos e encaminhamentos mencionados, quando existirem.
- Seja conciso, mas suficientemente completo para leitura gerencial.

FORMATO
Resumo Tecnico Profissional:
<resumo em 1 a 3 paragrafos curtos>
""";
    }
}
