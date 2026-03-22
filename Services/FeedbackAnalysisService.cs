using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using POCLeituradeVozCliente.Models;

namespace POCLeituradeVozCliente.Services;

public class FeedbackAnalysisService : IFeedbackAnalysisService
{
    private const string ExcelFilePath = @"C:\Users\mnmarques\source\repos\POCLeituradeVozCliente\POCLeituradeVozCliente\wwwroot\Arquivo\ExemploFeadback.xlsx";
    private const int MasterPromptMaxTokens = 2000;
    private const int DetailPromptMaxTokens = 1200;

    private static readonly string[] OfficialDepartments =
    {
        "AtendimentoTecnico_Implantacao",
        "Atendimentotecnico_Laboratorio",
        "Atendimentotecnico_Solucoes",
        "Atendimento_Tecnico",
        "BKC_Cadastro",
        "BKC_Comercial",
        "BKC_Contratos",
        "BKS_Orcamentos",
        "BKS_Pecas",
        "BKS_TrocaTecnica",
        "CS",
        "Portal UX",
        "Cobranca",
        "Comercial",
        "Consumivel",
        "Contadores",
        "DPO",
        "DRA_Retirada",
        "Faturamento",
        "GestaoOI_OD",
        "ServiceDesk",
        "Transportes",
        "NOC",
        "Planejamento",
        "Fiscal",
        "Juridico"
    };

    private readonly IExcelFeedbackReader _excelFeedbackReader;
    private readonly IIaAnalysisClient _iaAnalysisClient;

    public FeedbackAnalysisService(IExcelFeedbackReader excelFeedbackReader, IIaAnalysisClient iaAnalysisClient)
    {
        _excelFeedbackReader = excelFeedbackReader;
        _iaAnalysisClient = iaAnalysisClient;
    }

    public async Task<FeedbackProcessingSummary> ProcessAsync(int startRow, int batchSize, CancellationToken cancellationToken)
    {
        var feedbacks = _excelFeedbackReader.ReadFirstColumnValues(ExcelFilePath);
        var safeStartRow = Math.Max(1, startRow);
        var batchFeedbacks = feedbacks
            .Skip(safeStartRow - 1)
            .Take(batchSize)
            .ToList();

        var summary = new FeedbackProcessingSummary
        {
            TotalFeedbacksInFile = feedbacks.Count,
            ProcessedInBatch = batchFeedbacks.Count
        };

        for (var index = 0; index < batchFeedbacks.Count; index++)
        {
            summary.Analyses.Add(await AnalyzeFeedbackAsync(batchFeedbacks[index], safeStartRow + index, cancellationToken));
        }

        summary.NextRowToProcess = safeStartRow + batchFeedbacks.Count;
        summary.HasMoreFeedbacks = summary.NextRowToProcess <= feedbacks.Count;

        return summary;
    }

    public async Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(string feedbackText, int rowNumber, CancellationToken cancellationToken)
    {
        var masterResponse = await _iaAnalysisClient.AnalyzeAsync(MasterPrompt, feedbackText, MasterPromptMaxTokens, cancellationToken);
        var departments = ExtractDepartments(masterResponse);

        var analysis = new FeedbackAnalysisResult
        {
            RowNumber = rowNumber,
            FeedbackText = feedbackText,
            MasterResponse = masterResponse
        };

        foreach (var department in departments)
        {
            var detailPrompt = BuildDetailPromptByDepartment(department);
            var detailedResponse = await _iaAnalysisClient.AnalyzeAsync(detailPrompt, feedbackText, DetailPromptMaxTokens, cancellationToken);

            analysis.DepartmentAnalyses.Add(new DepartmentAnalysisResult
            {
                DepartmentName = department,
                Pains = ExtractPains(detailedResponse),
                Sentiment = ExtractSingleLineValue(detailedResponse, "Analise do Sentimento do Cliente")
                    ?? ExtractSingleLineValue(detailedResponse, "Sentimento")
                    ?? "Nao identificado",
                ActionPlan = ExtractMultilineValue(detailedResponse, "Sugestao de Plano de Acao")
                    ?? ExtractMultilineValue(detailedResponse, "Plano de Acao")
                    ?? "Nao identificado",
                DetailedResponse = detailedResponse
            });
        }

        return analysis;
    }

