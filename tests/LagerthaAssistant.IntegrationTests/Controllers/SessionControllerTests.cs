namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class SessionControllerTests
{
    [Fact]
    public async Task GetBootstrap_ShouldReturnCombinedSessionPayload_WithDefaults()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService
        {
            SaveMode = "auto",
            StorageMode = "local",
            Graph = new GraphAuthStatus(true, false, "Not authenticated.", null),
            CommandGroups =
            [
                new ConversationCommandCatalogGroup(
                    "Session",
                    [new ConversationCommandCatalogItem("Session", "/help", "Show help")])
            ],
            PartOfSpeechOptions =
            [
                new VocabularyPartOfSpeechOption(1, "n", "noun", ["n", "noun"])
            ]
        };

        var sut = new SessionController(scopeAccessor, bootstrapService);

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("api", payload.Scope.Channel);
        Assert.Equal("anonymous", payload.Scope.UserId);
        Assert.Equal("default", payload.Scope.ConversationId);

        Assert.Equal("auto", payload.Preferences.SaveMode);
        Assert.Equal("local", payload.Preferences.StorageMode);
        Assert.Equal(["ask", "auto", "off"], payload.Preferences.AvailableSaveModes);
        Assert.Equal(["local", "graph"], payload.Preferences.AvailableStorageModes);

        Assert.True(payload.Graph.IsConfigured);
        Assert.False(payload.Graph.IsAuthenticated);
        Assert.Equal("Not authenticated.", payload.Graph.Message);

        Assert.NotEmpty(payload.CommandGroups);
        Assert.Contains(payload.CommandGroups, g => g.Category == "Session");
        Assert.NotEmpty(payload.PartOfSpeechOptions);
        Assert.Contains(payload.PartOfSpeechOptions, option => option.Marker == "n");
        Assert.Null(payload.WritableDecks);
    }

    [Fact]
    public async Task GetBootstrap_ShouldNormalizeScopeFromQuery()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService();

        var sut = new SessionController(scopeAccessor, bootstrapService);

        var response = await sut.GetBootstrap(
            " TeLeGrAm ",
            "Mike",
            "chat-42",
            includeDecks: true,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("telegram", payload.Scope.Channel);
        Assert.Equal("mike", payload.Scope.UserId);
        Assert.Equal("chat-42", payload.Scope.ConversationId);
        Assert.Equal(scopeAccessor.Current, bootstrapService.LastScope);
        Assert.True(bootstrapService.LastIncludeDecks);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeConversationBootstrapService : IConversationBootstrapService
    {
        public string SaveMode { get; set; } = "ask";

        public IReadOnlyList<string> AvailableSaveModes { get; set; } = ["ask", "auto", "off"];

        public string StorageMode { get; set; } = "local";

        public IReadOnlyList<string> AvailableStorageModes { get; set; } = ["local", "graph"];

        public GraphAuthStatus Graph { get; set; } = new(true, false, "Not authenticated.", null);

        public IReadOnlyList<ConversationCommandCatalogGroup> CommandGroups { get; set; } = [];

        public IReadOnlyList<VocabularyPartOfSpeechOption> PartOfSpeechOptions { get; set; } = [];

        public ConversationScope? LastScope { get; private set; }
        public bool LastIncludeDecks { get; private set; }

        public Task<ConversationBootstrapSnapshot> BuildAsync(
            ConversationScope scope,
            bool includeDecks = false,
            CancellationToken cancellationToken = default)
        {
            LastScope = scope;
            LastIncludeDecks = includeDecks;
            return Task.FromResult(new ConversationBootstrapSnapshot(
                scope,
                SaveMode,
                AvailableSaveModes,
                StorageMode,
                AvailableStorageModes,
                Graph,
                CommandGroups,
                PartOfSpeechOptions,
                null));
        }
    }
}
