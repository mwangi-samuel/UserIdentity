using FakeItEasy;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Persistence.Repositories.AppSignInSettings;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.SignInSettings.Queries;

public class GetSignInSettingsQueryHandlerTests
{
    private readonly IAppSignInSettingsRepository _repository = A.Fake<IAppSignInSettingsRepository>();

    [Fact]
    public async Task An_App_Without_Settings_Has_Every_Switch_Off_And_No_Default_Role()
    {
        // Arrange
        A.CallTo(() => _repository.GetEntityDtoAsync(7, A<CancellationToken>._)).Returns((AppSignInSettingsDto?)null);

        // Act
        var vm = await new GetSignInSettingsQueryHandler(_repository).GetItemAsync(
            new GetSignInSettingsQuery(7),
            TestContext.Current.CancellationToken
        );

        // Assert
        var settings = vm.SignInSettings;
        Assert.Equal(7, settings.AppId);
        Assert.False(settings.RequireConfirmedAccount);
        Assert.False(settings.RequireInviteCode);
        Assert.Null(settings.DefaultRole);
        Assert.False(settings.RequireTwoFactor);
        Assert.False(settings.RequireTwoFactorForAdmins);
        Assert.False(settings.AllowAuthenticatorTwoFactor);
        Assert.False(settings.AllowEmailTwoFactor);
        Assert.False(settings.AllowSmsTwoFactor);
    }

    [Fact]
    public async Task An_App_With_Settings_Gets_Them()
    {
        // Arrange
        var stored = new AppSignInSettingsDto
        {
            AppId = 7,
            RequireConfirmedAccount = true,
            DefaultRole = "user",
        };
        A.CallTo(() => _repository.GetEntityDtoAsync(7, A<CancellationToken>._)).Returns(stored);

        // Act
        var vm = await new GetSignInSettingsQueryHandler(_repository).GetItemAsync(
            new GetSignInSettingsQuery(7),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(stored, vm.SignInSettings);
    }
}
