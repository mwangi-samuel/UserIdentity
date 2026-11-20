using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json.Linq;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests;

public class ProgramTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string RegisteredAppUri = "/api/v1/registered-app";
    private const string BootstrapAppName = "BootstrapApp";
    private const string BootstrapUserName = "bootstrap.admin";
    private const string BootstrapPassword = "B00tstr@p-Adm1n";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    [Fact]
    public async Task Health_Returns_Healthy()
    {
        // Act
        var response = await HttpClient.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Db_Context_Options_Are_Shared_By_Every_Request()
    {
        // Arrange
        using var firstScope = _testingWebAppFactory.Services.CreateScope();
        using var secondScope = _testingWebAppFactory.Services.CreateScope();

        // Act
        var firstOptions = firstScope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        var secondOptions = secondScope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();

        // Assert
        Assert.Same(firstOptions, secondOptions);
    }

    [Fact]
    public async Task Startup_Seeds_The_Entity_Names_Of_Every_Endpoint_Group()
    {
        // Arrange
        using var scope = _testingWebAppFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var entityNames = await context.AppEntity.Select(entity => entity.EntityName).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Superset(new HashSet<string> { "user", "role", "invite-code", "wait-list", "sign-in-settings" }, entityNames.ToHashSet());
    }

    [Fact]
    public async Task Create_Registered_App_Without_App_Name_Returns_Validation_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var requestPayload = new { BaseUrl = "http://localhost:3001" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, RegisteredAppUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.NotNull(errorList);
        var error = Assert.Single(errorList);
        Assert.Equal("AppName", error.Field);
        Assert.Equal("The AppName field is required.", error.Message);
    }

    [Fact]
    public async Task Startup_With_Bootstrap_Settings_Creates_A_Global_Admin_Who_Is_An_Admin_Of_The_App()
    {
        // Arrange
        await using var factory = CreateBootstrapFactory();
        using var httpClient = factory.CreateClient();

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiRequestHelper.loginUrl)
        {
            Content = SerDeHelper.ConvertToHttpContent(new { UserName = BootstrapUserName, Password = BootstrapPassword }),
        };
        httpRequest.AddXApiKey();
        httpRequest.Headers.Add("X-App-Name", BootstrapAppName);

        // Act
        var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = $"{jsonObject?["userToken"]?["accessToken"]?["token"]}";
        var roles = new JsonWebToken(token).Claims.Where(c => c.Type == JwtCustomClaimNames.Roles).Select(c => c.Value).ToList();

        Assert.Contains(KnownRoles.GlobalAdmin, roles);
        Assert.Contains("bootstrapapp:admin", roles);
        Assert.Equal("Session", $"{jsonObject?["nextStep"]}");

        var context = ServiceProvider.GetRequiredService<AppDbContext>();
        var bootstrapApp = await context
            .RegisteredApp.AsNoTracking()
            .SingleAsync(app => app.AppName == BootstrapAppName, TestContext.Current.CancellationToken);

        Assert.Null(bootstrapApp.AppSecretKey);
        Assert.False(
            await context.AppSignInSettings.AnyAsync(settings => settings.AppId == bootstrapApp.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Startup_With_Bootstrap_Settings_Creates_No_User_When_A_Global_Admin_Exists()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();
        var userCount = TestDbHelper.CountIdentityUsers();

        await using var factory = CreateBootstrapFactory();

        // Act
        using var httpClient = factory.CreateClient();

        // Assert
        Assert.Equal(userCount, TestDbHelper.CountIdentityUsers());
        Assert.False(
            await ServiceProvider
                .GetRequiredService<AppDbContext>()
                .Users.AnyAsync(user => user.UserName == BootstrapUserName, TestContext.Current.CancellationToken)
        );
    }

    private WebApplicationFactory<Program> CreateBootstrapFactory() =>
        _testingWebAppFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.AppName)}", BootstrapAppName);
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.AppBaseUrl)}", "http://localhost:3003");
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.UserName)}", BootstrapUserName);
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.FirstName)}", "Boot");
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.Email)}", "bootstrap.admin@example.com");
            builder.UseSetting($"{nameof(BootstrapAdminSettings)}:{nameof(BootstrapAdminSettings.Password)}", BootstrapPassword);
        });
}
