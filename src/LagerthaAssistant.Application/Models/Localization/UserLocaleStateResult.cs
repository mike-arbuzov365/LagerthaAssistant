namespace LagerthaAssistant.Application.Models.Localization;

public sealed record UserLocaleStateResult(
    string Locale,
    bool IsInitialized,
    bool IsSwitched);
