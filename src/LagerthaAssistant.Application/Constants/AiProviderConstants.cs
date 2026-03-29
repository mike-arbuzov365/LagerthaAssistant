namespace LagerthaAssistant.Application.Constants;

public static class AiProviderConstants
{
    public const string OpenAi = "openai";
    public const string Claude = "claude";

    public static readonly IReadOnlyList<string> SupportedProviders =
    [
        OpenAi,
        Claude
    ];
}
