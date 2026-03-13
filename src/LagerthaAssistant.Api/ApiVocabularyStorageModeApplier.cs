namespace LagerthaAssistant.Api;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

internal static class ApiVocabularyStorageModeApplier
{
    public static async Task<(bool Success, string? Error, VocabularyStorageMode Mode)> TryApplyAsync(
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        ConversationScope scope,
        string? requestedStorageMode,
        CancellationToken cancellationToken = default)
    {
        VocabularyStorageMode mode;

        if (!string.IsNullOrWhiteSpace(requestedStorageMode))
        {
            if (!storageModeProvider.TryParse(requestedStorageMode, out mode))
            {
                return (false, $"Unsupported mode '{requestedStorageMode}'. Use local or graph.", default);
            }
        }
        else
        {
            mode = await storagePreferenceService.GetModeAsync(scope, cancellationToken);
        }

        storageModeProvider.SetMode(mode);
        return (true, null, mode);
    }
}
