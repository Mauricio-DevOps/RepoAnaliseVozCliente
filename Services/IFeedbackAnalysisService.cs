using POCLeituradeVozCliente.Models;

namespace POCLeituradeVozCliente.Services;

public interface IFeedbackAnalysisService
{
    Task<FeedbackProcessingSummary> ProcessAsync(int startRow, int batchSize, CancellationToken cancellationToken);

    Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(string feedbackText, int rowNumber, CancellationToken cancellationToken);
}
