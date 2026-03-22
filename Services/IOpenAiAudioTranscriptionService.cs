namespace POCLeituradeVozCliente.Services;

public interface IOpenAiAudioTranscriptionService
{
    Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken);
}
