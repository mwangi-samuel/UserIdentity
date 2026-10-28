using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using PolyzenKit.Infrastructure.Kafka;
using PolyzenKit.Infrastructure.Security.AppSecrets;
using PolyzenKit.Infrastructure.Security.KeySets;
using PolyzenKit.Persistence.Settings;
using PolyzenKit.Presentation.Settings;

using Testcontainers.MySql;
using Testcontainers.Redpanda;

using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.IntegrationTests.TestUtils;

using WireMock.Server;

using Xunit;

namespace UserIdentity.IntegrationTests;

public class TestingWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  public const string OtherAppName = "OtherApp";

  public WireMockServer WireMockServer { get; private set; }

  private readonly MySqlContainer _mysqlContainer;
  private readonly RedpandaContainer _redpandaContainer;

  private static readonly string _dbUserName = "test-user";
  private static readonly string _dbUserPassword = "test-user-db@user@Pass";
  private static readonly string _dbName = "useridentity-test-db";

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

    _redpandaContainer = new RedpandaBuilder("redpandadata/redpanda:v25.3.3")
        .Build();
  }

  public async ValueTask InitializeAsync()
  {
    await Task.WhenAll(
      _mysqlContainer.StartAsync(),
      _redpandaContainer.StartAsync()
    );
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureAppConfiguration((context, config) =>
    {

    });

    // Override MYSQL Settings
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Port)}", _mysqlContainer.GetMappedPublicPort().ToString());
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.UserName)}", _dbUserName);
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Password)}", _dbUserPassword);
    builder.UseSetting($"{nameof(MysqlSettings)}:{nameof(MysqlSettings.Database)}", _dbName);

    // Override Kafka Settings
    builder.UseSetting($"{nameof(KafkaSettings)}:{nameof(KafkaSettings.BootstrapServers)}", _redpandaContainer.GetBootstrapAddress());
    builder.UseSetting($"{nameof(KafkaSettings)}:{nameof(KafkaSettings.SchemaRegistryUrl)}", _redpandaContainer.GetSchemaRegistryAddress());

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

  }

  public override async ValueTask DisposeAsync()
  {
    // Stop the app first, so its Kafka consumers shut down before the broker goes away
    await base.DisposeAsync();

    await _redpandaContainer.DisposeAsync();

    await _mysqlContainer.DisposeAsync();

    WireMockServer.Stop();
    WireMockServer.Dispose();

    GC.SuppressFinalize(this);
  }
}
