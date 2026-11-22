using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Queries;

public class GetStepUpUserQueryHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler = A.Fake<
        IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel>
    >();
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "admin-id" };
    private readonly UserDto _userDetails = new() { Id = "admin-id", TwoFactorMethod = TwoFactorMethod.Email };

    private readonly AppSignInSettingsDto _signInSettings = new()
    {
        AppId = 1,
        AllowAuthenticatorTwoFactor = true,
        AllowEmailTwoFactor = true,
        AllowSmsTwoFactor = true,
    };

    public GetStepUpUserQueryHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(false);
        A.CallTo(() => _getAccountLimitQueryHandler.GetItemAsync(A<GetAccountLimitQuery>._, A<CancellationToken>._))
            .Returns(new AccountLimitViewModel { IsReached = false });
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(1), A<CancellationToken>._))
            .ReturnsLazily(() => new SignInSettingsViewModel { SignInSettings = _signInSettings });
    }

    [Theory]
    [InlineData(TwoFactorMethod.Authenticator)]
    [InlineData(TwoFactorMethod.Email)]
    [InlineData(TwoFactorMethod.Sms)]
    public async Task Gives_The_Admin_And_Their_Method(TwoFactorMethod method)
    {
        // Arrange
        _userDetails.TwoFactorMethod = method;

        // Act
        var vm = await GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(_user, vm.User);
        Assert.Same(_userDetails, vm.UserDetails);
        Assert.Equal(method, vm.Method);
    }

    [Fact]
    public async Task A_Locked_Out_Admin_Is_Refused()
    {
        // Arrange
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountLocked, exception.Message);
    }

    [Fact]
    public async Task An_Admin_Past_The_Daily_Failed_Second_Step_Cap_Is_Refused()
    {
        // Arrange
        A.CallTo(() =>
                _getAccountLimitQueryHandler.GetItemAsync(
                    new GetAccountLimitQuery(_user.Id, AccountLimitKind.FailedSecondStep),
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = true });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountLocked, exception.Message);
    }

    [Fact]
    public async Task An_Admin_Without_A_Method_Is_Asked_To_Set_One_Up()
    {
        // Arrange
        _userDetails.TwoFactorMethod = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.SetUpToConfirm, exception.Message);
    }

    [Fact]
    public async Task A_Method_The_App_No_Longer_Allows_Counts_As_None()
    {
        // Arrange
        _signInSettings.AllowEmailTwoFactor = false;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.SetUpToConfirm, exception.Message);
    }

    [Fact]
    public async Task An_Account_Gone_Since_Sign_In_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns((IdentityUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() => GetHandler().GetItemAsync(GetQuery(), TestContext.Current.CancellationToken));
    }

    private GetStepUpUserQuery GetQuery() => new(_user.Id, _registeredApp);

    private GetStepUpUserQueryHandler GetHandler() =>
        new(_userManager, _userRepository, _getAccountLimitQueryHandler, _getSignInSettingsQueryHandler);
}
