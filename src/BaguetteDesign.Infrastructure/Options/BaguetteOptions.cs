namespace BaguetteDesign.Infrastructure.Options;

public sealed class BaguetteOptions
{
    public const string SectionName = "Baguette";

    public long AdminUserId { get; set; }

    /// <summary>Railway public URL, e.g. https://baguette-design.up.railway.app</summary>
    public string WebAppUrl { get; set; } = string.Empty;
}
