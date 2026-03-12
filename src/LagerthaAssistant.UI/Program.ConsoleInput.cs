namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static bool TryReadInputLine(out string input)
    {
        var line = Console.ReadLine();
        if (line is null)
        {
            input = string.Empty;
            PrintInputStreamClosedWarning();
            return false;
        }

        input = line;
        return true;
    }

    private static bool TryReadTrimmedLowerInput(out string input)
    {
        if (!TryReadInputLine(out var line))
        {
            input = string.Empty;
            return false;
        }

        input = line.Trim().ToLowerInvariant();
        return true;
    }

    private static bool TryReadTrimmedInput(out string input)
    {
        if (!TryReadInputLine(out var line))
        {
            input = string.Empty;
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
