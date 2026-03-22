namespace POCLeituradeVozCliente.Models;

public class OpenAiAudioOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-transcribe";

    public string Language { get; set; } = "pt";
}
