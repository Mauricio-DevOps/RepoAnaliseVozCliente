using System.IO;

namespace POCLeituradeVozCliente.Services;

public interface IOpenAiAudioTranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName, string? contentType, CancellationToken cancellationToken);
}
