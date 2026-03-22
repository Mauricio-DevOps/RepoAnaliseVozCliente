namespace POCLeituradeVozCliente.Models;

public class PocVozDoClienteViewModel
{
    public string? StatusMessage { get; set; }

    public bool IsSuccess { get; set; }

    public int TotalFeedbacksInFile { get; set; }

    public int TotalProcessed { get; set; }

    public int NextRowToProcess { get; set; } = 1;

    public bool HasMoreFeedbacks { get; set; }

    public List<FeedbackAnalysisResult> Analyses { get; set; } = new();

    public string? AudioStatusMessage { get; set; }

    public bool IsAudioSuccess { get; set; }

    public string? AudioFilePath { get; set; }

    public string? AudioTranscription { get; set; }

    public string? AudioTechnicalSummary { get; set; }

    public FeedbackAnalysisResult? AudioAnalysis { get; set; }
}
