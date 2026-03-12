namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class VocabularyCardToken : BaseEntity
{
    public int VocabularyCardId { get; set; }

    public VocabularyCard VocabularyCard { get; set; } = null!;

    public string TokenNormalized { get; set; } = string.Empty;
}
