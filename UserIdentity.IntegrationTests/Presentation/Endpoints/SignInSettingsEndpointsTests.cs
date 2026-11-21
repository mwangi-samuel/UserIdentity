using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class SignInSettingsEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/sign-in-settings";

    [Fact]
    public async Task Global_Admin_Reads_The_Sign_In_Settings_Of_The_App_Named_In_The_Header()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, BaseUri);

        // Assert
        var settings = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["signInSettings"];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id, settings?["appId"]?.Value<int>());
        Assert.Equal("True", $"{settings?["requireConfirmedAccount"]}");
        Assert.Equal("False", $"{settings?["requireInviteCode"]}");
        Assert.Equal(TestingWebAppFactory.DefaultRole, $"{settings?["defaultRole"]}");
        Assert.Equal("False", $"{settings?["requireTwoFactor"]}");
        Assert.Equal("False", $"{settings?["allowAuthenticatorTwoFactor"]}");
    }

    [Fact]
    public async Task Sign_In_Settings_Of_An_App_Without_A_Row_Have_Every_Switch_Off_And_No_Default_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        const string appName = "NoSettingsApp";

        await ServiceProvider
            .GetRequiredService<ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel>>()
            .CreateItemAsync(
                new CreateRegisteredAppCommand { AppName = appName, BaseUrl = "http://localhost:3005" },
                KnownUsers.SystemId,
                TestContext.Current.CancellationToken
            );

        var httpRequest = await HttpClient.CreateAuthorizedHttpRequestMessageAsync(HttpMethod.Get, BaseUri);
        httpRequest.Headers.Remove("X-App-Name");
        httpRequest.Headers.Add("X-App-Name", appName);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var settings = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["signInSettings"];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestDbHelper.GetRegisteredAppEntityByAppName(appName).Id, settings?["appId"]?.Value<int>());
        Assert.Equal("False", $"{settings?["requireConfirmedAccount"]}");
        Assert.Equal("False", $"{settings?["requireTwoFactorForAdmins"]}");
        Assert.Null(settings?["defaultRole"]);
    }

    [Fact]
    public async Task Update_Replaces_Every_Setting_And_Stores_The_Default_Role_In_Lower_Case()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Put,
            BaseUri,
            new
            {
                RequireConfirmedAccount = false,
                RequireInviteCode = true,
                DefaultRole = "Member",
                RequireTwoFactorForAdmins = true,
                AllowAuthenticatorTwoFactor = true,
            }
        );

        // Assert
        var settings = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["signInSettings"];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("False", $"{settings?["requireConfirmedAccount"]}");
        Assert.Equal("True", $"{settings?["requireInviteCode"]}");
        Assert.Equal("member", $"{settings?["defaultRole"]}");
        Assert.Equal("False", $"{settings?["requireTwoFactor"]}");
        Assert.Equal("True", $"{settings?["requireTwoFactorForAdmins"]}");
        Assert.Equal("True", $"{settings?["allowAuthenticatorTwoFactor"]}");
        Assert.Equal("False", $"{settings?["allowEmailTwoFactor"]}");
        Assert.Equal("False", $"{settings?["allowSmsTwoFactor"]}");
    }

    [Fact]
    public async Task Update_With_An_Admin_Default_Role_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, BaseUri, new { DefaultRole = "Admin" });

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.SignInSettings.AdminDefaultRole("Admin"), $"{jsonObject?["error"]?["message"]}");
    }

    [Theory]
    [InlineData("a role")]
    [InlineData("testapp:member")]
    public async Task Update_With_A_Default_Role_That_Isnt_A_Bare_Role_Name_Returns_A_Validation_Error(string defaultRole)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, BaseUri, new { DefaultRole = defaultRole });

        // Assert
        var errorList = SerDeHelper
            .Deserialize<JObject>(await response.ValidateRequestResponseAsync())
            ?["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            errorList ?? [],
            error =>
                error.Field == "DefaultRole"
                && error.Message == "The DefaultRole field must be a role name without the app prefix, spaces or colons."
        );
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Update_Requiring_Two_Step_Sign_In_With_No_Method_Allowed_Returns_A_Validation_Error(
        bool requireTwoFactor,
        bool requireTwoFactorForAdmins
    )
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Put,
            BaseUri,
            new { RequireTwoFactor = requireTwoFactor, RequireTwoFactorForAdmins = requireTwoFactorForAdmins }
        );

        // Assert
        var errorList = SerDeHelper
            .Deserialize<JObject>(await response.ValidateRequestResponseAsync())
            ?["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorList ?? [], error => error.Message == ErrorMessages.SignInSettings.TwoFactorWithoutMethod);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task App_Admins_Cant_Read_Or_Change_Sign_In_Settings(string method)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(new HttpMethod(method), BaseUri, new { RequireConfirmedAccount = false });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sign_Up_Follows_The_Updated_Settings()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Put,
            BaseUri,
            new { RequireConfirmedAccount = false, DefaultRole = "member" }
        );

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            "/api/v1/user/register",
            new
            {
                UserSettingHelper.FirstName,
                UserName = "open.sign.up",
                UserEmail = "open.sign.up@example.com",
                UserSettingHelper.UserPassword,
            }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());
        var userId = $"{jsonObject?["user"]?["id"]}";

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Session", $"{jsonObject?["nextStep"]}");
        Assert.False(string.IsNullOrEmpty($"{jsonObject?["userToken"]?["accessToken"]?["token"]}"));
        Assert.NotNull(TestDbHelper.GetIdentityUserRole("testapp:member", userId));
        Assert.Null(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userId, TimeSpan.FromSeconds(1)));
    }
}
