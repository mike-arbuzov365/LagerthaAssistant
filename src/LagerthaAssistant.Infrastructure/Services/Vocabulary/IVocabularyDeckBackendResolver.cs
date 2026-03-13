namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyDeckBackendResolver
{
    IVocabularyDeckBackend Resolve(VocabularyStorageMode mode);
}
