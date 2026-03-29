namespace SharedBotKernel.Constants;

public static class OpenAiConstants
{
    public const string SectionName = "OpenAI";
    public const string BaseUrlKey = "BaseUrl";
    public const string ModelKey = "Model";
    public const string ApiKeyKey = "ApiKey";
    public const string TemperatureKey = "Temperature";

    public const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    public const string ChatCompletionsEndpoint = "chat/completions";

    public const string DefaultBaseUrl = "https://api.openai.com/v1/";
    public const string DefaultModel = "gpt-4.1-mini";
    public const double DefaultTemperature = 0.2;
    public const int HttpTimeoutSeconds = 120;
}
