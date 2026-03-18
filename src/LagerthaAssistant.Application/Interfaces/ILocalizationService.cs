namespace LagerthaAssistant.Application.Interfaces;

public interface ILocalizationService
{
    string Get(string key, string locale);

    string GetLocaleForUser(string? telegramLanguageCode);

    bool IsRussian(string? languageCode);
}
