namespace SharedBotKernel.Options;

using SharedBotKernel.Constants;

public sealed class ClaudeOptions
{
    public string BaseUrl { get; set; } = ClaudeConstants.DefaultBaseUrl;

    public string Model { get; set; } = ClaudeConstants.DefaultModel;

    public string? ApiKey { get; set; }

    public double Temperature { get; set; } = ClaudeConstants.DefaultTemperature;

    public int MaxTokens { get; set; } = ClaudeConstants.DefaultMaxTokens;
}
