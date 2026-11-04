using Microsoft.IdentityModel.Logging;

using PolyzenKit;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Middlewares;

using UserIdentity;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.Persistence;
using UserIdentity.Presentation.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);
var assembly = typeof(Program).Assembly;

builder.AddAppSerilog();

builder.Services.AddAppMysql<AppDbContext>(builder.Configuration);

builder.Services.AddAppIdentity<AppDbContext>(builder.Configuration);

builder.Services.AddAppRepositories(assembly);

builder.Services.AddAppCommandAndQueryHandlers(assembly);

builder.Services.AddAppEventHandlers(assembly);

builder.Services.AddAppEndpoints();

builder.Services.AddValidation();

builder.Services.AddOpenApi();

builder.Services.AddAppAuthentication(builder.Configuration);

builder.Services.AddAppAuthorization(builder.Configuration);

builder.Services.AddAppApiKeySettings(builder.Configuration);

builder.Services.AddAppRegisteredApps(builder.Configuration);

builder.Services.AddHttpContextAccessor();

builder.Services.AddAppExternalServices(builder.Configuration);

builder.Services.AddScoped<IAppCallbackService, AppCallbackService>();

builder.Services.AddGoogleRecaptcha(builder.Configuration);

builder.Services.AddAppRefreshTokenSettings(builder.Configuration);

builder.Services.AddAppPasswordResetSettings(builder.Configuration);

builder.Services.AddAppCleanups(builder.Configuration);

builder.Services.AddAppForwardedHeaders(builder.Configuration);

builder.Services.AddAppRateLimiting(builder.Configuration);

builder.Services.AddAppMessaging(builder.Configuration, assembly);

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  IdentityModelEventSource.ShowPII = true;
  IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}

app.UseMiddleware<ApiKeyMiddleware>();

app.UseMiddleware<RequestIdMiddleware>();

app.UseMiddleware<ExceptionMiddleware>();

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

app.MapHealthChecks("/health");

DbInitializer.InitializeDb(app);
app.AppSeedEntityNamesData();
app.AppSeedRegisteredAppsData(builder.Configuration);

app.Run();

public partial class Program { }
