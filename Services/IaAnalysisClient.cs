using System.Text;
using System.Text.Json;

namespace POCLeituradeVozCliente.Services;

public class IaAnalysisClient : IIaAnalysisClient
{
    private const string IaEndpoint = "https://localhost:44332/IA/v1/consulta-openai";
    private readonly IHttpClientFactory _httpClientFactory;

    public IaAnalysisClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> AnalyzeAsync(string prompt, string text, int maxTokens, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("IaClient");
        var payload = new
        {
            prompt,
            text,
            maxTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, IaEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        return ExtractText(content);
    }

    private static string ExtractText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return ExtractFromElement(document.RootElement) ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static string? ExtractFromElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in new[] { "response", "result", "content", "text", "message", "answer" })
        {
            if (element.TryGetProperty(propertyName, out var propertyValue))
            {
                var extracted = ExtractFromElement(propertyValue);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
            }
        }

        if (element.TryGetProperty("data", out var dataValue))
        {
            return ExtractFromElement(dataValue);
        }

        return element.ToString();
    }
}
