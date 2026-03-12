namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;

public sealed class VocabularyConversationAgent : IConversationAgent
{
    private readonly IVocabularyWorkflowService _workflowService;

    public VocabularyConversationAgent(IVocabularyWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public string Name => "vocabulary-agent";

    public int Order => 100;

    public bool CanHandle(ConversationAgentContext context)
        => true;

    public async Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsBatch)
        {
            var batch = await _workflowService.ProcessBatchAsync(context.BatchItems, cancellationToken);
            var items = batch
                .Select(MapFromWorkflow)
                .ToList();

            return new ConversationAgentResult(Name, "vocabulary.batch", true, items);
        }

        var single = await _workflowService.ProcessAsync(context.Input, cancellationToken: cancellationToken);
        return new ConversationAgentResult(Name, "vocabulary.single", false, [MapFromWorkflow(single)]);
    }

    private static ConversationAgentItemResult MapFromWorkflow(Application.Models.Vocabulary.VocabularyWorkflowItemResult item)
    {
        return new ConversationAgentItemResult(
            item.Input,
            item.Lookup,
            item.AssistantCompletion,
            item.AppendPreview);
    }
}
