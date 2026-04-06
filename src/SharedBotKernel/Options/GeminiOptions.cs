namespace SharedBotKernel.Options;

using SharedBotKernel.Constants;

public sealed class GeminiOptions
{
    public string BaseUrl { get; set; } = GeminiConstants.DefaultBaseUrl;

    public string Model { get; set; } = GeminiConstants.DefaultModel;

    public string? ApiKey { get; set; }

    public double Temperature { get; set; } = GeminiConstants.DefaultTemperature;

    public int MaxTokens { get; set; } = GeminiConstants.DefaultMaxTokens;
}
