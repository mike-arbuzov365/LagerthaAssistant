namespace SharedBotKernel.Options;

using SharedBotKernel.Constants;

public sealed class OpenAiOptions
{
    public string BaseUrl { get; set; } = OpenAiConstants.DefaultBaseUrl;

    public string Model { get; set; } = OpenAiConstants.DefaultModel;

    public string? ApiKey { get; set; }

    public double Temperature { get; set; } = OpenAiConstants.DefaultTemperature;
}
