using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.UI.Constants;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static IReadOnlyDictionary<string, string> CommandCatalogDescriptions { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CommandCatalogCommandsByCategory { get; set; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private static void InitializeCommandCatalog(IConversationCommandCatalogService commandCatalogService)
    {
        CommandCatalogDescriptions = commandCatalogService
            .GetCommands()
            .ToDictionary(item => item.Command, item => item.Description, StringComparer.OrdinalIgnoreCase);

        CommandCatalogCommandsByCategory = commandCatalogService
            .GetGroups()
            .ToDictionary(
                group => group.Category,
                group => (IReadOnlyList<string>)group.Commands.Select(item => item.Command).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void PrintBanner(string model)
    {
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("OpenAI Console Assistant Prototype");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine("Type /help to see all commands.");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        var promptSetWithText = $"{ConsoleCommands.PromptSet} <text>";
        var systemPromptExcludedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            promptSetWithText
        };

        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine();

        Console.WriteLine(ConversationCommandCategories.General);
        WriteCatalogCommandsForCategory(ConversationCommandCategories.General);
        WriteCommandHelp(ConsoleCommands.Batch, "Start smart-paste batch mode for multiple words/phrases/sentences.");

        Console.WriteLine();
        Console.WriteLine(ConversationCommandCategories.Conversation);
        WriteCatalogCommandsForCategory(ConversationCommandCategories.Conversation);

        Console.WriteLine();
        Console.WriteLine(ConversationCommandCategories.SystemPrompt);
        WriteCatalogCommandsForCategory(
            ConversationCommandCategories.SystemPrompt,
            systemPromptExcludedCommands);
        WriteCommandHelp(ConsoleCommands.PromptSet, "Start multiline prompt editor (finish with /end, cancel with /cancel).");
        WriteCatalogCommandHelp(promptSetWithText, "Set system prompt from a single line.");

        Console.WriteLine();
        Console.WriteLine(ConversationCommandCategories.PromptProposals);
        WriteCatalogCommandsForCategory(ConversationCommandCategories.PromptProposals);

        Console.WriteLine();
        Console.WriteLine("Saving");
        WriteCommandHelp(ConsoleCommands.Save, "Show current save mode (ask|auto|off).");
        WriteCommandHelp("/save mode ask", "Ask before every save (default). Stored in DB.");
        WriteCommandHelp("/save mode auto", "Save automatically without confirmation. Stored in DB.");
        WriteCommandHelp("/save mode off", "Never save automatically. Stored in DB.");
        Console.WriteLine("In ask mode, option 4 lets you pick another writable deck and override the POS marker.");
        Console.WriteLine("Custom save quick example: 4 -> choose deck number -> enter marker (e.g. prep) -> confirm.");
        Console.WriteLine("All interactive prompts accept digits and text (example: 1 or yes).");

        Console.WriteLine();
        Console.WriteLine("Storage");
        WriteCommandHelp(ConsoleCommands.Storage, "Show current vocabulary storage mode (local|graph).");
        WriteCommandHelp("/storage mode local", "Use local OneDrive-synced Excel files.");
        WriteCommandHelp("/storage mode graph", "Use OneDrive via Microsoft Graph API.");

        Console.WriteLine();
        Console.WriteLine("Graph integration");
        WriteCommandHelp(ConsoleCommands.GraphStatus, "Show Graph authentication/configuration status.");
        WriteCommandHelp(ConsoleCommands.GraphLogin, "Start device-code login for OneDrive access.");
        WriteCommandHelp(ConsoleCommands.GraphLogout, "Sign out and clear cached Graph tokens.");

        Console.WriteLine();
        Console.WriteLine(ConversationCommandCategories.SyncQueue);
        WriteCatalogCommandsForCategory(ConversationCommandCategories.SyncQueue);

        Console.WriteLine();
        Console.WriteLine(ConversationCommandCategories.Session);
        WriteCatalogCommandsForCategory(ConversationCommandCategories.Session);
        WriteCommandHelp(ConsoleCommands.Exit, "Exit the application.");

        Console.WriteLine();
        Console.WriteLine("Vocabulary flow");
        Console.WriteLine("Type an English word/phrase to check writable decks first, then AI answer and save based on current save mode.");
        Console.WriteLine("Use /batch to paste many items: one per line, or single-line text that is auto-split by tab, ;, or sentence punctuation.");
        Console.WriteLine("Batch mode processes items sequentially and asks once at the end before saving (in ask mode).");
        Console.WriteLine("For one-line space-separated input, app can ask whether to keep as one phrase or split into words.");
        Console.WriteLine("What it does: detects phrasal verbs as (pv), keeps persistent expressions as (pe), normalizes irregular verbs to 3 forms as (iv),");
        Console.WriteLine("checks duplicates in writable decks, applies deck-specific save rules, and saves card data to the best deck or your custom target.");
        Console.WriteLine();
    }

    private static void WriteCatalogCommandsForCategory(string category, IReadOnlySet<string>? excludedCommands = null)
    {
        if (!CommandCatalogCommandsByCategory.TryGetValue(category, out var commands))
        {
            return;
        }

        foreach (var command in commands)
        {
            if (excludedCommands is not null && excludedCommands.Contains(command))
            {
                continue;
            }

            WriteCatalogCommandHelp(command, "No description provided.");
        }
    }

    private static void WriteCatalogCommandHelp(string commandText, string fallbackDescription)
    {
        if (!CommandCatalogDescriptions.TryGetValue(commandText, out var description))
        {
            description = fallbackDescription;
        }

        WriteCommandHelp(commandText, description);
    }

    private static void WriteCommandHelp(string commandText, string description)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(commandText);
        Console.ResetColor();
        Console.Write(" - ");
        Console.WriteLine(description);
    }
}
