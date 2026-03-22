namespace POCLeituradeVozCliente.Models;

public class FeedbackProcessingSummary
{
    public int TotalFeedbacksInFile { get; set; }

    public int ProcessedInBatch { get; set; }

    public int NextRowToProcess { get; set; }

    public bool HasMoreFeedbacks { get; set; }

    public List<FeedbackAnalysisResult> Analyses { get; set; } = new();
}

public class FeedbackAnalysisResult
{
    public int RowNumber { get; set; }

    public string FeedbackText { get; set; } = string.Empty;

    public string? TranscriptionText { get; set; }

    public string MasterResponse { get; set; } = string.Empty;

    public List<DepartmentAnalysisResult> DepartmentAnalyses { get; set; } = new();
}

public class DepartmentAnalysisResult
{
    public string DepartmentName { get; set; } = string.Empty;

    public List<string> Pains { get; set; } = new();

    public string Sentiment { get; set; } = string.Empty;

    public string ActionPlan { get; set; } = string.Empty;

    public string DetailedResponse { get; set; } = string.Empty;
}
