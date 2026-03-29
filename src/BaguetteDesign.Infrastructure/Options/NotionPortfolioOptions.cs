namespace BaguetteDesign.Infrastructure.Options;

public sealed class NotionPortfolioOptions
{
    public const string SectionName = "NotionPortfolio";

    public string ApiKey { get; init; } = string.Empty;
    public string DatabaseId { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.notion.com/v1";
    public string Version { get; init; } = "2022-06-28";

    // Property names in the Notion database
    public string TitleProperty { get; init; } = "Name";
    public string CategoryProperty { get; init; } = "Category";
    public string DescriptionProperty { get; init; } = "Description";
    public string TagsProperty { get; init; } = "Tags";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(DatabaseId);
}
