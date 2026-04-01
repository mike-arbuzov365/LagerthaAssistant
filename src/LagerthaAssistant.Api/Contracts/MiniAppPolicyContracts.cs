namespace LagerthaAssistant.Api.Contracts;

public sealed record MiniAppPolicyResponse(
    string DefaultLocale,
    IReadOnlyList<string> SupportedLocales,
    string StorageModePolicy,
    IReadOnlyList<string> AllowedStorageModes,
    string OneDriveAuthScope,
    bool RequiresInitDataVerification,
    IReadOnlyList<string> Notes);
