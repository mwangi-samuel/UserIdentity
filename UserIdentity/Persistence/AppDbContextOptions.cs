using Microsoft.EntityFrameworkCore;
using Npgsql;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;

namespace UserIdentity.Persistence;

public static class AppDbContextOptions
{
    public static DbContextOptionsBuilder UseAppPostgreSql(this DbContextOptionsBuilder options, DatabaseSettings databaseSettings) =>
        options
            .UseNpgsql(databaseSettings.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new UniqueViolationInterceptor(exception => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            );
}
