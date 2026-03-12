namespace LagerthaAssistant.Infrastructure.Options;

public sealed class VocabularyDeckOptions
{
    public string FolderPath { get; init; } = "%OneDrive%\\Apps\\Flashcards Deluxe";

    public string FilePattern { get; init; } = "wm-*.xlsx";

    public IReadOnlyList<string> ReadOnlyFileNames { get; init; } = [];

    public string NounDeckFileName { get; init; } = "wm-nouns-ua-en.xlsx";
    public string VerbDeckFileName { get; init; } = "wm-verbs-us-en.xlsx";
    public string IrregularVerbDeckFileName { get; init; } = "wm-irregular-verbs-ua-en.xlsx";
    public string PhrasalVerbDeckFileName { get; init; } = "wm-phrasal-verbs-ua-en.xlsx";
    public string AdjectiveDeckFileName { get; init; } = "wm-adjectives-ua-en.xlsx";
    public string AdverbDeckFileName { get; init; } = "wm-adverbs-ua-en.xlsx";
    public string PrepositionDeckFileName { get; init; } = "wm-prepositions-ua-en.xlsx";
    public string ConjunctionDeckFileName { get; init; } = "wm-conjunctions-ua-en.xlsx";
    public string PronounDeckFileName { get; init; } = "wm-pronouns-ua-en.xlsx";
    public string PersistentExpressionDeckFileName { get; init; } = "wm-persistant-expressions-ua-en.xlsx";
    public string FallbackDeckFileName { get; init; } = "wm-vocabulary-1-grade-ua-en.xlsx";
}