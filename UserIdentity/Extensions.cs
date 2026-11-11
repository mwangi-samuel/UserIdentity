using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PolyzenKit;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence;
using UserIdentity.Presentation.Filters;

namespace UserIdentity;

public static class Extensions
{
    public static void AddAppMysql<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        var databaseSettings = configuration.GetRequiredSetting<DatabaseSettings>();

        services.AddDbContextPool<DbContext, TContext>(options => options.UseAppMysql(databaseSettings));
        services.AddHealthChecks().AddDbContextCheck<TContext>("mysql");
    }

    public static void AddAppPasswordHashingLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var passwordHashingSettings = configuration.GetRequiredSetting<PasswordHashingSettings>();

        services.AddSingleton(_ => new ConcurrencyLimiter(
            new ConcurrencyLimiterOptions
            {
                PermitLimit = passwordHashingSettings.MaxConcurrent,
                QueueLimit = passwordHashingSettings.QueueLimit,
            }
        ));
    }

    public static void AddAppCleanups(this IServiceCollection services, IConfiguration configuration)
    {
        var cleanupSettings = services.AddAppSetting<CleanupSettings>(configuration);

        var interval = TimeSpan.FromMinutes(cleanupSettings.IntervalInMinutes);

        services.AddAppPeriodicDeleteCommand<DeleteExpiredRefreshTokensCommand>(interval);
        services.AddAppPeriodicDeleteCommand<DeleteExpiredLoginTicketsCommand>(interval);
        services.AddAppPeriodicDeleteCommand<DeleteExpiredPasswordResetsCommand>(interval);
        services.AddAppPeriodicDeleteCommand<DeleteUnconfirmedUsersCommand>(interval);
    }
}
