using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using NATS.Client.Core;
using NATS.Client.JetStream;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.AppEntities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Infrastructure.ExternalServices;
using PolyzenKit.Infrastructure.Messaging.Shared;
using PolyzenKit.Infrastructure.Security.AppSecrets;
using PolyzenKit.Infrastructure.Security.KeySets;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;
using PolyzenKit.Presentation.Settings;
using Respawn;
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
    public const string CallbackAppSecretKey = "Ca11backApp5ecretKey-For-Tests-Only-0123";
    public const string CallbackPath = "/callback";

    public WireMockServer WireMockServer { get; private set; }

    public SqlCommandCounter SqlCommandCounter { get; } = new();

    private readonly MySqlContainer _mysqlContainer;
    private readonly NatsContainer _natsContainer;

    private MySqlConnection? _respawnConnection;
    private Respawner? _respawner;

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
        await Task.WhenAll(_mysqlContainer.StartAsync(), _natsContainer.StartAsync());

        // Without the streams, the app's consumers back off for up to 30 seconds before they attach
        await using var natsConnection = new NatsConnection(
            NatsOpts.Default with
            {
                Url = NatsUrl,
                AuthOpts = NatsAuthOpts.Default with { Username = NatsUser, Password = NatsPassword },
            }
        );

        await NatsTestHelper.CreateStreamsAsync(new NatsJSContext(natsConnection), TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(SqlCommandCounter))
        );

        // Override MYSQL Settings
        builder.UseSetting(
            $"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.Port)}",
            _mysqlContainer.GetMappedPublicPort().ToString(CultureInfo.InvariantCulture)
        );
        builder.UseSetting($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.UserName)}", _dbUserName);
        builder.UseSetting($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.Password)}", _dbUserPassword);
        builder.UseSetting($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.Database)}", _dbName);

        // Override Messaging Settings
        builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsUrl)}", NatsUrl);
        builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsUser)}", NatsUser);
        builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.NatsPassword)}", NatsPassword);
        builder.UseSetting($"{nameof(MessagingSettings)}:{nameof(MessagingSettings.RetryDelaysSeconds)}:0", "1");

        // Override pub/private key Settings
        var keyPair = EdDsaHelper.GenerateEd25519KeyPair();
        var privateKeyPassPhrase = RandomStrings.Generate(32, true, true);
        builder.UseSetting("EDDSA_PRIVATE_KEY", EdDsaHelper.ConvertToPem(keyPair.Private, privateKeyPassPhrase));
        builder.UseSetting("EDDSA_PUBLIC_KEY", EdDsaHelper.ConvertToPem(keyPair.Public));
        builder.UseSetting($"{nameof(KeySetSettings)}:{nameof(KeySetSettings.PrivateKeyPassPhrase)}", privateKeyPassPhrase);

        // Override app secret encryption key - "key1" is the CurrentKeyId in appsettings.json
        builder.UseSetting($"{nameof(AppSecretSettings)}:{nameof(AppSecretSettings.Keys)}:key1", _appSecretKey);

        // Override Google Recaptcha Settings
        builder.UseSetting($"{nameof(GoogleRecaptchaSettings)}:{nameof(GoogleRecaptchaSettings.Enabled)}", "true");
        builder.UseSetting(
            $"{nameof(ExternalServicesSettings)}:{nameof(ExternalServicesSettings.ExternalServices)}:GoogleRecaptcha:BaseUrl",
            WireMockServer.Urls[0]
        );
        builder.UseSetting(
            $"{nameof(ExternalServicesSettings)}:{nameof(ExternalServicesSettings.ExternalServices)}:GoogleRecaptcha:TimeoutSeconds",
            "60"
        );

        // Override Rate Limit Settings
        builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "false");

        // A second app, for cross-app checks
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:AppName", OtherAppName);
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:BaseUrl", "http://localhost:3001");
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:1:AppSecretKey", "0therApp5ecretKey-For-Tests-Only-0123456");

        // A third app, whose sign-ups through its UI are called back on WireMock
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:AppName", CallbackAppName);
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:BaseUrl", "http://localhost:3002");
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:AppSecretKey", CallbackAppSecretKey);
        builder.UseSetting("RegisteredAppsSettings:RegisteredApps:2:CallbackUrl", WireMockServer.Urls[0] + CallbackPath);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_respawner is null || _respawnConnection is null)
        {
            using var scope = Services.CreateScope();
            var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _respawnConnection = new MySqlConnection(_mysqlContainer.GetConnectionString());
            await _respawnConnection.OpenAsync(cancellationToken);

            _respawner = await Respawner.CreateAsync(
                _respawnConnection,
                new RespawnerOptions
                {
                    DbAdapter = DbAdapter.MySql,
                    TablesToIgnore =
                    [
                        appDbContext.GetTableName<AppEntity>()!,
                        appDbContext.GetTableName<RegisteredAppEntity>()!,
                        HistoryRepository.DefaultTableName,
                    ],
                }
            );
        }

        await _respawner.ResetAsync(_respawnConnection);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_respawnConnection is not null)
            await _respawnConnection.DisposeAsync();

        // Stop the app first, so its message handlers shut down before NATS goes away
        await base.DisposeAsync();

        await _natsContainer.DisposeAsync();

        await _mysqlContainer.DisposeAsync();

        WireMockServer.Stop();
        WireMockServer.Dispose();

        GC.SuppressFinalize(this);
    }
}
