using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.UI.Constants;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static readonly IReadOnlyDictionary<string, string> CommandCatalogDescriptions =
        ConversationCommandCatalog.SlashCommands
            .ToDictionary(item => item.Command, item => item.Description, StringComparer.OrdinalIgnoreCase);

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
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine();

        var promptSetText = $"{ConsoleCommands.PromptSet} <text>";
        var promptProposeText = $"{ConsoleCommands.PromptPropose} <reason> || <text>";
        var promptImproveText = $"{ConsoleCommands.PromptImprove} <goal>";
        var promptApplyText = $"{ConsoleCommands.PromptApply} <id>";
        var promptRejectText = $"{ConsoleCommands.PromptReject} <id>";
        var syncRunWithCountText = $"{ConsoleCommands.SyncRun} <n>";

        Console.WriteLine("General");
        WriteCatalogCommandHelp(ConsoleCommands.Help, "Show this help message.");
        WriteCommandHelp(ConsoleCommands.Batch, "Start smart-paste batch mode for multiple words/phrases/sentences.");

        Console.WriteLine();
        Console.WriteLine("Conversation");
        WriteCatalogCommandHelp(ConsoleCommands.History, "Show recent conversation history.");
        WriteCatalogCommandHelp(ConsoleCommands.Memory, "Show active memory facts.");

        Console.WriteLine();
        Console.WriteLine("System prompt");
        WriteCatalogCommandHelp(ConsoleCommands.Prompt, "Show the active system prompt.");
        WriteCatalogCommandHelp(ConsoleCommands.PromptDefault, "Reset active system prompt to default and save it.");
        WriteCatalogCommandHelp(ConsoleCommands.PromptHistory, "Show saved system prompt versions.");
        WriteCommandHelp(ConsoleCommands.PromptSet, "Start multiline prompt editor (finish with /end, cancel with /cancel).");
        WriteCatalogCommandHelp(promptSetText, "Set system prompt from a single line.");

        Console.WriteLine();
        Console.WriteLine("Prompt proposals");
        WriteCatalogCommandHelp(ConsoleCommands.PromptProposals, "Show recent prompt proposals.");
        WriteCatalogCommandHelp(promptProposeText, "Create a manual proposal for a new prompt.");
        WriteCatalogCommandHelp(promptImproveText, "Ask AI to generate a prompt proposal for your goal.");
        WriteCatalogCommandHelp(promptApplyText, "Apply a pending proposal and make it active.");
        WriteCatalogCommandHelp(promptRejectText, "Reject a pending proposal.");

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
        Console.WriteLine("Sync queue");
        WriteCatalogCommandHelp(ConsoleCommands.Sync, "Show pending vocabulary sync jobs.");
        WriteCatalogCommandHelp(ConsoleCommands.SyncStatus, "Alias for /sync.");
        WriteCatalogCommandHelp(ConsoleCommands.SyncRun, "Run pending sync jobs (default batch size: 25).");
        WriteCatalogCommandHelp(syncRunWithCountText, "Run up to <n> pending sync jobs now.");

        Console.WriteLine();
        Console.WriteLine("Session");
        WriteCatalogCommandHelp(ConsoleCommands.Reset, "Reset in-memory conversation context.");
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
