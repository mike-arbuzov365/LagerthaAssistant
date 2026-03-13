namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyBatchInputService
{
    VocabularyBatchParseResult Parse(string rawInput, bool applySpaceSplitForSingleItem = false);
}
