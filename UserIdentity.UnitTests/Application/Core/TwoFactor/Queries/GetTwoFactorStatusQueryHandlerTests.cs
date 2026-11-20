using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Queries;

public class GetTwoFactorStatusQueryHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1" };

    public GetTwoFactorStatusQueryHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.CountRecoveryCodesAsync(_user)).Returns(7);
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .Returns(
                new SignInSettingsViewModel
                {
                    SignInSettings = new AppSignInSettingsDto
                    {
                        AppId = 1,
                        RequireTwoFactorForAdmins = true,
                        AllowAuthenticatorTwoFactor = true,
                        AllowEmailTwoFactor = true,
                    },
                }
            );
    }

    [Fact]
    public async Task The_Status_Has_The_Method_The_Codes_Left_Whether_It_Is_Required_And_The_Allowed_Methods()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, TwoFactorMethod = TwoFactorMethod.Email });
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act
        var vm = await GetHandler()
            .GetItemAsync(new GetTwoFactorStatusQuery(_user.Id, _registeredApp), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Email, vm.Method);
        Assert.Equal(7, vm.RecoveryCodesLeft);
        Assert.True(vm.Required);
        Assert.Equal([TwoFactorMethod.Authenticator, TwoFactorMethod.Email], vm.AllowedMethods);
    }

    [Fact]
    public async Task A_Method_The_App_No_Longer_Allows_Shows_As_None()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, TwoFactorMethod = TwoFactorMethod.Sms });
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);

        // Act
        var vm = await GetHandler()
            .GetItemAsync(new GetTwoFactorStatusQuery(_user.Id, _registeredApp), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(vm.Method);
        Assert.Equal(0, vm.RecoveryCodesLeft);
        Assert.False(vm.Required);
    }

    [Fact]
    public async Task A_User_Of_Another_App_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._)).Returns((UserDto?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().GetItemAsync(new GetTwoFactorStatusQuery(_user.Id, _registeredApp), TestContext.Current.CancellationToken)
        );
    }

    private GetTwoFactorStatusQueryHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _userRepository,
            _getSignInSettingsQueryHandler
        );
}
