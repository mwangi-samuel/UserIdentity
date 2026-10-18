using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using PolyzenKit;
using PolyzenKit.Application.Core.Errors.Queries.GerError;
using PolyzenKit.Application.Core.Errors.ViewModels;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories.AppEntities;
using PolyzenKit.Persistence.Settings;

using UserIdentity.Application.Core.Tokens.Settings;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.Presentation.Settings;

namespace UserIdentity;

public static class Extensions
{
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

    services.Configure<PasswordResetSettings>(options =>
    {
      options.ValidForSeconds = passwordResetSettings.ValidForSeconds;
      options.RequestCooldownSeconds = passwordResetSettings.RequestCooldownSeconds;
    });
  }

  // Add Forwarded Headers
  public static void AddAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
  {
    var forwardedHeadersSettings = configuration.GetSection(nameof(ForwardedHeadersSettings)).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

    var knownNetworks = (forwardedHeadersSettings.KnownNetworks ?? string.Empty)
      .Split(ZenConstants.LIST_CONFIG_SEPARATOR, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(System.Net.IPNetwork.Parse)
      .ToList();

    services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

      foreach (var knownNetwork in knownNetworks)
        options.KnownIPNetworks.Add(knownNetwork);
    });
  }

  // Add Rate Limiting - per client IP and endpoint
  public static void AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
  {
    var rateLimitSettings = configuration.GetSection(nameof(RateLimitSettings)).Get<RateLimitSettings>() ?? throw new MissingConfigurationException(nameof(RateLimitSettings));

    if (rateLimitSettings.Enabled && (rateLimitSettings.PermitLimit <= 0 || rateLimitSettings.WindowSeconds <= 0))
      throw new MissingConfigurationException(nameof(RateLimitSettings));

    services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      options.OnRejected = WriteRateLimitRejectionAsync;

      options.AddPolicy(RateLimitSettings.ClientIpPolicy, httpContext =>
      {
        if (!rateLimitSettings.Enabled)
          return RateLimitPartition.GetNoLimiter(string.Empty);

        var partitionKey = $"{httpContext.Connection.RemoteIpAddress}-{httpContext.GetEndpoint()?.DisplayName}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = rateLimitSettings.PermitLimit,
          Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds)
        });
      });
    });
  }

  private static async ValueTask WriteRateLimitRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
  {
    var httpContext = context.HttpContext;

    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
      httpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    var getErrorQueryHandler = httpContext.RequestServices.GetRequiredService<IGetItemQueryHandler<GetErrorQuery, ErrorViewModel>>();

    var errorViewModel = await getErrorQueryHandler.GetItemAsync(new GetErrorQuery
    {
      ErrorMessage = "Too many requests, try again later",
      StatusMessage = $"{StatusCodes.Status429TooManyRequests} -{HttpStatusCode.TooManyRequests}"
    }, cancellationToken);

    httpContext.Response.ContentType = ZenConstants.APPLICATION_JSON;

    await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorViewModel, JsonUtil.GetJsonSerializerOptions()), cancellationToken);
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
