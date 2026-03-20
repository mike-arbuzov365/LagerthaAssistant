namespace LagerthaAssistant.Api.Options;

using LagerthaAssistant.Application.Constants;

public sealed class ReleaseAnnouncementOptions
{
    public bool Enabled { get; set; } = true;

    public string Version { get; set; } = "auto";

    public string NotesEn { get; set; } = string.Empty;

    public string NotesUk { get; set; } = string.Empty;

    public string NotesEs { get; set; } = string.Empty;

    public string NotesFr { get; set; } = string.Empty;

    public string NotesDe { get; set; } = string.Empty;

    public string NotesPl { get; set; } = string.Empty;

    public string ResolveNotes(string locale)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(locale);

        return normalized switch
        {
            LocalizationConstants.UkrainianLocale => string.IsNullOrWhiteSpace(NotesUk) ? NotesEn : NotesUk,
            LocalizationConstants.SpanishLocale => string.IsNullOrWhiteSpace(NotesEs) ? NotesEn : NotesEs,
            LocalizationConstants.FrenchLocale => string.IsNullOrWhiteSpace(NotesFr) ? NotesEn : NotesFr,
            LocalizationConstants.GermanLocale => string.IsNullOrWhiteSpace(NotesDe) ? NotesEn : NotesDe,
            LocalizationConstants.PolishLocale => string.IsNullOrWhiteSpace(NotesPl) ? NotesEn : NotesPl,
            _ => NotesEn
        };
    }
}
