using System.Reflection;
using System.Text.Json.Serialization.Metadata;

using Microsoft.EntityFrameworkCore;

using PolyzenKit;
using PolyzenKit.Application.Core.Errors.Queries.GerError;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories.AppEntities;
using PolyzenKit.Persistence.Settings;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Settings;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;

namespace UserIdentity;

public static class Extensions
{
  // Add OpenAPI
  public static void AddAppOpenApi(this IServiceCollection services)
  {
    services.AddOpenApi();
    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.TypeInfoResolver =
        (options.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
        .WithAddedModifier(typeInfo =>
        {
          if (typeInfo.Type != typeof(UpdateRegisteredAppCommand))
            return;

          // The controller supplies this ID from the route, not the JSON body.
          // Its required, internal setter is incompatible with schema generation.
          var idProperty = typeInfo.Properties.FirstOrDefault(property =>
            property.AttributeProvider is PropertyInfo { Name: nameof(UpdateRegisteredAppCommand.Id) });

          if (idProperty is not null)
            typeInfo.Properties.Remove(idProperty);
        });
    });
  }

  // Add Mysql
  public static void AddAppMysql<TContext>(this IServiceCollection services, IConfiguration configuration) where TContext : DbContext
  {
    var mysqlSettings = configuration.GetSection(nameof(MysqlSettings)).Get<MysqlSettings>() ?? throw new MissingConfigurationException(nameof(MysqlSettings));

    services.AddDbContext<DbContext, TContext>(options =>
    {
      options.UseMySql(mysqlSettings.ConnectionString, ServerVersion.AutoDetect(mysqlSettings.ConnectionString)).UseSnakeCaseNamingConvention();
    });
  }

  // Add App Command and Query Handlers
  public static void AddAppCommandAndQueryHandlers(this IServiceCollection services)
  {
    services.AddAppCommandAndQueryHandlers(Assembly.GetExecutingAssembly());

    var polyzenKitAssembly = Assembly.GetAssembly(typeof(GetErrorQueryHandler))!;

    services.AddAppCommandAndQueryHandlers(polyzenKitAssembly);
  }

  // Add App Event Handlers
  public static void AddAppEventHandlers(this IServiceCollection services)
  {
    services.AddAppEventHandlers(Assembly.GetExecutingAssembly());
    var polyzenKitAssembly = Assembly.GetAssembly(typeof(GetErrorQueryHandler))!;
    services.AddAppEventHandlers(polyzenKitAssembly);
  }

  // Add Repositories
  public static void AddAppRepositories(this IServiceCollection services)
  {
    services.AddAppRepositories(Assembly.GetExecutingAssembly());

    var polyzenKitAssembly = Assembly.GetAssembly(typeof(AppEntityRepository))!;
    services.AddAppRepositories(polyzenKitAssembly);
  }

  // Add Google Recaptcha
  public static void AddGoogleRecaptcha(this IServiceCollection services, IConfiguration configuration)
  {
    var googleRecaptchaSettings = configuration.GetSection(nameof(GoogleRecaptchaSettings)).Get<GoogleRecaptchaSettings>() ?? throw new MissingConfigurationException(nameof(GoogleRecaptchaSettings));

    if (googleRecaptchaSettings.Enabled && googleRecaptchaSettings.SiteKey == null)
      throw new MissingConfigurationException(nameof(GoogleRecaptchaSettings.SiteKey));

    services.Configure<GoogleRecaptchaSettings>(options =>
    {
      options.Enabled = googleRecaptchaSettings.Enabled;
      options.SiteKey = googleRecaptchaSettings.SiteKey;
    });

    services.AddScoped<IGoogleRecaptchaService, GoogleRecaptchaService>();
  }

  // Add Refresh Token Settings
  public static void AddAppRefreshTokenSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var refreshTokenSettings = configuration.GetSection(nameof(RefreshTokenSettings)).Get<RefreshTokenSettings>() ?? throw new MissingConfigurationException(nameof(RefreshTokenSettings));

    if (refreshTokenSettings.ValidForSeconds <= 0)
      throw new MissingConfigurationException(nameof(RefreshTokenSettings.ValidForSeconds));

    services.Configure<RefreshTokenSettings>(options =>
    {
      options.ValidForSeconds = refreshTokenSettings.ValidForSeconds;
    });
  }

  // Add Password Reset Settings
  public static void AddAppPasswordResetSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var passwordResetSettings = configuration.GetSection(nameof(PasswordResetSettings)).Get<PasswordResetSettings>() ?? throw new MissingConfigurationException(nameof(PasswordResetSettings));

    if (passwordResetSettings.ValidForSeconds <= 0)
      throw new MissingConfigurationException(nameof(PasswordResetSettings.ValidForSeconds));

    if (passwordResetSettings.MaxRequestsPerDay <= 0)
      throw new MissingConfigurationException(nameof(PasswordResetSettings.MaxRequestsPerDay));

    services.Configure<PasswordResetSettings>(options =>
    {
      options.ValidForSeconds = passwordResetSettings.ValidForSeconds;
      options.RequestCooldownSeconds = passwordResetSettings.RequestCooldownSeconds;
      options.MaxRequestsPerDay = passwordResetSettings.MaxRequestsPerDay;
    });
  }

  // Add Cleanup Settings and the periodic cleanups
  public static void AddAppCleanups(this IServiceCollection services, IConfiguration configuration)
  {
    var cleanupSettings = configuration.GetSection(nameof(CleanupSettings)).Get<CleanupSettings>() ?? throw new MissingConfigurationException(nameof(CleanupSettings));

    if (cleanupSettings.IntervalInMinutes <= 0)
      throw new MissingConfigurationException(nameof(CleanupSettings.IntervalInMinutes));

    if (cleanupSettings.UnconfirmedUserLifetimeInDays <= 0)
      throw new MissingConfigurationException(nameof(CleanupSettings.UnconfirmedUserLifetimeInDays));

    services.Configure<CleanupSettings>(options =>
    {
      options.IntervalInMinutes = cleanupSettings.IntervalInMinutes;
      options.UnconfirmedUserLifetimeInDays = cleanupSettings.UnconfirmedUserLifetimeInDays;
    });

    var interval = TimeSpan.FromMinutes(cleanupSettings.IntervalInMinutes);

    services.AddAppPeriodicDeleteCommand<DeleteExpiredRefreshTokensCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteExpiredPasswordResetsCommand>(interval);
    services.AddAppPeriodicDeleteCommand<DeleteUnconfirmedUsersCommand>(interval);
  }

  public static void AddAppKafka(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddAppKafka(configuration, Assembly.GetExecutingAssembly());
  }


  // Seed Entity Names
  public static void AppSeedEntityNamesData(this IApplicationBuilder applicationBuilder)
  {
    Assembly.GetExecutingAssembly().AppSeedEntityNamesData(applicationBuilder);
  }
}
