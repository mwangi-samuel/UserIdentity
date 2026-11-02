using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NATS.Client.Core;
using NATS.Client.JetStream;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using PolyzenKit.Infrastructure.Messaging.Shared;
using PolyzenKit.Infrastructure.Security.AppSecrets;
using PolyzenKit.Infrastructure.Security.KeySets;
using PolyzenKit.Persistence.Settings;
using PolyzenKit.Presentation.Settings;

using Testcontainers.MySql;
using Testcontainers.Nats;

using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;

using WireMock.Server;

using Xunit;

namespace UserIdentity.IntegrationTests;

public class TestingWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  public const string OtherAppName = "OtherApp";
  public const string CallbackAppName = "CallbackApp";
  public const string CallbackAppSecretKey = "Ca11backApp5ecretKey";
  public const string CallbackPath = "/callback";

  public WireMockServer WireMockServer { get; private set; }

  public SqlCommandCounter SqlCommandCounter { get; } = new();

  private readonly MySqlContainer _mysqlContainer;
  private readonly NatsContainer _natsContainer;

  private static readonly string _dbUserName = "test-user";
  private static readonly string _dbUserPassword = "test-user-db@user@Pass";
  private static readonly string _dbName = "useridentity-test-db";

  private const string NatsUser = "useridentity";
  private const string NatsPassword = "useridentity-test";
  private const ushort NatsPort = 4222;

  // Shared by hosts made with WithWebHostBuilder, which use the same database
  private readonly string _appSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

  public TestingWebAppFactory()
  {

    WireMockServer = WireMockServer.Start();

    _mysqlContainer = new MySqlBuilder("mysql:8.4.0")
        .WithUsername(_dbUserName)
        .WithPassword(_dbUserPassword)
        .WithDatabase(_dbName)
        .Build();

    _natsContainer = new NatsBuilder("nats:2.15.2-alpine")
        .WithUsername(NatsUser)
        .WithPassword(NatsPassword)
        .WithCommand("--jetstream")
        .Build();
  }

  private string NatsUrl => $"nats://{_natsContainer.Hostname}:{_natsContainer.GetMappedPublicPort(NatsPort)}";

  public async ValueTask InitializeAsync()
  {
    await Task.WhenAll(
      _mysqlContainer.StartAsync(),
      _natsContainer.StartAsync()
    );

    // Without the streams, the app's consumers back off for up to 30 seconds before they attach
    await using var natsConnection = new NatsConnection(NatsOpts.Default with
    {
      Url = NatsUrl,
      AuthOpts = NatsAuthOpts.Default with { Username = NatsUser, Password = NatsPassword }
    });

    await NatsTestHelper.CreateStreamsAsync(new NatsJSContext(natsConnection), TestContext.Current.CancellationToken);
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureTestServices(services =>
      services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(SqlCommandCounter)));

    // Override MYSQL Settings
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Port)}", _mysqlContainer.GetMappedPublicPort().ToString());
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.UserName)}", _dbUserName);
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Password)}", _dbUserPassword);
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Database)}", _dbName);

    // Override Messaging Settings
    builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsUrl)}", NatsUrl);
    builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsUser)}", NatsUser);
    builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsPassword)}", NatsPassword);
    builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.RetryDelaysSeconds)}:0", "1");

    // Override pub/private key Settings 
    var keyPair = EdDsaHelper.GenerateEd25519KeyPair();
    var privateKeyPassPhrase = StringUtil.GenerateRandomString(32, true, true);
    builder.UseSetting("EDDSA_PRIVATE_KEY", EdDsaHelper.ConvertToPem(keyPair.Private, privateKeyPassPhrase));
    builder.UseSetting("EDDSA_PUBLIC_KEY", EdDsaHelper.ConvertToPem(keyPair.Public));
    builder.UseSetting($"{nameof(KeySetOptions)}:{nameof(KeySetOptions.PrivateKeyPassPhrase)}", privateKeyPassPhrase);

    // Override app secret encryption key - "key1" is the CurrentKeyId in appsettings.json
    builder.UseSetting($"{nameof(AppSecretOptions)}:{nameof(AppSecretOptions.Keys)}:key1", _appSecretKey);

    // Override Google Recaptcha Settings
    builder.UseSetting($"{nameof(GoogleRecaptchaSettings)}:{nameof(GoogleRecaptchaSettings.Enabled)}", "true");
    builder.UseSetting($"{nameof(ExternalServicesSettings)}:{nameof(ExternalServicesSettings.ExternalServices)}:GoogleRecaptcha:BaseUrl", WireMockServer.Urls[0]);

    // Override Rate Limit Settings
    builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "false");

    // A second app, for cross-app checks
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:AppName", OtherAppName);
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:BaseUrl", "http://localhost:3001");
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:AppSecretKey", "0therApp5ecretKey");

    // A third app, whose sign-ups through its UI are called back on WireMock
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:AppName", CallbackAppName);
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:BaseUrl", "http://localhost:3002");
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:AppSecretKey", CallbackAppSecretKey);
    builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:CallbackUrl", WireMockServer.Urls[0] + CallbackPath);

  }

  public override async ValueTask DisposeAsync()
  {
    // Stop the app first, so its message handlers shut down before NATS goes away
    await base.DisposeAsync();

    await _natsContainer.DisposeAsync();

    await _mysqlContainer.DisposeAsync();

    WireMockServer.Stop();
    WireMockServer.Dispose();

    GC.SuppressFinalize(this);
  }
}
