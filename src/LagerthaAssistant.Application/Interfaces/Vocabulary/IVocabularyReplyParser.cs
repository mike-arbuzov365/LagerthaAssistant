namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyReplyParser
{
    bool TryParse(string assistantReply, out ParsedVocabularyReply? parsedReply);
}
