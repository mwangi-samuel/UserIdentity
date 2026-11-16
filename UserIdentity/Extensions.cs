using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PolyzenKit;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Persistence;
using UserIdentity.Presentation.Filters;

namespace UserIdentity;

public static class Extensions
{
    public static void AddAppPostgreSql<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        var databaseSettings = configuration.GetRequiredSetting<DatabaseSettings>();

        services.AddDbContextPool<DbContext, TContext>(options => options.UseAppPostgreSql(databaseSettings));
        services.AddHealthChecks().AddDbContextCheck<TContext>("postgresql");
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

    public static async Task AppBootstrapAdminAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICreateItemCommandHandler<BootstrapAdminCommand, BootstrappedAdminViewModel>
        >();

        await handler.CreateItemAsync(new BootstrapAdminCommand(), KnownUsers.SystemId, cancellationToken);
    }
}
