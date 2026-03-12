namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static bool TryReadTrimmedLowerInput(out string input)
    {
        var line = Console.ReadLine();
        if (line is null)
        {
            input = string.Empty;
            PrintInputStreamClosedWarning();
            return false;
        }

        input = line.Trim().ToLowerInvariant();
        return true;
    }

    private static bool TryReadTrimmedInput(out string input)
    {
        var line = Console.ReadLine();
        if (line is null)
        {
            input = string.Empty;
            PrintInputStreamClosedWarning();
            return false;
        }

        input = line.Trim();
        return true;
    }

    private static void PrintInputStreamClosedWarning()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("warning: Input stream closed. Current prompt cancelled.");
        Console.ResetColor();
    }
}
