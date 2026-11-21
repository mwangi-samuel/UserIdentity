using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Npgsql;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.AppEntities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Infrastructure.Messaging.Shared;
using PolyzenKit.Infrastructure.Security.AppSecrets;
using PolyzenKit.Infrastructure.Security.KeySets;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;
using PolyzenKit.Presentation.Settings;
using Respawn;
using Testcontainers.Nats;
using Testcontainers.PostgreSql;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests;

public class TestingWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestAppName = "TestApp";
    public const string OtherAppName = "OtherApp";
    public const string DefaultRole = "user";

    public TestBackend TestBackend { get; } = new();

    public CreateRegisteredAppCommand TestApp { get; }

    public StubHttpServer StubHttpServer { get; } = new();

    public StubCaptchaVerifier CaptchaVerifier { get; } = new();

    public SqlCommandCounter SqlCommandCounter { get; } = new();

    private readonly PostgreSqlContainer _postgreSqlContainer;
    private readonly NatsContainer _natsContainer;

    private NpgsqlConnection? _respawnConnection;
    private Respawner? _respawner;

    private static readonly string _dbUserName = "test-user";
    private static readonly string _dbUserPassword = "test-user-db@user@Pass";
    private static readonly string _dbName = "useridentity-test-db";

    private const string NatsUser = "useridentity";
    private const string NatsPassword = "useridentity-local";
    private const ushort NatsPort = 4222;

    // Shared by hosts made with WithWebHostBuilder, which use the same database
    private readonly string _appSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public TestingWebAppFactory()
    {
        TestApp = new()
        {
            AppName = TestAppName,
            BaseUrl = "http://localhost:3000",
            Backends = [new RegisteredAppBackendInput { Name = TestBackend.Name, PublicKey = TestBackend.PublicKeyPem }],
        };

        _postgreSqlContainer = new PostgreSqlBuilder("postgres:18.7")
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
            _postgreSqlContainer.StartAsync(),
            _natsContainer.StartAsync(),
            StubHttpServer.StartAsync(TestContext.Current.CancellationToken)
        );

        await using var natsConnection = new NatsConnection(
            NatsOpts.Default with
            {
                Url = NatsUrl,
                AuthOpts = NatsAuthOpts.Default with { Username = NatsUser, Password = NatsPassword },
            }
        );

        await NatsTestHelper.CreateStreamAsync(new NatsJSContext(natsConnection), TestContext.Current.CancellationToken);

        await using var scope = Services.CreateAsyncScope();

        var createRegisteredAppCommandHandler = scope.ServiceProvider.GetRequiredService<
            ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel>
        >();

        CreateRegisteredAppCommand[] registeredApps = [TestApp, new() { AppName = OtherAppName, BaseUrl = "http://localhost:3001" }];

        foreach (var registeredApp in registeredApps)
            await createRegisteredAppCommandHandler.CreateItemAsync(
                registeredApp,
                KnownUsers.SystemId,
                TestContext.Current.CancellationToken
            );

        await ResetSignInSettingsAsync(TestContext.Current.CancellationToken);
    }

    public async Task<AppSignInSettingsDto> UpdateSignInSettingsAsync(
        string appName,
        UpdateSignInSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var scope = Services.CreateAsyncScope();

        command.RegisteredApp = (
            await scope
                .ServiceProvider.GetRequiredService<IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel>>()
                .GetItemAsync(new GetRegisteredAppQuery { AppName = appName }, cancellationToken)
        ).RegisteredApp;

        var vm = await scope
            .ServiceProvider.GetRequiredService<IUpdateItemCommandHandler<UpdateSignInSettingsCommand, SignInSettingsViewModel>>()
            .UpdateItemAsync(command, KnownUsers.SystemId, cancellationToken);

        return vm.SignInSettings;
    }

    public async Task ResetSignInSettingsAsync(CancellationToken cancellationToken)
    {
        foreach (var appName in new[] { TestAppName, OtherAppName })
            await UpdateSignInSettingsAsync(
                appName,
                new UpdateSignInSettingsCommand { RequireConfirmedAccount = true, DefaultRole = DefaultRole },
                cancellationToken
            );
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(SqlCommandCounter));
            services.AddSingleton<ICaptchaVerifier>(CaptchaVerifier);
        });

        builder.UseSetting(
            $"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.Port)}",
            _postgreSqlContainer.GetMappedPublicPort().ToString(CultureInfo.InvariantCulture)
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
        var privateKeyPassPhrase = "TestKeyPassPhrase-For-Local-Use-Only";
        builder.UseSetting("EDDSA_PRIVATE_KEY", EdDsaHelper.ConvertToPem(keyPair.Private, privateKeyPassPhrase));
        builder.UseSetting("EDDSA_PUBLIC_KEY", EdDsaHelper.ConvertToPem(keyPair.Public));
        builder.UseSetting($"{nameof(KeySetSettings)}:{nameof(KeySetSettings.PrivateKeyPassPhrase)}", privateKeyPassPhrase);

        // Override app secret encryption key - "key1" is the CurrentKeyId in appsettings.json
        builder.UseSetting($"{nameof(AppSecretSettings)}:{nameof(AppSecretSettings.Keys)}:key1", _appSecretKey);

        builder.UseSetting(
            $"{nameof(CodeCheckSettings)}:{nameof(CodeCheckSettings.VerifyUrl)}",
            StubHttpServer.Url + CodeCheckStub.VerifyPath
        );
        builder.UseSetting(
            $"{nameof(CodeCheckSettings)}:{nameof(CodeCheckSettings.ResendUrl)}",
            StubHttpServer.Url + CodeCheckStub.RetryPath
        );

        // Override Rate Limit Settings
        builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "false");

        TurnOffBootstrapAdmin(builder);
    }

    public static void TurnOffBootstrapAdmin(IWebHostBuilder builder)
    {
        string[] bootstrapAdminSettings =
        [
            nameof(BootstrapAdminSettings.AppName),
            nameof(BootstrapAdminSettings.AppBaseUrl),
            nameof(BootstrapAdminSettings.UserName),
            nameof(BootstrapAdminSettings.FirstName),
            nameof(BootstrapAdminSettings.LastName),
            nameof(BootstrapAdminSettings.Email),
            nameof(BootstrapAdminSettings.PhoneNumber),
            nameof(BootstrapAdminSettings.Password),
        ];

        foreach (var setting in bootstrapAdminSettings)
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{setting}", string.Empty);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_respawner is null || _respawnConnection is null)
        {
            using var scope = Services.CreateScope();
            var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _respawnConnection = new NpgsqlConnection(_postgreSqlContainer.GetConnectionString());
            await _respawnConnection.OpenAsync(cancellationToken);

            _respawner = await Respawner.CreateAsync(
                _respawnConnection,
                new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = ["public"],
                    TablesToIgnore =
                    [
                        appDbContext.GetTableName<AppEntity>()!,
                        appDbContext.GetTableName<RegisteredAppEntity>()!,
                        appDbContext.GetTableName<RegisteredAppBackendEntity>()!,
                        appDbContext.GetTableName<AppSignInSettingsEntity>()!,
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

        await _postgreSqlContainer.DisposeAsync();

        await StubHttpServer.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
