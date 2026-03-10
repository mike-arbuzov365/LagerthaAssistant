namespace LagerthaAssistant.Infrastructure.Constants;

public static class VocabularyDeckConstants
{
    public const string SectionName = "VocabularyDecks";

    public const string FolderPathKey = "FolderPath";
    public const string FilePatternKey = "FilePattern";
    public const string ReadOnlyFileNamesKey = "ReadOnlyFileNames";

    public const string NounDeckFileNameKey = "NounDeckFileName";
    public const string VerbDeckFileNameKey = "VerbDeckFileName";
    public const string IrregularVerbDeckFileNameKey = "IrregularVerbDeckFileName";
    public const string PhrasalVerbDeckFileNameKey = "PhrasalVerbDeckFileName";
    public const string AdjectiveDeckFileNameKey = "AdjectiveDeckFileName";
    public const string AdverbDeckFileNameKey = "AdverbDeckFileName";
    public const string PrepositionDeckFileNameKey = "PrepositionDeckFileName";
    public const string ConjunctionDeckFileNameKey = "ConjunctionDeckFileName";
    public const string PronounDeckFileNameKey = "PronounDeckFileName";
    public const string FallbackDeckFileNameKey = "FallbackDeckFileName";

    public static readonly string[] DefaultReadOnlyFileNames =
    [
        "wm-vocabulary-all-ru-en.xlsx",
        "wm-training-all-ru-en.xlsx",
        "wm-grammar-all-ru-en.xlsx"
    ];
}