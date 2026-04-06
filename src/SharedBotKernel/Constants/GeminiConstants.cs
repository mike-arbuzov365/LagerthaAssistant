namespace SharedBotKernel.Constants;

public static class GeminiConstants
{
    public const string SectionName = "Gemini";
    public const string BaseUrlKey = "BaseUrl";
    public const string ModelKey = "Model";
    public const string ApiKeyKey = "ApiKey";
    public const string TemperatureKey = "Temperature";
    public const string MaxTokensKey = "MaxTokens";

    public const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";
    public const string GenerateContentEndpoint = "models/{0}:generateContent";

    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    public const string DefaultModel = "gemini-2.0-flash";
    public const double DefaultTemperature = 0.2;
    public const int DefaultMaxTokens = 1200;
    public const int HttpTimeoutSeconds = 120;
}