    private static List<string> ExtractDepartments(string masterResponse)
    {
        var matches = Regex.Matches(
            masterResponse,
            @"Departamento:\s*(.+)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        return matches
            .Select(match => match.Groups[1].Value.Trim())
            .Select(TryResolveDepartmentName)
            .Where(department => !string.IsNullOrWhiteSpace(department))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private static string BuildDetailPromptByDepartment(string department)
    {
        return NormalizeDepartmentKey(department) switch
        {
            "atendimentotecnicoimplantacao" => AtendimentoTecnicoImplantacaoPrompt,
            "atendimentotecnicolaboratorio" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a celular, tablet, notebook, defeito, configuracao e retorno do laboratorio."),
            "atendimentotecnicosolucoes" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a suporte tecnico de solucoes, status de chamado, configuracao pendente e comunicacao do suporte."),
            "atendimentotecnico" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a manutencao, visita tecnica, OS, SLA, backup, peca e recorrencia tecnica."),
            "bkccadastro" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a alteracao cadastral, endereco, CNPJ, razao social, inscricao estadual ou dados do cadastro."),
            "bkccomercial" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a multa de retirada, envio de equipamento comercializado, toner inicial ou venda mercantil."),
            "bkccontratos" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a SLA contratual, contrato vencido ou ajuste de valores contratuais."),
            "bksorcamentos" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a posicao de orcamento, atraso no envio, valor acima do mercado ou documento tecnico pendente."),
            "bkspecas" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a peca pendente, falta de estoque, auditoria, separacao, coleta, internacao ou envio de peca."),
            "bkstrocatecnica" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a troca tecnica, falta de equipamento para troca, atraso ou falta de retorno da troca."),
            "cs" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a Customer Success, retorno do analista, treinamento, portal ou plataforma."),
            "portalux" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a usabilidade, lentidao, confusao, pedidos, chamados ou autenticacao no portal."),
            "cobranca" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a boleto, juros indevidos, vencimento, falta de retorno, protesto ou negativacao."),
            "comercial" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a renovacao, proposta, upgrade, aditivo, movimentacao, transferencia e relacionamento comercial."),
            "consumivel" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a toner, resma, consumivel, bloqueio de pedido, auditoria, liberacao, reposicao ou envio emergencial."),
            "contadores" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a divergencia de contador, base de cobranca, desbloqueio por contador ou envio indevido de contadores."),
            "dpo" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a projeto, cronograma, entrega em projeto e comunicacao do projeto."),
            "draretirada" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a retirada, coleta, atraso na coleta, falta de posicao ou negociacao de retirada."),
            "faturamento" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a faturamento, cobranca faturada incorretamente, desconto, relatorio ou retorno do time."),
            "gestaooiod" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a ativacao, finalizacao, inibicao de OI ou OD, item ativo indevido e divergencia de instalacao."),
            "servicedesk" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a chat, WhatsApp, telefone, atendimento inicial, comunicacao e acesso ao Simpress UX."),
            "transportes" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a entrega, rastreio, reentrega, transito, comprovante, previsao de chegada e priorizacao logistica."),
            "noc" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a monitoramento, perda de monitoramento e comunicacao relacionada a monitoramento."),
            "planejamento" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a falta de peca, toner, equipamento ou indisponibilidade por planejamento."),
            "fiscal" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a XML, nota fiscal, carta de correcao, rejeicao, cancelamento ou retencao fiscal."),
            "juridico" => BuildDepartmentPrompt(department, "Considere apenas sinais ligados a contrato juridico, aditivo, renovacao, morosidade e comunicacao do juridico."),
            _ => BuildDepartmentPrompt(department, "Considere apenas o escopo exato desse departamento.")
        };
    }

    private static string? TryResolveDepartmentName(string department)
    {
        var normalizedDepartment = NormalizeDepartmentKey(department);

        return OfficialDepartments.FirstOrDefault(officialDepartment =>
            NormalizeDepartmentKey(officialDepartment) == normalizedDepartment);
    }

    private static string NormalizeDepartmentKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string BuildDepartmentPrompt(string department, string departmentFocus)
    {
        return $"""
ANALISE APENAS O DEPARTAMENTO "{department}".

{departmentFocus}

TAREFA
Com base somente no feedback recebido, retorne:
- Dores identificadas para esse departamento
- Analise do sentimento do cliente
- Sugestao objetiva de plano de acao

REGRAS
- Nao invente fatos.
- Use apenas evidencia textual do feedback.
- Se nao houver evidencia suficiente, informe isso claramente.
- Seja objetivo e curto.

FORMATO OBRIGATORIO
Departamento Ofensor Principal: {department}
Dores Identificadas:
- <listar dores encontradas>
Analise do Sentimento do Cliente: <1 linha>
Sugestao de Plano de Acao: <1 a 3 linhas>
""";
    }

    private const string AtendimentoTecnicoImplantacaoPrompt = """
Voce e um analista especialista em Customer Experience (CX), responsavel por analisar feedbacks de clientes e identificar o departamento ofensor principal, com foco especifico em:
AtendimentoTecnico_Implantacao

Sua tarefa e analisar o feedback e identificar problemas relacionados especificamente ao processo de implantacao, instalacao, desinstalacao, execucao e gestao de Ordens de Instalacao (OI).

INSTRUCOES CRITICAS
Voce deve:
Ler todo o feedback cuidadosamente
Identificar o departamento ofensor principal
Priorizar obrigatoriamente o departamento:
AtendimentoTecnico_Implantacao
mesmo que outros departamentos sejam mencionados.
Identificar exclusivamente as dores relacionadas a implantacao e atendimento tecnico.

Ignore problemas relacionados a:
faturamento
comercial
financeiro
contrato
transportadora (exceto quando impactar diretamente instalacao)
relacionamento geral

Considere apenas problemas que impactem diretamente:
implantacao
instalacao
execucao tecnica
finalizacao da instalacao
organizacao tecnica
comunicacao tecnica relacionada a implantacao

DORES VALIDAS PARA CLASSIFICACAO
Voce deve identificar apenas as seguintes dores:
Priorizacao de OI - Ordem de instalacao
Posicao de OI - Ordem de instalacao
Finalizacao OI - Ordem de instalacao
Instalacao
Desinstalacao
Morosidade para atendimento
Falta de comunicacao

REGRAS DE INTERPRETACAO SEMANTICA
Considere como equivalentes semanticos:

Instalacao inclui:
atraso na instalacao
instalacao incompleta
instalacao nao realizada
instalacao parcial
equipamentos nao instalados
tecnicos nao instalaram
implantacao desorganizada

Morosidade inclui:
demora
lentidao
atraso
prazo nao cumprido
espera excessiva
processo demorado

Falta de comunicacao inclui:
ausencia de atualizacao
falta de retorno
falta de posicionamento
cliente nao recebeu informacao
comunicacao ineficiente
cliente teve que insistir por respostas

Finalizacao OI inclui:
implantacao incompleta
instalacao nao finalizada
equipamentos pendentes
ordem nao concluida

FORMATO DE SAIDA (OBRIGATORIO)
Responda EXCLUSIVAMENTE neste formato:

Departamento Ofensor Principal: AtendimentoTecnico_Implantacao
Dores Identificadas:
- [listar apenas dores validas encontradas]
Analise do Sentimento do Cliente: [1 linha objetiva]
Sugestao de Plano de Acao: [1 a 3 linhas objetivas]
Resumo Executivo:
[resumo objetivo em ate 3 linhas explicando o problema relacionado a implantacao]
Severidade:
Alta | Media | Baixa
Justificativa da Severidade:
[explicar baseado no impacto operacional]
""";

    private const string MasterPrompt = """
Voce e um analista de Customer Experience (CX).
Analise o feedback e identifique apenas os departamentos ofensores com base em evidencia textual clara.

REGRAS
- Nao invente informacoes.
- Nao use conhecimento externo.
- Nao classifique por achismo ou contexto fraco.
- Se houver mais de um departamento, liste todos.
- Se nao houver departamento claro, responda "Nenhum departamento classificado".
- Nao retorne dores nesta etapa.
- Use somente estes nomes de departamento:
AtendimentoTecnico_Implantacao, Atendimentotecnico_Laboratorio, Atendimentotecnico_Solucoes, Atendimento_Tecnico, BKC_Cadastro, BKC_Comercial, BKC_Contratos, BKS_Orcamentos, BKS_Pecas, BKS_TrocaTecnica, CS, Portal UX, Cobranca, Comercial, Consumivel, Contadores, DPO, DRA_Retirada, Faturamento, GestaoOI_OD, ServiceDesk, Transportes, NOC, Planejamento, Fiscal, Juridico.

SEVERIDADE
- Alta: impacto forte, longa demora, sem solucao, risco operacional ou protesto/negativacao.
- Media: atraso moderado, retrabalho ou impacto relevante sem criticidade maxima.
- Baixa: problema pontual, baixo impacto ou ja resolvido.

FORMATO OBRIGATORIO
Departamentos Ofensores Identificados:
1) Departamento: <nome exato>
   Severidade: Alta | Media | Baixa
   Justificativa: <1-2 linhas com base no feedback>
   Resumo Executivo: <ate 3 linhas>

Severidade Geral do Feedback: Alta | Media | Baixa
Motivo da Severidade Geral: <1 linha>
""";

    private static List<string> ExtractPains(string detailedResponse)
    {
        var painsMatch = Regex.Match(
            detailedResponse,
            @"Dores Identificadas:\s*(?<content>[\s\S]*?)(?:Analise do Sentimento do Cliente:|Sentimento:|Sugestao de Plano de Acao:|Plano de Acao:|Resumo Executivo:|Severidade:|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!painsMatch.Success)
        {
            var singlePain = ExtractSingleLineValue(detailedResponse, "Dor");
            return string.IsNullOrWhiteSpace(singlePain)
                ? new List<string>()
                : new List<string> { singlePain };
        }

        return painsMatch.Groups["content"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimStart('-', '•').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static string? ExtractSingleLineValue(string content, string label)
    {
        var match = Regex.Match(
            content,
            $@"{Regex.Escape(label)}:\s*(.+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractMultilineValue(string content, string label)
    {
        var match = Regex.Match(
            content,
            $@"{Regex.Escape(label)}:\s*(?<value>[\s\S]*?)(?:Resumo Executivo:|Severidade:|Justificativa da Severidade:|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
