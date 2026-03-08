namespace LagerthaAssistant.Application.Interfaces.Memory;

using LagerthaAssistant.Application.Models.Memory;

public interface IConversationMemoryExtractor
{
    IReadOnlyCollection<MemoryFactCandidate> ExtractFromUserMessage(string userMessage);
}

