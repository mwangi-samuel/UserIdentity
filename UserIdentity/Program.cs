using Microsoft.IdentityModel.Logging;
using PolyzenKit;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Middlewares;
using UserIdentity;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.Persistence;
using UserIdentity.Presentation.Endpoints;

// No request needs more than a few KB, and at Kestrel's default of 30 MB one request can use up the service's memory
const long MaxRequestBodyBytes = 64 * 1024;

var builder = WebApplication.CreateSlimBuilder(args);
var assembly = typeof(Program).Assembly;

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

builder.AddAppSerilog();

builder.Services.AddAppMysql<AppDbContext>(builder.Configuration);

builder.Services.AddAppIdentity<AppDbContext>(builder.Configuration);

builder.Services.AddAppRepositories(assembly);

builder.Services.AddAppCommandAndQueryHandlers(assembly);

builder.Services.AddAppEventHandlers(assembly);

builder.Services.AddAppEndpoints();

builder.Services.AddAppExceptionHandler();

builder.Services.AddValidation();

builder.Services.AddOpenApi();

builder.Services.AddAppAuthentication(builder.Configuration);

builder.Services.AddAppAuthorization(builder.Configuration);

builder.Services.AddAppApiKeySettings(builder.Configuration);

builder.Services.AddAppRegisteredApps(builder.Configuration);

builder.Services.AddAppExternalServices(builder.Configuration);

builder.Services.AddScoped<IAppCallbackService, AppCallbackService>();

builder.Services.AddGoogleRecaptcha(builder.Configuration);

builder.Services.AddAppRefreshTokenSettings(builder.Configuration);

builder.Services.AddAppPasswordResetSettings(builder.Configuration);

builder.Services.AddAppCleanups(builder.Configuration);

builder.Services.AddAppForwardedHeaders(builder.Configuration);

builder.Services.AddAppRateLimiting(builder.Configuration);

builder.Services.AddAppPasswordHashingLimit(builder.Configuration);

builder.Services.AddAppMessaging(builder.Configuration, assembly);

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    IdentityModelEventSource.ShowPII = true;
    IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}

app.UseMiddleware<ApiKeyMiddleware>();

app.UseMiddleware<RequestIdMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapInviteCodeEndpoints();
app.MapWaitListEndpoints();
app.MapJwksEndpoints();
app.MapAppEntityEndpoints();
app.MapRegisteredAppEndpoints();

app.MapHealthChecks("/health").AllowAnonymous();

DbInitializer.InitializeDb(app);
await app.AppSeedEntityNamesDataAsync();
await app.AppSeedRegisteredAppsDataAsync(builder.Configuration);

app.Run();

public partial class Program { }
