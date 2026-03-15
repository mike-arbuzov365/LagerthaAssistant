namespace LagerthaAssistant.Api.Options;

using Microsoft.Extensions.Options;

public sealed class TelegramOptionsValidator : IValidateOptions<TelegramOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramOptions options)
    {
        if (options.Enabled && string.IsNullOrWhiteSpace(options.BotToken))
        {
            return ValidateOptionsResult.Fail("Telegram:BotToken is required when Telegram is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
