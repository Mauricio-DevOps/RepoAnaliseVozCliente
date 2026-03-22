using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using POCLeituradeVozCliente.Models;

namespace POCLeituradeVozCliente.Services;

public class OpenAiAudioTranscriptionService : IOpenAiAudioTranscriptionService
{
    private const string AudioTranscriptionEndpoint = "https://api.openai.com/v1/audio/transcriptions";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiAudioOptions _options;

    public OpenAiAudioTranscriptionService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiAudioOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Configure a chave da OpenAI em OpenAiAudio:ApiKey.");
        }

        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("Arquivo de audio nao encontrado.", audioFilePath);
        }

        var client = _httpClientFactory.CreateClient("OpenAiAudioClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        await using var audioStream = File.OpenRead(audioFilePath);
        using var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");

        using var formData = new MultipartFormDataContent
        {
            { new StringContent(_options.Model), "model" },
            { new StringContent(_options.Language), "language" },
            { audioContent, "file", Path.GetFileName(audioFilePath) }
        };

        using var response = await client.PostAsync(AudioTranscriptionEndpoint, formData, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(content);
        if (document.RootElement.TryGetProperty("text", out var text))
        {
            return text.GetString() ?? string.Empty;
        }

        return content;
    }
}
