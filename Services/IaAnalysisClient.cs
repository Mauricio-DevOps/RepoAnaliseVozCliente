using System.Text;
using System.Text.Json;

namespace POCLeituradeVozCliente.Services;

public class IaAnalysisClient : IIaAnalysisClient
{
    private const string IaEndpoint = "https://apiiadev-gjenhsf0cpbvg3hm.canadacentral-01.azurewebsites.net/api/IA/chat";
    private const string DefaultModel = "gpt-4o-mini";
    private const double DefaultTemperature = 0.8;
    private readonly IHttpClientFactory _httpClientFactory;

    public IaAnalysisClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> AnalyzeAsync(string prompt, string text, int maxTokens, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("IaClient");
        var systemPromptBuilder = new StringBuilder(prompt ?? string.Empty);

        if (maxTokens > 0)
        {
            if (systemPromptBuilder.Length > 0)
            {
                systemPromptBuilder.AppendLine().AppendLine();
            }

            systemPromptBuilder.Append($"Limite sua resposta a aproximadamente {maxTokens} tokens equivalentes.");
        }

        var payload = new
        {
            prompt = text ?? string.Empty,
            systemPrompt = systemPromptBuilder.ToString(),
            model = DefaultModel,
            temperature = DefaultTemperature
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, IaEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(content);
        if (TryExtractChatMessage(document.RootElement, out var message))
        {
            return message;
        }

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

    private static bool TryExtractChatMessage(JsonElement rootElement, out string result)
    {
        result = string.Empty;

        if (rootElement.TryGetProperty("choices", out var choicesElement) &&
            choicesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choicesElement.EnumerateArray())
            {
                if (choice.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (choice.TryGetProperty("message", out var messageElement) &&
                    messageElement.ValueKind == JsonValueKind.Object &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    var value = contentElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result = value!;
                        return true;
                    }
                }

                if (choice.TryGetProperty("text", out var textElement))
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result = value!;
                        return true;
                    }
                }
            }
        }

        if (rootElement.TryGetProperty("message", out var singleMessageElement) &&
            singleMessageElement.ValueKind == JsonValueKind.Object &&
            singleMessageElement.TryGetProperty("content", out var singleContentElement))
        {
            var value = singleContentElement.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result = value!;
                return true;
            }
        }

        return false;
    }
}
