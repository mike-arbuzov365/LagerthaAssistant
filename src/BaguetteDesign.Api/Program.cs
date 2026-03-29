using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Interfaces;
using BaguetteDesign.Infrastructure.Data;
using BaguetteDesign.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BaguetteDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var baguetteOptions = builder.Configuration
    .GetSection(BaguetteOptions.SectionName)
    .Get<BaguetteOptions>() ?? new BaguetteOptions();

builder.Services.AddSingleton<IRoleRouter>(new RoleRouter(baguetteOptions.AdminUserId));

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
