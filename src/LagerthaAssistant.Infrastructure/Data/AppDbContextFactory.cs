namespace LagerthaAssistant.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using LagerthaAssistant.Infrastructure.Constants;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var uiPath = Path.Combine(basePath, "..", "LagerthaAssistant.UI");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(uiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(PersistenceConstants.ConnectionStringName)
            ?? "Host=localhost;Database=LagerthaAssistantDb;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}

