namespace LagerthaAssistant.Domain.Entities;


public sealed class VocabularyCardToken : BaseEntity
{
    public int VocabularyCardId { get; set; }

    public VocabularyCard VocabularyCard { get; set; } = null!;

    public string TokenNormalized { get; set; } = string.Empty;
}
