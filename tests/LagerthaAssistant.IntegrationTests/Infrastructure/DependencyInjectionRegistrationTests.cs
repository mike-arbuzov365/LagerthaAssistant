namespace LagerthaAssistant.IntegrationTests.Infrastructure;

using LagerthaAssistant.Infrastructure;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class DependencyInjectionRegistrationTests
{
    [Fact]
    public void AddLagerthaAssistant_ShouldRegisterGraphVocabularyDeckServiceAsSingleton()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVocabularyReplyParser, VocabularyReplyParser>();

        services.AddLagerthaAssistant(configuration);

        var graphServiceDescriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(GraphVocabularyDeckService));

        Assert.Equal(ServiceLifetime.Singleton, graphServiceDescriptor.Lifetime);
    }

    [Fact]
    public async Task AddLagerthaAssistant_ShouldResolveSameGraphVocabularyDeckServiceAcrossScopes()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVocabularyReplyParser, VocabularyReplyParser>();
        services.AddLagerthaAssistant(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope1 = provider.CreateAsyncScope();
        await using var scope2 = provider.CreateAsyncScope();

        var graphService1 = scope1.ServiceProvider.GetRequiredService<GraphVocabularyDeckService>();
        var graphService2 = scope2.ServiceProvider.GetRequiredService<GraphVocabularyDeckService>();

        Assert.Same(graphService1, graphService2);
    }

    private static IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["OpenAI:ApiKey"] = "test-key"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
