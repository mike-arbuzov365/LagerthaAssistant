using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Infrastructure;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Api.HostedServices;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information);

var sessionOptions = BuildSessionOptions(builder.Configuration);

builder.Services
    .AddApplication(sessionOptions)
    .AddLagerthaAssistant(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<VocabularySyncWorkerOptions>(builder.Configuration.GetSection("VocabularySyncWorker"));
builder.Services.Configure<NotionSyncWorkerOptions>(builder.Configuration.GetSection("NotionSyncWorker"));
builder.Services.Configure<FoodSyncWorkerOptions>(builder.Configuration.GetSection("FoodSyncWorker"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient("telegram", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("vocab-discovery", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<LagerthaAssistant.Application.Interfaces.Food.INotionFoodClient, LagerthaAssistant.Infrastructure.Services.Food.NotionFoodClient>("notion-food", (sp, client) =>
{
    var opts = sp.GetRequiredService<LagerthaAssistant.Infrastructure.Options.NotionFoodOptions>();
    client.BaseAddress = new Uri(opts.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
});
builder.Services.AddSingleton<ITelegramConversationResponseFormatter, TelegramConversationResponseFormatter>();
builder.Services.AddSingleton<ITelegramBotSender, SharedBotKernel.Infrastructure.Telegram.TelegramBotSender>();
builder.Services.AddSingleton<ITelegramNavigationPresenter>(sp =>
{
    var localizationService = sp.GetRequiredService<ILocalizationService>();
    var settingsUrl = ResolveMiniAppSettingsUrl(builder.Configuration);
    return new TelegramNavigationPresenter(localizationService, settingsUrl);
});
builder.Services.AddSingleton<TelegramPendingStateStore>();
builder.Services.AddScoped<ITelegramImportSourceReader, TelegramImportSourceReader>();
builder.Services.AddScoped<IVocabularyDiscoveryService, VocabularyDiscoveryService>();
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("telegram-webhook", o =>
    {
        o.PermitLimit = 30;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddHostedService<VocabularySyncHostedService>();
builder.Services.AddHostedService<NotionSyncHostedService>();
builder.Services.AddHostedService<FoodSyncHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrated and ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var miniAppRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "miniapp");
if (Directory.Exists(miniAppRoot))
{
    var miniAppFiles = new PhysicalFileProvider(miniAppRoot);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = miniAppFiles,
        RequestPath = "/miniapp"
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = miniAppFiles,
        RequestPath = "/miniapp"
    });
}
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));
app.MapControllers();

if (Directory.Exists(miniAppRoot))
{
    app.MapFallbackToFile("/miniapp/{*path:nonfile}", "index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(miniAppRoot)
    });
}

await app.RunAsync();

static AssistantSessionOptions BuildSessionOptions(IConfiguration configuration)
{
    var section = configuration.GetSection("AssistantSession");

    var systemPrompt = section["SystemPrompt"];
    var maxHistoryRaw = section["MaxHistoryMessages"];

    var maxHistory = AssistantDefaults.MaxHistoryMessages;
    if (int.TryParse(maxHistoryRaw, out var parsed) && parsed > 1)
    {
        maxHistory = parsed;
    }

    return new AssistantSessionOptions
    {
        SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? AssistantDefaults.SystemPrompt
            : systemPrompt,
        MaxHistoryMessages = maxHistory
    };
}



static string? ResolveMiniAppSettingsUrl(IConfiguration configuration)
{
    var explicitSettingsUrl = configuration[$"{TelegramOptions.SectionName}:MiniAppSettingsUrl"];
    if (TryNormalizeHttpsUrl(explicitSettingsUrl, out var resolvedExplicitUrl))
    {
        return resolvedExplicitUrl;
    }

    var publicBaseUrl = configuration["PublicBaseUrl"];
    if (!TryNormalizeHttpsUrl(publicBaseUrl, out var resolvedPublicBaseUrl))
    {
        return null;
    }

    var builder = new UriBuilder(resolvedPublicBaseUrl)
    {
        Path = "/miniapp/settings"
    };

    return builder.Uri.AbsoluteUri;
}

static bool TryNormalizeHttpsUrl(string? raw, out string value)
{
    value = string.Empty;
    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
    {
        return false;
    }

    if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    value = parsed.AbsoluteUri;
    return true;
}
