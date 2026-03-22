namespace POCLeituradeVozCliente.Services;

public interface IIaAnalysisClient
{
    Task<string> AnalyzeAsync(string prompt, string text, int maxTokens, CancellationToken cancellationToken);
}
