using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
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
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddSingleton<IValidateOptions<TelegramOptions>, TelegramOptionsValidator>();
builder.Services.AddHttpClient("telegram", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("vocab-discovery", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddSingleton<ITelegramConversationResponseFormatter, TelegramConversationResponseFormatter>();
builder.Services.AddSingleton<ITelegramBotSender, TelegramBotSender>();
builder.Services.AddSingleton<ITelegramNavigationPresenter, TelegramNavigationPresenter>();
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

app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));
app.MapControllers();

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


