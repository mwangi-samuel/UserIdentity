using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PolyzenKit;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Settings;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.Persistence;

namespace UserIdentity;

public static class Extensions
{
  public static void AddAppMysql<TContext>(
    this IServiceCollection services,
    IConfiguration configuration) where TContext : DbContext
  {
    var mysqlSettings = configuration.GetRequiredSetting<MysqlSettings>();

    services.AddDbContext<DbContext, TContext>(
      options => options.UseAppMysql(mysqlSettings),
      optionsLifetime: ServiceLifetime.Singleton);
    services.AddHealthChecks().AddDbContextCheck<TContext>("mysql");
  }

  public static void AddGoogleRecaptcha(this IServiceCollection services, IConfiguration configuration)
  {
    var googleRecaptchaSettings = configuration.GetRequiredSetting<GoogleRecaptchaSettings>();

    services.AddSingleton(Options.Create(googleRecaptchaSettings));
    services.AddScoped<IGoogleRecaptchaService, GoogleRecaptchaService>();
  }

  public static void AddAppRefreshTokenSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var refreshTokenSettings = configuration.GetRequiredSetting<RefreshTokenSettings>();

    services.AddSingleton(Options.Create(refreshTokenSettings));
  }

  public static void AddAppPasswordResetSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var passwordResetSettings = configuration.GetRequiredSetting<PasswordResetSettings>();

    services.AddSingleton(Options.Create(passwordResetSettings));
  }

  public static void AddAppCleanups(this IServiceCollection services, IConfiguration configuration)
  {
    var cleanupSettings = configuration.GetRequiredSetting<CleanupSettings>();

    services.AddSingleton(Options.Create(cleanupSettings));

    var interval = TimeSpan.FromMinutes(cleanupSettings.IntervalInMinutes);

    services.AddAppPeriodicDeleteCommand<DeleteExpiredRefreshTokensCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteExpiredPasswordResetsCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteUnconfirmedUsersCommand>(interval);
  }
}
