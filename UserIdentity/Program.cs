using Microsoft.IdentityModel.Logging;

using PolyzenKit;
using PolyzenKit.Infrastructure.Security.KeySets;
using PolyzenKit.Presentation.Middlewares;

using UserIdentity;
using UserIdentity.Application.Interfaces;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Migrations;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.AddAppSerilog();

// DB 
builder.Services.AddAppMysql<AppDbContext>(builder.Configuration);

// Identity
builder.Services.AddAppIdentity<AppDbContext>(builder.Configuration);

// Repositories
builder.Services.AddAppRepositories();

// Command and Query Handlers
builder.Services.AddAppCommandAndQueryHandlers();

// Event Handlers
builder.Services.AddAppEventHandlers();

// Controllers
builder.Services.AddAppControllers(builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Utilities - Time, String, Log
builder.Services.AddAppDateTimeHelpers();
builder.Services.AddAppStringHelpers();
builder.Services.AddAppLogHelpers();

// Authentication Identity
builder.Services.AddAppAuthentication(
    builder.Configuration,
    (options, keyProvider) => new EdDSAKeySetFactory(options, keyProvider)
);

// Authorization
builder.Services.AddAppAuthorization(builder.Configuration);

// Api Key Settings
builder.Services.AddAppApiKeySettings(builder.Configuration);

// Registered Apps - app secret encryption & app resolution
builder.Services.AddAppRegisteredApps(builder.Configuration);

// Health Checks
builder.Services.AddHealthChecks();

// Add Http Context Accessor
builder.Services.AddHttpContextAccessor();

// Add External Services & Google Recaptcha
builder.Services.AddAppExternalServices(builder.Configuration);
builder.Services.AddScoped<IAppCallbackService, AppCallbackService>();
builder.Services.AddGoogleRecaptcha(builder.Configuration);

// Kafka
builder.Services.AddAppKafka(builder.Configuration);

// Cors Policy
var corsPolicyName = builder.Services.AddAppCorsPolicy(builder.Configuration);

// build the app
var app = builder.Build();

// Use Cors
app.UseCors(corsPolicyName);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
  IdentityModelEventSource.ShowPII = true;
  IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}

// Use Api Key Middleware
app.UseMiddleware<ApiKeyMiddleware>();

// Use Request Id Middleware
app.UseMiddleware<RequestIdMiddleware>();

// Use Exception Middleware
app.UseMiddleware<ExceptionMiddleware>();

// Use HTTPS
app.UseHttpsRedirection();

// Use AuthN
app.UseAuthentication();

// Use AuthZ
app.UseAuthorization();

// Use Endpoints
app.MapControllers();

// Map Health Check Endpoint
app.MapHealthChecks("/health");

// Migrate And Seed DB
DbInitializer.InitializeDb(app);
app.AppSeedEntityNamesData();
app.AppSeedRegisteredAppsData(builder.Configuration);

// Run the app
app.Run();

// use this for testing 
public partial class Program { }
