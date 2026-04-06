namespace SharedBotKernel.Constants;

public static class AiProviderConstants
{
    public const string OpenAi = "openai";
    public const string Claude = "claude";
    public const string Gemini = "gemini";

    public static readonly IReadOnlyList<string> SupportedProviders =
    [
        OpenAi,
        Claude,
        Gemini
    ];
}
