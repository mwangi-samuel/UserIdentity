using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Settings;

namespace UserIdentity.Persistence;

internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var databaseSettings = configuration.GetRequiredSetting<DatabaseSettings>();

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppMysql(databaseSettings);

        return new AppDbContext(options.Options);
    }
}
