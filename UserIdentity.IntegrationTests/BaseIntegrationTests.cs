using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.JetStream;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.AppEntities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Infrastructure;
using Respawn;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests;

[Collection("Integration Tests")]
public class BaseIntegrationTests : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    private readonly Func<Task> _resetDatabase;

    protected ITestOutputHelper OutputHelper { get; }
    protected HttpClient HttpClient { get; }
    protected IServiceProvider ServiceProvider { get; }

    private readonly AppDbContext _appDbContext;

    protected TestDbHelper TestDbHelper { get; }
    protected NatsTestHelper NatsTestHelper { get; }

    protected RegisteredAppSeed RegisteredApp { get; }

    protected IConfiguration Configuration { get; }

    public BaseIntegrationTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
        HttpClient = testingWebAppFactory.CreateClient();

        _scope = testingWebAppFactory.Services.CreateScope();
        ServiceProvider = _scope.ServiceProvider;

        _appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();

        Configuration = ServiceProvider.GetRequiredService<IConfiguration>();

        TestDbHelper = new TestDbHelper(
            Configuration,
            _appDbContext,
            ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
            ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
        );

        NatsTestHelper = new NatsTestHelper(ServiceProvider.GetRequiredService<INatsJSContext>());

        RegisteredApp =
            Configuration.GetSetting<RegisteredAppsSettings>().RegisteredApps.FirstOrDefault()
            ?? throw new MissingConfigurationException("At least one registered app must be provided");

        _resetDatabase = ResolveResetDatabaseFunc();
    }

    private Func<Task> ResolveResetDatabaseFunc()
    {
        return async () =>
        {
            var dbConnection = _appDbContext.Database.GetDbConnection();

            await dbConnection.OpenAsync();

            var respawner = await Respawner.CreateAsync(
                dbConnection,
                new RespawnerOptions
                {
                    DbAdapter = DbAdapter.MySql,
                    TablesToIgnore =
                    [
                        _appDbContext.GetTableName<AppEntity>()!,
                        _appDbContext.GetTableName<RegisteredAppEntity>()!,
                        _appDbContext.GetTableName<IdentityRole>()!,
                        HistoryRepository.DefaultTableName,
                    ],
                }
            );

            await respawner.ResetAsync(dbConnection);
        };
    }

    public async ValueTask InitializeAsync() => await _resetDatabase();

    public ValueTask DisposeAsync()
    {
        HttpClient.Dispose();
        _scope.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
