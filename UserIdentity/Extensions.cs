using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MySqlConnector;

using PolyzenKit;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;

namespace UserIdentity;

public static class Extensions
{
  public static void AddAppMysql<TContext>(this IServiceCollection services, IConfiguration configuration) where TContext : DbContext
  {
    var mysqlSettings = configuration.GetRequiredSetting<MysqlSettings>();

    services.AddDbContext<DbContext, TContext>(options =>
    {
      options.UseMySql(mysqlSettings.ConnectionString, ServerVersion.AutoDetect(mysqlSettings.ConnectionString)).UseSnakeCaseNamingConvention()
        .AddInterceptors(new UniqueViolationInterceptor(exception => exception is MySqlException { Number: 1062 }));
    });
  }

  public static void AddGoogleRecaptcha(this IServiceCollection services, IConfiguration configuration)
  {
    var googleRecaptchaSettings = configuration.GetRequiredSetting<GoogleRecaptchaSettings>();

    if (googleRecaptchaSettings.Enabled && googleRecaptchaSettings.SiteKey == null)
      throw new MissingConfigurationException(nameof(GoogleRecaptchaSettings.SiteKey));

    services.AddSingleton(Options.Create(googleRecaptchaSettings));
    services.AddScoped<IGoogleRecaptchaService, GoogleRecaptchaService>();
  }

  public static void AddAppRefreshTokenSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var refreshTokenSettings = configuration.GetRequiredSetting<RefreshTokenSettings>();

    if (refreshTokenSettings.ValidForSeconds <= 0)
      throw new MissingConfigurationException(nameof(RefreshTokenSettings.ValidForSeconds));

    services.AddSingleton(Options.Create(refreshTokenSettings));
  }

  public static void AddAppPasswordResetSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var passwordResetSettings = configuration.GetRequiredSetting<PasswordResetSettings>();

    if (passwordResetSettings.ValidForSeconds <= 0)
      throw new MissingConfigurationException(nameof(PasswordResetSettings.ValidForSeconds));

    if (passwordResetSettings.MaxRequestsPerDay <= 0)
      throw new MissingConfigurationException(nameof(PasswordResetSettings.MaxRequestsPerDay));

    services.AddSingleton(Options.Create(passwordResetSettings));
  }

  public static void AddAppCleanups(this IServiceCollection services, IConfiguration configuration)
  {
    var cleanupSettings = configuration.GetRequiredSetting<CleanupSettings>();

    if (cleanupSettings.IntervalInMinutes <= 0)
      throw new MissingConfigurationException(nameof(CleanupSettings.IntervalInMinutes));

    if (cleanupSettings.UnconfirmedUserLifetimeInDays <= 0)
      throw new MissingConfigurationException(nameof(CleanupSettings.UnconfirmedUserLifetimeInDays));

    services.AddSingleton(Options.Create(cleanupSettings));

    var interval = TimeSpan.FromMinutes(cleanupSettings.IntervalInMinutes);

    services.AddAppPeriodicDeleteCommand<DeleteExpiredRefreshTokensCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteExpiredPasswordResetsCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteUnconfirmedUsersCommand>(interval);
  }
}
