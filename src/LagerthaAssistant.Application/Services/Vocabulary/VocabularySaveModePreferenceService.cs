namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;

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
        var entry = await GetScopedOrLegacyEntryAsync(scope, cancellationToken);
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
        var now = DateTimeOffset.UtcNow;

        var entry = await _userMemoryRepository.GetByKeyAsync(
            UserPreferenceMemoryKeys.SaveMode,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (entry is null)
        {
            await _userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = UserPreferenceMemoryKeys.SaveMode,
                Value = modeValue,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = now,
                Channel = scope.Channel,
                UserId = scope.UserId
            }, cancellationToken);
        }
        else
        {
            entry.Value = modeValue;
            entry.Confidence = 1.0;
            entry.IsActive = false;
            entry.LastSeenAtUtc = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return mode;
    }

    private async Task<UserMemoryEntry?> GetScopedOrLegacyEntryAsync(ConversationScope scope, CancellationToken cancellationToken)
    {
        var scoped = await _userMemoryRepository.GetByKeyAsync(
            UserPreferenceMemoryKeys.SaveMode,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (scoped is not null)
        {
            return scoped;
        }

        if (scope.Channel.Equals(ConversationScope.DefaultChannel, StringComparison.Ordinal)
            && scope.UserId.Equals(ConversationScope.DefaultUserId, StringComparison.Ordinal))
        {
            return null;
        }

        return await _userMemoryRepository.GetByKeyAsync(UserPreferenceMemoryKeys.SaveMode, cancellationToken);
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
