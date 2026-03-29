namespace BaguetteDesign.Infrastructure.Options;

public sealed class NotionPriceOptions
{
    public const string SectionName = "NotionPrice";

    public string ApiKey { get; init; } = string.Empty;
    public string DatabaseId { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.notion.com/v1";
    public string Version { get; init; } = "2022-06-28";

    // Property names in the Notion database
    public string NameProperty { get; init; } = "Name";
    public string CategoryProperty { get; init; } = "Category";
    public string DescriptionProperty { get; init; } = "Description";
    public string PriceProperty { get; init; } = "Price";
    public string CurrencyProperty { get; init; } = "Currency";
    public string CountryProperty { get; init; } = "Country";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(DatabaseId);
}
