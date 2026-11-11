using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PolyzenKit.Presentation.Validation;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests;

public class ProgramTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
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
        Assert.Superset(new HashSet<string> { "user", "role", "invite-code", "wait-list" }, entityNames.ToHashSet());
    }

    [Fact]
    public async Task Create_Registered_App_Without_App_Name_Returns_Validation_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var requestPayload = new { AppSecretKey = "S3TAG00dSeC8E7-For-Local-Use-Only-012345", BaseUrl = "http://localhost:3001" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/registered-app", requestPayload);

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
}
