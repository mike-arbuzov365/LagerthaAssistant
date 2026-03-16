namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

public interface IWordValidationService
{
    bool IsValidWord(string word);
    IReadOnlyList<string> GetSuggestions(string word, int maxCount = 5);
}
