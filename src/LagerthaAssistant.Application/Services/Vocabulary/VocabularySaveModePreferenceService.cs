namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
{
    private static readonly IReadOnlyList<VocabularySaveMode> OrderedModes = Enum.GetValues<VocabularySaveMode>()
        .ToList();

    private static readonly IReadOnlyDictionary<string, VocabularySaveMode> ParseMap = OrderedModes
        .ToDictionary(ModeToText, mode => mode, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<string> SaveModes = OrderedModes
        .Select(ModeToText)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VocabularySaveModePreferenceService(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork)
    {
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<string> SupportedModes => SaveModes;

    public bool TryParse(string? value, out VocabularySaveMode mode)
    {
        mode = VocabularySaveMode.Ask;
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return ParseMap.TryGetValue(normalized, out mode);
    }

    public string ToText(VocabularySaveMode mode)
    {
        return ModeToText(mode);
    }

    public async Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
    {
        var entry = await VocabularyScopedPreferenceMemory.GetScopedOrLegacyEntryAsync(
            _userMemoryRepository,
            UserPreferenceMemoryKeys.SaveMode,
            scope,
            cancellationToken);

        if (entry is not null && TryParse(entry.Value, out var parsedMode))
        {
            return parsedMode;
        }

        return VocabularySaveMode.Ask;
    }

    public async Task<VocabularySaveMode> SetModeAsync(
        ConversationScope scope,
        VocabularySaveMode mode,
        CancellationToken cancellationToken = default)
    {
        var modeValue = ToText(mode);
        await VocabularyScopedPreferenceMemory.UpsertScopedEntryAsync(
            _userMemoryRepository,
            _unitOfWork,
            UserPreferenceMemoryKeys.SaveMode,
            modeValue,
            scope,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return mode;
    }

    private static string ModeToText(VocabularySaveMode mode)
    {
        return mode switch
        {
            VocabularySaveMode.Ask => "ask",
            VocabularySaveMode.Auto => "auto",
            VocabularySaveMode.Off => "off",
            _ => "ask"
        };
    }
}
