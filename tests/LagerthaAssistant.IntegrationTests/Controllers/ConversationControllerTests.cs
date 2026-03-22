namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ConversationControllerTests
{
    [Fact]
    public void GetCommands_ShouldReturnSlashCommandCatalog()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = sut.GetCommands();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationCommandItemResponse>>(ok.Value);

        Assert.NotEmpty(payload);
        Assert.All(payload, item => Assert.False(string.IsNullOrWhiteSpace(item.Category)));
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.Help && item.Category == ConversationCommandCategories.General);
        Assert.Contains(payload, item => item.Command == $"{ConversationSlashCommands.PromptSet} <text>" && item.Category == ConversationCommandCategories.SystemPrompt);
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.Index && item.Category == ConversationCommandCategories.VocabularyIndex);
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.IndexClear && item.Category == ConversationCommandCategories.VocabularyIndex);
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.IndexRebuild && item.Category == ConversationCommandCategories.VocabularyIndex);
        Assert.Contains(payload, item => item.Command == $"{ConversationSlashCommands.SyncRun} <n>" && item.Category == ConversationCommandCategories.SyncQueue);
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.SyncFailed && item.Category == ConversationCommandCategories.SyncQueue);
        Assert.Contains(payload, item => item.Command == $"{ConversationSlashCommands.SyncRetryFailed} <n>" && item.Category == ConversationCommandCategories.SyncQueue);
    }

    [Fact]
    public void GetGroupedCommands_ShouldReturnGroupedCatalog()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = sut.GetGroupedCommands();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationCommandGroupResponse>>(ok.Value);

        Assert.NotEmpty(payload);

        var generalGroup = Assert.Single(payload, group => group.Category == ConversationCommandCategories.General);
        Assert.Contains(generalGroup.Commands, item => item.Command == ConversationSlashCommands.Help);

        var syncGroup = Assert.Single(payload, group => group.Category == ConversationCommandCategories.SyncQueue);
        Assert.Contains(syncGroup.Commands, item => item.Command == ConversationSlashCommands.SyncRun);
        Assert.Contains(syncGroup.Commands, item => item.Command == ConversationSlashCommands.SyncFailed);

        var indexGroup = Assert.Single(payload, group => group.Category == ConversationCommandCategories.VocabularyIndex);
        Assert.Contains(indexGroup.Commands, item => item.Command == ConversationSlashCommands.Index);
        Assert.Contains(indexGroup.Commands, item => item.Command == ConversationSlashCommands.IndexClear);
        Assert.Contains(indexGroup.Commands, item => item.Command == ConversationSlashCommands.IndexRebuild);
    }

    [Fact]
    public async Task GetHistory_ShouldSetScopeAndReturnMappedHistory()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            History =
            [
                ConversationMessage.Create(MessageRole.User, "hello", DateTimeOffset.UtcNow)
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.GetHistory(10, "  TeLeGrAm  ", "Mike", "chat-42", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationHistoryEntryResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal("user", entry.Role);
        Assert.Equal("hello", entry.Content);

        Assert.Equal("telegram", scopeAccessor.Current.Channel);
        Assert.Equal("mike", scopeAccessor.Current.UserId);
        Assert.Equal("chat-42", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task GetMemory_ShouldUseDefaults_WhenScopeNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            Memory =
            [
                new UserMemoryEntry
                {
                    Key = MemoryKeys.UserName,
                    Value = "Mike",
                    Confidence = 0.95,
                    IsActive = true,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.GetMemory(5, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationMemoryEntryResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal(MemoryKeys.UserName, entry.Key);
        Assert.Equal("Mike", entry.Value);

        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("anonymous", scopeAccessor.Current.UserId);
        Assert.Equal("default", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task GetPrompt_ShouldReturnActivePrompt()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            SystemPrompt = "You are a concise assistant."
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.GetPrompt(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptResponse>(ok.Value);

        Assert.Equal("You are a concise assistant.", payload.Prompt);
    }

    [Fact]
    public async Task SetPrompt_ShouldReturnBadRequest_WhenPromptMissing()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.SetPrompt(new ConversationSetSystemPromptRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(sessionService.LastSetPrompt);
    }

    [Fact]
    public async Task SetPrompt_ShouldUpdatePromptWithProvidedSource()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.SetPrompt(
            new ConversationSetSystemPromptRequest("Keep replies practical.", "manual-api"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptResponse>(ok.Value);

        Assert.Equal("Keep replies practical.", payload.Prompt);
        Assert.Equal("Keep replies practical.", sessionService.LastSetPrompt);
        Assert.Equal("manual-api", sessionService.LastSetPromptSource);
    }

    [Fact]
    public async Task ResetPromptToDefault_ShouldUseDefaultPrompt()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.ResetPromptToDefault(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptResponse>(ok.Value);

        Assert.Equal(AssistantDefaults.SystemPrompt, payload.Prompt);
        Assert.Equal(AssistantDefaults.SystemPrompt, sessionService.LastSetPrompt);
        Assert.Equal("default", sessionService.LastSetPromptSource);
    }

    [Fact]
    public async Task GetPromptHistory_ShouldReturnMappedEntries()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            PromptHistory =
            [
                new SystemPromptEntry
                {
                    Version = 3,
                    PromptText = "Prompt v3",
                    Source = "manual",
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.GetPromptHistory(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationSystemPromptHistoryEntryResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal(3, entry.Version);
        Assert.Equal("Prompt v3", entry.PromptText);
        Assert.Equal("manual", entry.Source);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public async Task GetPromptProposals_ShouldReturnMappedEntries()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            PromptProposals =
            [
                new SystemPromptProposal
                {
                    Id = 7,
                    ProposedPrompt = "New prompt",
                    Reason = "Improve clarity",
                    Confidence = 0.83,
                    Source = "ai",
                    Status = "pending",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    ReviewedAtUtc = null,
                    AppliedSystemPromptEntryId = null
                }
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.GetPromptProposals(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationSystemPromptProposalResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal(7, entry.Id);
        Assert.Equal("New prompt", entry.ProposedPrompt);
        Assert.Equal("Improve clarity", entry.Reason);
        Assert.Equal("pending", entry.Status);
    }

    [Fact]
    public async Task CreatePromptProposal_ShouldCreateAndMapProposal()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            NextCreatedProposal = new SystemPromptProposal
            {
                Id = 11,
                ProposedPrompt = "Use concise outputs",
                Reason = "Reduce noise",
                Confidence = 0.9,
                Source = "manual",
                Status = "pending",
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.CreatePromptProposal(
            new ConversationCreatePromptProposalRequest("Use concise outputs", "Reduce noise", 0.9, "manual"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptProposalResponse>(ok.Value);

        Assert.Equal(11, payload.Id);
        Assert.Equal("Use concise outputs", sessionService.LastProposalPrompt);
        Assert.Equal("Reduce noise", sessionService.LastProposalReason);
        Assert.Equal(0.9, sessionService.LastProposalConfidence);
        Assert.Equal("manual", sessionService.LastProposalSource);
    }

    [Fact]
    public async Task ImprovePromptProposal_ShouldCreateProposalFromGoal()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            NextGeneratedProposal = new SystemPromptProposal
            {
                Id = 12,
                ProposedPrompt = "Prioritize examples",
                Reason = "AI-generated",
                Confidence = 0.7,
                Source = "assistant",
                Status = "pending",
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.ImprovePromptProposal(
            new ConversationPromptImproveRequest("Focus on examples"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptProposalResponse>(ok.Value);

        Assert.Equal(12, payload.Id);
        Assert.Equal("Focus on examples", sessionService.LastImproveGoal);
    }

    [Fact]
    public async Task ApplyPromptProposal_ShouldReturnUpdatedPrompt()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            ApplyPromptResult = "Applied prompt"
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.ApplyPromptProposal(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationSystemPromptResponse>(ok.Value);

        Assert.Equal("Applied prompt", payload.Prompt);
        Assert.Equal(5, sessionService.LastAppliedProposalId);
    }

    [Fact]
    public async Task ApplyPromptProposal_ShouldReturnNotFound_WhenProposalMissing()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            ThrowOnApply = true
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.ApplyPromptProposal(99, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task RejectPromptProposal_ShouldReturnActionMessage()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.RejectPromptProposal(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationActionResponse>(ok.Value);

        Assert.Equal("Proposal #7 rejected.", payload.Message);
        Assert.Equal(7, sessionService.LastRejectedProposalId);
    }

    [Fact]
    public void ResetConversation_ShouldSetScopeAndResetSession()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = sut.ResetConversation("telegram", "Mike", "chat-42");

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationActionResponse>(ok.Value);

        Assert.Contains("channel=telegram", payload.Message, StringComparison.Ordinal);
        Assert.Contains("userId=mike", payload.Message, StringComparison.Ordinal);
        Assert.Contains("conversationId=chat-42", payload.Message, StringComparison.Ordinal);
        Assert.Equal(1, sessionService.ResetCalls);
        Assert.Equal("telegram", scopeAccessor.Current.Channel);
        Assert.Equal("mike", scopeAccessor.Current.UserId);
        Assert.Equal("chat-42", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task PostMessage_ShouldUseDefaultChannel_WhenNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(new ConversationMessageRequest("void"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationMessageResponse>(ok.Value);

        Assert.Equal("api", orchestrator.LastChannel);
        Assert.Equal("vocabulary.single", payload.Intent);
    }

    [Fact]
    public async Task PostMessage_ShouldNormalizeProvidedChannel()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(new ConversationMessageRequest("void", "  TeLeGrAm  "), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("telegram", orchestrator.LastChannel);
    }

    [Fact]
    public async Task PostMessage_ShouldForwardUserAndConversationIds()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(
            new ConversationMessageRequest("void", "api", "Mike", "chat-42"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("mike", orchestrator.LastUserId);
        Assert.Equal("chat-42", orchestrator.LastConversationId);
    }

    [Fact]
    public async Task PostMessage_ShouldApplyStoredStorageMode_WhenOverrideNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Graph
        };
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, storageModeProvider, storagePreferenceService, new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(
            new ConversationMessageRequest("void", "api", "Mike", "chat-42"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
        Assert.NotNull(storagePreferenceService.LastGetScope);
        Assert.Equal("api", storagePreferenceService.LastGetScope!.Channel);
        Assert.Equal("mike", storagePreferenceService.LastGetScope!.UserId);
    }

    [Fact]
    public async Task PostMessage_ShouldApplyOverrideStorageMode_WhenProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Local
        };
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, storageModeProvider, storagePreferenceService, new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(
            new ConversationMessageRequest("void", "api", "Mike", "chat-42", "graph"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
        Assert.Null(storagePreferenceService.LastGetScope);
    }

    [Fact]
    public async Task PostMessage_ShouldReturnBadRequest_WhenOverrideStorageModeUnsupported()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            SupportedModes = ["local", "graph", "hybrid"]
        };
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, storageModeProvider, storagePreferenceService, new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(
            new ConversationMessageRequest("void", "api", "Mike", "chat-42", "cloud"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Unsupported mode 'cloud'. Use one of: local, graph, hybrid.", badRequest.Value);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task PostMessage_ShouldMapCommandResultPayload()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty("command-agent", "command.prompt.set", "System prompt updated and saved.")
        };

        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(new ConversationMessageRequest("/prompt set Keep replies concise"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationMessageResponse>(ok.Value);

        Assert.Equal("command-agent", payload.Agent);
        Assert.Equal("command.prompt.set", payload.Intent);
        Assert.False(payload.IsBatch);
        Assert.Empty(payload.Items);
        Assert.Equal("System prompt updated and saved.", payload.Message);
    }

    [Fact]
    public async Task PostMessage_ShouldExposeSuggestedPartOfSpeechAndDuplicateMatches()
    {
        var duplicate = new VocabularyDeckEntry(
            DeckFileName: "wm-verbs-us-en.xlsx",
            DeckPath: "C:\\Decks\\wm-verbs-us-en.xlsx",
            RowNumber: 42,
            Word: "prepare",
            Meaning: "(v) готувати",
            Examples: "We prepare release notes.");

        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = new ConversationAgentResult(
                AgentName: "vocabulary-agent",
                Intent: "vocabulary.single",
                IsBatch: false,
                Items:
                [
                    new ConversationAgentItemResult(
                        Input: "prepare",
                        Lookup: new VocabularyLookupResult("prepare", []),
                        AssistantCompletion: new AssistantCompletionResult(
                            Content: "prepare\n\n(v) готувати\n\nWe prepare release notes.",
                            Model: "gpt-4.1-mini",
                            Usage: null),
                        AppendPreview: new VocabularyAppendPreviewResult(
                            Status: VocabularyAppendPreviewStatus.ReadyToAppend,
                            Word: "prepare",
                            TargetDeckFileName: "wm-verbs-us-en.xlsx",
                            TargetDeckPath: "C:\\Decks\\wm-verbs-us-en.xlsx",
                            DuplicateMatches: [duplicate],
                            Message: null))
                ])
        };

        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(
            orchestrator,
            sessionService,
            scopeAccessor,
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(new ConversationMessageRequest("prepare"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationMessageResponse>(ok.Value);
        var item = Assert.Single(payload.Items);

        Assert.True(item.ReadyToAppend);
        Assert.Equal("v", item.SuggestedPartOfSpeech);

        var duplicatePayload = Assert.Single(item.DuplicateMatches!);
        Assert.Equal("wm-verbs-us-en.xlsx", duplicatePayload.DeckFileName);
        Assert.Equal(42, duplicatePayload.RowNumber);
        Assert.Equal("prepare", duplicatePayload.Word);
    }

    [Fact]
    public async Task PostMessage_ShouldReturnBadRequest_WhenInputIsEmpty()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor, new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), new FakeConversationCommandCatalogService());

        var response = await sut.PostMessage(new ConversationMessageRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, orchestrator.Calls);
    }

    private sealed class FakeConversationCommandCatalogService : IConversationCommandCatalogService
    {
        public IReadOnlyList<ConversationCommandCatalogItem> GetCommands()
            => ConversationCommandCatalog.SlashCommands;

        public IReadOnlyList<ConversationCommandCatalogGroup> GetGroups()
            => ConversationCommandCatalog.SlashCommandGroups;
    }

    private sealed class FakeConversationOrchestrator : IConversationOrchestrator
    {
        public int Calls { get; private set; }

        public string LastChannel { get; private set; } = string.Empty;

        public string? LastUserId { get; private set; }

        public string? LastConversationId { get; private set; }

        public ConversationAgentResult NextResult { get; set; } = new(
            "vocabulary-agent",
            "vocabulary.single",
            false,
            []);

        public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
            => ProcessAsync(input, "unknown", null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            CancellationToken cancellationToken = default)
            => ProcessAsync(input, channel, null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            string locale,
            CancellationToken cancellationToken)
            => ProcessAsync(input, channel, null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            string? userId,
            string? conversationId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChannel = channel;
            LastUserId = userId;
            LastConversationId = conversationId;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeVocabularyStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = default;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }

    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; set; } = ["local", "graph"];

        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public ConversationScope? LastGetScope { get; private set; }

        public ConversationScope? LastSetScope { get; private set; }

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            LastGetScope = scope;
            return Task.FromResult(CurrentMode);
        }

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
        {
            LastSetScope = scope;
            CurrentMode = mode;
            return Task.FromResult(mode);
        }
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public IReadOnlyCollection<ConversationMessage> History { get; set; } = [];

        public IReadOnlyCollection<UserMemoryEntry> Memory { get; set; } = [];

        public string SystemPrompt { get; set; } = "prompt";

        public IReadOnlyCollection<SystemPromptEntry> PromptHistory { get; set; } = [];

        public IReadOnlyCollection<SystemPromptProposal> PromptProposals { get; set; } = [];

        public SystemPromptProposal NextCreatedProposal { get; set; } = new()
        {
            Id = 1,
            ProposedPrompt = "proposal",
            Reason = "reason",
            Confidence = 0.8,
            Source = "manual",
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        public SystemPromptProposal NextGeneratedProposal { get; set; } = new()
        {
            Id = 2,
            ProposedPrompt = "generated",
            Reason = "ai",
            Confidence = 0.7,
            Source = "assistant",
            Status = "pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        public string ApplyPromptResult { get; set; } = "prompt";

        public string? LastSetPrompt { get; private set; }

        public string? LastSetPromptSource { get; private set; }

        public string? LastProposalPrompt { get; private set; }

        public string? LastProposalReason { get; private set; }

        public double? LastProposalConfidence { get; private set; }

        public string? LastProposalSource { get; private set; }

        public string? LastImproveGoal { get; private set; }

        public int? LastAppliedProposalId { get; private set; }

        public int? LastRejectedProposalId { get; private set; }

        public int ResetCalls { get; private set; }

        public bool ThrowOnApply { get; set; }

        public bool ThrowOnReject { get; set; }

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("ok", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Memory);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(SystemPrompt);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(PromptHistory);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(PromptProposals);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(
            string prompt,
            string reason,
            double confidence,
            string source = "manual",
            CancellationToken cancellationToken = default)
        {
            LastProposalPrompt = prompt;
            LastProposalReason = reason;
            LastProposalConfidence = confidence;
            LastProposalSource = source;
            return Task.FromResult(NextCreatedProposal);
        }

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
        {
            LastImproveGoal = goal;
            return Task.FromResult(NextGeneratedProposal);
        }

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("Proposal not found.");
            }

            LastAppliedProposalId = proposalId;
            return Task.FromResult(ApplyPromptResult);
        }

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnReject)
            {
                throw new InvalidOperationException("Proposal not found.");
            }

            LastRejectedProposalId = proposalId;
            return Task.CompletedTask;
        }

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
        {
            LastSetPrompt = prompt;
            LastSetPromptSource = source;
            SystemPrompt = prompt;
            return Task.FromResult(prompt);
        }

        public void Reset()
        {
            ResetCalls++;
        }
    }
}




