using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.JetStream;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests;

[Collection("Integration Tests")]
public class BaseIntegrationTests : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    private readonly TestingWebAppFactory _testingWebAppFactory;

    protected ITestOutputHelper OutputHelper { get; }
    protected HttpClient HttpClient { get; }
    protected IServiceProvider ServiceProvider { get; }

    protected TestDbHelper TestDbHelper { get; }
    protected NatsTestHelper NatsTestHelper { get; }

    protected RegisteredAppSeed RegisteredApp { get; }

    protected IConfiguration Configuration { get; }

    protected DateTime TestStartedAt { get; private set; }

    public BaseIntegrationTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    {
        _testingWebAppFactory = testingWebAppFactory;

        OutputHelper = outputHelper;
        HttpClient = testingWebAppFactory.CreateClient();

        _scope = testingWebAppFactory.Services.CreateScope();
        ServiceProvider = _scope.ServiceProvider;

        Configuration = ServiceProvider.GetRequiredService<IConfiguration>();

        TestDbHelper = new TestDbHelper(
            Configuration,
            ServiceProvider.GetRequiredService<AppDbContext>(),
            ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
            ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
        );

        NatsTestHelper = new NatsTestHelper(ServiceProvider.GetRequiredService<INatsJSContext>());

        RegisteredApp =
            Configuration.GetSetting<RegisteredAppsSettings>().RegisteredApps.FirstOrDefault()
            ?? throw new MissingConfigurationException("At least one registered app must be provided");
    }

    public async ValueTask InitializeAsync()
    {
        TestStartedAt = DateTime.UtcNow;

        await _testingWebAppFactory.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    }

    protected void AssertErrorTimestamp(JObject jsonObject)
    {
        var timestamp = (DateTime?)jsonObject["error"]?["timestamp"];

        Assert.NotNull(timestamp);
        Assert.InRange(timestamp.Value.ToUniversalTime(), TestStartedAt, DateTime.UtcNow);
    }

    public ValueTask DisposeAsync()
    {
        HttpClient.Dispose();
        _scope.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
