namespace LagerthaAssistant.Infrastructure.Constants;

public static class ClaudeConstants
{
    public const string SectionName = "Claude";
    public const string BaseUrlKey = "BaseUrl";
    public const string ModelKey = "Model";
    public const string ApiKeyKey = "ApiKey";
    public const string TemperatureKey = "Temperature";
    public const string MaxTokensKey = "MaxTokens";

    public const string ApiKeyEnvironmentVariable = "ANTHROPIC_API_KEY";
    public const string MessagesEndpoint = "messages";
    public const string AnthropicVersionHeader = "anthropic-version";
    public const string AnthropicVersion = "2023-06-01";

    public const string DefaultBaseUrl = "https://api.anthropic.com/v1/";
    public const string DefaultModel = "claude-3-5-haiku-latest";
    public const double DefaultTemperature = 0.2;
    public const int DefaultMaxTokens = 1200;
    public const int HttpTimeoutSeconds = 120;
}
