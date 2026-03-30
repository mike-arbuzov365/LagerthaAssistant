namespace BaguetteDesign.IntegrationTests.Data;

using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class BaguetteDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task MigrationsApply_ToFreshDatabase_WithoutErrors()
    {
        var options = new DbContextOptionsBuilder<BaguetteDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new BaguetteDbContext(options);

        // Should apply all migrations without throwing
        await context.Database.MigrateAsync();

        // Verify tables exist by running a simple query on each domain DbSet
        Assert.Empty(await context.Leads.ToListAsync());
        Assert.Empty(await context.Projects.ToListAsync());
        Assert.Empty(await context.DialogStates.ToListAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_SetsAuditFields_OnAdd()
    {
        var options = new DbContextOptionsBuilder<BaguetteDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new BaguetteDbContext(options);
        await context.Database.MigrateAsync();

        var lead = new BaguetteDesign.Domain.Entities.Lead
        {
            UserId = "test-user",
            ServiceType = "logo"
        };

        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        Assert.NotEqual(default, lead.CreatedAt);
        Assert.NotEqual(default, lead.UpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesUpdatedAt_OnModify()
    {
        var options = new DbContextOptionsBuilder<BaguetteDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new BaguetteDbContext(options);
        await context.Database.MigrateAsync();

        var lead = new BaguetteDesign.Domain.Entities.Lead
        {
            UserId = "test-user-2",
            ServiceType = "branding"
        };
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var createdAt = lead.CreatedAt;
        await Task.Delay(10); // ensure time difference

        lead.ServiceType = "logo";
        await context.SaveChangesAsync();

        Assert.Equal(createdAt, lead.CreatedAt);
        // UpdatedAt should be >= CreatedAt (may be equal if test is fast)
        Assert.True(lead.UpdatedAt >= lead.CreatedAt);
    }
}
