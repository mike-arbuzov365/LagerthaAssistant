namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;

public sealed class VocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
{
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;

    public VocabularyStoragePreferenceService(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        IVocabularyStorageModeProvider storageModeProvider)
    {
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _storageModeProvider = storageModeProvider;
    }

    public async Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
    {
        var entry = await GetScopedOrLegacyEntryAsync(scope, cancellationToken);
        if (entry is not null && _storageModeProvider.TryParse(entry.Value, out var parsedMode))
        {
            return parsedMode;
        }

        return _storageModeProvider.CurrentMode;
    }

    public async Task<VocabularyStorageMode> SetModeAsync(
        ConversationScope scope,
        VocabularyStorageMode mode,
        CancellationToken cancellationToken = default)
    {
        var modeValue = _storageModeProvider.ToText(mode);
        var now = DateTimeOffset.UtcNow;

        var entry = await _userMemoryRepository.GetByKeyAsync(
            UserPreferenceMemoryKeys.StorageMode,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (entry is null)
        {
            await _userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = UserPreferenceMemoryKeys.StorageMode,
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
            UserPreferenceMemoryKeys.StorageMode,
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

        return await _userMemoryRepository.GetByKeyAsync(UserPreferenceMemoryKeys.StorageMode, cancellationToken);
    }
}
