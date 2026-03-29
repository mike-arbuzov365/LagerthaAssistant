using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Interfaces;
using BaguetteDesign.Infrastructure.AI;
using BaguetteDesign.Infrastructure.Data;
using BaguetteDesign.Infrastructure.Options;
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
