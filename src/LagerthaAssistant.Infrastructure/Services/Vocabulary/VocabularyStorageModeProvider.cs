namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;

public sealed class VocabularyStorageModeProvider : IVocabularyStorageModeProvider
{
    private readonly object _sync = new();
    private VocabularyStorageMode _currentMode;

    public VocabularyStorageModeProvider(VocabularyStorageOptions options)
    {
        _currentMode = ParseOrDefault(options.DefaultMode);
    }

    public VocabularyStorageMode CurrentMode
    {
        get
        {
            lock (_sync)
            {
                return _currentMode;
            }
        }
    }

    public void SetMode(VocabularyStorageMode mode)
    {
        lock (_sync)
        {
            _currentMode = mode;
        }
    }

    public bool TryParse(string? value, out VocabularyStorageMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "local":
                mode = VocabularyStorageMode.Local;
                return true;
            case "graph":
                mode = VocabularyStorageMode.Graph;
                return true;
            default:
                mode = VocabularyStorageMode.Local;
                return false;
        }
    }

    public string ToText(VocabularyStorageMode mode)
    {
        return mode switch
        {
            VocabularyStorageMode.Local => "local",
            VocabularyStorageMode.Graph => "graph",
            _ => "local"
        };
    }

    private VocabularyStorageMode ParseOrDefault(string? value)
    {
        return TryParse(value, out var mode)
            ? mode
            : VocabularyStorageMode.Local;
    }
}
