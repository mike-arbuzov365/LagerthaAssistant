using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.UI.Constants;
using System.Text;

namespace LagerthaAssistant.UI;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging
            .ClearProviders()
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information);

        var sessionOptions = BuildSessionOptions(builder.Configuration);

        builder.Services
            .AddApplication(sessionOptions)
            .AddLagerthaAssistant(builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrated and ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database.");
            throw;
        }

        var aiOptions = services.GetRequiredService<OpenAiOptions>();
        var assistantSession = services.GetRequiredService<IAssistantSessionService>();

        if (string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            Console.WriteLine($"{OpenAiConstants.ApiKeyEnvironmentVariable} is not configured.");
            Console.WriteLine($"Set environment variable {OpenAiConstants.ApiKeyEnvironmentVariable} and run again.");
            return;
        }

        await RunConsoleAssistantAsync(assistantSession, aiOptions.Model);
    }

    private static async Task RunConsoleAssistantAsync(IAssistantSessionService assistantSession, string model)
    {
        PrintBanner(model);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You > ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (input is null)
            {
                continue;
            }

            var command = input.Trim();
            if (command.Equals(ConsoleCommands.Exit, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (command.Equals(ConsoleCommands.Reset, StringComparison.OrdinalIgnoreCase))
            {
                assistantSession.Reset();
                Console.WriteLine("Conversation has been reset.");
                continue;
            }

            if (command.Equals(ConsoleCommands.History, StringComparison.OrdinalIgnoreCase))
            {
                var preview = await assistantSession.GetRecentHistoryAsync(ConsoleCommands.HistoryPreviewTake);
                PrintHistory(preview);
                continue;
            }

            if (command.Equals(ConsoleCommands.Memory, StringComparison.OrdinalIgnoreCase)
                || command.Equals(ConsoleCommands.MemoryAlias, StringComparison.OrdinalIgnoreCase))
            {
                var memory = await assistantSession.GetActiveMemoryAsync(ConsoleCommands.MemoryPreviewTake);
                PrintMemory(memory);
                continue;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            try
            {
                var result = await assistantSession.AskAsync(command);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Assistant ({result.Model}) > {result.Content}");
                Console.ResetColor();

                if (result.Usage is not null)
                {
                    Console.WriteLine(
                        $"Tokens: prompt={result.Usage.PromptTokens}, completion={result.Usage.CompletionTokens}, total={result.Usage.TotalTokens}");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        Console.WriteLine("Assistant session ended.");
    }

    private static void PrintBanner(string model)
    {
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("OpenAI Console Assistant Prototype");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Commands: {ConsoleCommands.History}, {ConsoleCommands.Memory} ({ConsoleCommands.MemoryAlias}), {ConsoleCommands.Reset}, {ConsoleCommands.Exit}");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
    }

    private static void PrintHistory(IReadOnlyCollection<ConversationMessage> messages)
    {
        Console.WriteLine();
        Console.WriteLine("Conversation history:");

        if (messages.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var message in messages)
        {
            Console.WriteLine($"- {message.Role}: {message.Content}");
        }

        Console.WriteLine();
    }

    private static void PrintMemory(IReadOnlyCollection<UserMemoryEntry> memoryEntries)
    {
        Console.WriteLine();
        Console.WriteLine("Stored memory:");

        if (memoryEntries.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var memory in memoryEntries)
        {
            Console.WriteLine($"- {memory.Key}: {memory.Value} (confidence={memory.Confidence:F2}, seen={memory.LastSeenAtUtc:yyyy-MM-dd HH:mm:ss} UTC)");
        }

        Console.WriteLine();
    }

    private static AssistantSessionOptions BuildSessionOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("AssistantSession");

        var systemPrompt = section["SystemPrompt"];
        var maxHistoryRaw = section["MaxHistoryMessages"];

        var maxHistory = AssistantDefaults.MaxHistoryMessages;
        if (int.TryParse(maxHistoryRaw, out var parsed) && parsed > 1)
        {
            maxHistory = parsed;
        }

        return new AssistantSessionOptions
        {
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? AssistantDefaults.SystemPrompt
                : systemPrompt,
            MaxHistoryMessages = maxHistory
        };
    }
}

