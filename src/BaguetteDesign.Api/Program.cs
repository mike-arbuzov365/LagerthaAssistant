using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Interfaces;
using BaguetteDesign.Infrastructure.AI;
using BaguetteDesign.Infrastructure.Data;
using BaguetteDesign.Infrastructure.Options;
using BaguetteDesign.Infrastructure.Calendar;
using BaguetteDesign.Infrastructure.Notion;
using BaguetteDesign.Infrastructure.Options;
using BaguetteDesign.Infrastructure.Telegram;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Telegram;
using SharedBotKernel.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BaguetteDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var baguetteOptions = builder.Configuration
    .GetSection(BaguetteOptions.SectionName)
    .Get<BaguetteOptions>() ?? new BaguetteOptions();

builder.Services.AddSingleton<IRoleRouter>(new RoleRouter(baguetteOptions.AdminUserId));

builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient("telegram");
builder.Services.AddSingleton<ITelegramBotSender, TelegramBotSender>();
builder.Services.AddScoped<IStartCommandHandler, StartCommandHandler>();

var claudeOptions = builder.Configuration
    .GetSection("Claude")
    .Get<ClaudeOptions>() ?? new ClaudeOptions();
builder.Services.AddSingleton(claudeOptions);
builder.Services.AddSingleton<ClaudeChatClient>(sp =>
    new ClaudeChatClient(claudeOptions, sp.GetRequiredService<ILogger<ClaudeChatClient>>()));
builder.Services.AddSingleton<IAiChatClient, ClaudeChatClientAdapter>();

builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IQuestionHandler, QuestionHandler>();

builder.Services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
builder.Services.AddScoped<ILeadRepository, LeadRepository>();
builder.Services.AddScoped<IBriefFlowService, BriefFlowService>();

var notionPriceOptions = builder.Configuration
    .GetSection(NotionPriceOptions.SectionName)
    .Get<NotionPriceOptions>() ?? new NotionPriceOptions();
builder.Services.AddSingleton(notionPriceOptions);
builder.Services.AddHttpClient<INotionPriceClient, NotionPriceClient>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<IPriceHandler, PriceHandler>();

var notionPortfolioOptions = builder.Configuration
    .GetSection(NotionPortfolioOptions.SectionName)
    .Get<NotionPortfolioOptions>() ?? new NotionPortfolioOptions();
builder.Services.AddSingleton(notionPortfolioOptions);
builder.Services.AddHttpClient<INotionPortfolioClient, NotionPortfolioClient>();
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IPortfolioHandler, PortfolioHandler>();

var googleCalendarOptions = builder.Configuration
    .GetSection(GoogleCalendarOptions.SectionName)
    .Get<GoogleCalendarOptions>() ?? new GoogleCalendarOptions();
builder.Services.AddSingleton(googleCalendarOptions);
builder.Services.AddHttpClient<IGoogleTokenProvider, GoogleTokenProvider>();
builder.Services.AddHttpClient<ICalendarService, GoogleCalendarService>();
builder.Services.AddSingleton<IDesignerNotifier>(sp =>
    new DesignerNotifier(
        sp.GetRequiredService<ITelegramBotSender>(),
        baguetteOptions));
builder.Services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IContactHandler, ContactHandler>();
builder.Services.AddScoped<IStatusHandler, StatusHandler>();
builder.Services.AddScoped<IDialogStateRepository, DialogStateRepository>();
builder.Services.AddScoped<IInboxHandler, InboxHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", async (BaguetteDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", db = "connected" });
    }
    catch
    {
        return Results.Ok(new { status = "healthy", db = "unavailable" });
    }
});

app.MapControllers();

app.Run();
