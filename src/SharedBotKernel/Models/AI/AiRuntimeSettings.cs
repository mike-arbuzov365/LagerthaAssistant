namespace SharedBotKernel.Models.AI;

public sealed record AiRuntimeSettings(
    string Provider,
    string Model,
    string ApiKey,
    AiApiKeySource ApiKeySource);
