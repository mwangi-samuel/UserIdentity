using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class UpdatePasswordCommandHandlerTests
{
    private const string ResetRequestId = "SampleResetRequestId";
    private const string ResetRequestIdHash = "SampleResetRequestIdHash";
    private const string NewPassword = "NewP@ssw0rd";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler;
    private readonly FakeTimeProvider _timeProvider;

    private readonly DateTime _fixedNow = new(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
    private readonly IdentityUser _identityUser = new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = "test.user",
        Email = "test@ml.clm",
    };

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        AppSecretKey = "test-secret",
        BaseUrl = "https://app.test",
    };

    private readonly UserTokenViewModel _session = new()
    {
        UserToken = new UserTokenDto
        {
            AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 180 },
            RefreshToken = "refresh-token",
        },
    };

    public UpdatePasswordCommandHandlerTests()
    {
        _userManager = IdentityFakes.UserManager();
        _tokenFactory = A.Fake<ITokenFactory>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        _passwordResetRepository = A.Fake<IPasswordResetRepository>();
        _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
        _userRepository = A.Fake<IUserRepository>();
        _startSessionCommandHandler = A.Fake<ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>>();
        _timeProvider = new FakeTimeProvider();

        _timeProvider.SetUtcNow(_fixedNow);
        A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
        A.CallTo(() => _userManager.FindByIdAsync(_identityUser.Id)).Returns(_identityUser);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_identityUser.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(new UserDto { Id = _identityUser.Id });
        A.CallTo(() =>
                _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(_identityUser.Id, _identityUser.UserName!, _registeredApp.AppName),
                    _identityUser.Id,
                    A<CancellationToken>._
                )
            )
            .Returns(_session);
    }

    [Fact]
    public async Task UpdatePassword_When_No_Existing_Request_Returns_False()
    {
        // Arrange
        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns((PasswordResetEntity?)null);

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UpdatePasswordViewModel>(vm);
        Assert.False(vm.UpdatePasswordResult.PasswordUpdated);
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false, false, 60)]
    [InlineData(true, true, 60)]
    [InlineData(true, false, -1)]
    public async Task UpdatePassword_When_Request_Is_Not_Verified_Already_Applied_Or_Expired_Returns_False(
        bool verified,
        bool applied,
        int expiresInSeconds
    )
    {
        // Arrange
        SetUpPasswordResetEntity(verified, applied, _fixedNow.AddSeconds(expiresInSeconds));

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(vm.UpdatePasswordResult.PasswordUpdated);
        A.CallTo(() => _userManager.ResetPasswordAsync(A<IdentityUser>._, A<string>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdatePassword_When_No_Existing_User_Returns_False()
    {
        // Arrange
        var existingEntity = SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));

        A.CallTo(() => _userManager.FindByIdAsync(existingEntity.UserId)).Returns(default(IdentityUser));

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(vm.UpdatePasswordResult.PasswordUpdated);
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdatePassword_With_Password_Not_Meeting_Rules_Throws_InvalidDataException_And_Rolls_Back()
    {
        // Arrange
        var existingEntity = SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));

        A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(_identityUser)).Returns("identity-reset-token");
        A.CallTo(() => _userManager.ResetPasswordAsync(_identityUser, "identity-reset-token", NewPassword))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Passwords must have at least one digit ('0'-'9')." }));

        var handler = GetUpdatePasswordCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Passwords must have at least one digit ('0'-'9').", exception.Message);
        Assert.False(existingEntity.Applied);
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdatePassword_With_Verified_Request_Updates_Password_And_Ends_Sessions()
    {
        // Arrange
        var existingEntity = SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));

        A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(_identityUser)).Returns("identity-reset-token");
        A.CallTo(() => _userManager.ResetPasswordAsync(_identityUser, "identity-reset-token", NewPassword)).Returns(IdentityResult.Success);

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.UpdatePasswordResult.PasswordUpdated);
        Assert.True(existingEntity.Applied);
        Assert.Equal(_identityUser.Id, existingEntity.UpdatedBy);

        A.CallTo(() => _passwordResetRepository.UpdateEntityItem(existingEntity)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(_identityUser.Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdatePassword_Signs_In_A_Confirmed_User_Of_The_App_After_Ending_The_Old_Sessions()
    {
        // Arrange
        SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));
        SetUpSuccessfulReset();

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.UpdatePasswordResult.PasswordUpdated);
        Assert.Same(_session.UserToken, vm.UserToken);

        A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(_identityUser.Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(
                A.CallTo(() =>
                        _startSessionCommandHandler.CreateItemAsync(
                            new StartSessionCommand(_identityUser.Id, _identityUser.UserName!, _registeredApp.AppName),
                            _identityUser.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task UpdatePassword_Of_A_User_Of_Another_App_Updates_The_Password_Without_Signing_In()
    {
        // Arrange
        SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));
        SetUpSuccessfulReset();

        A.CallTo(() => _userRepository.GetUserDtoAsync(_identityUser.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(default(UserDto));

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.UpdatePasswordResult.PasswordUpdated);
        Assert.Null(vm.UserToken);
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdatePassword_Of_An_Unconfirmed_User_When_Confirmation_Is_Required_Updates_The_Password_Without_Signing_In()
    {
        // Arrange
        SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));
        SetUpSuccessfulReset();

        var identityOptions = new IdentityOptions();
        identityOptions.SignIn.RequireConfirmedAccount = true;

        var handler = GetUpdatePasswordCommandHandler(Options.Create(identityOptions));

        // Act
        var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.UpdatePasswordResult.PasswordUpdated);
        Assert.Null(vm.UserToken);
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdatePassword_Clears_The_Lockout_And_Failed_Attempts_In_The_Password_Update()
    {
        // Arrange
        SetUpPasswordResetEntity(true, false, _fixedNow.AddSeconds(60));

        _identityUser.LockoutEnd = _fixedNow.AddMinutes(5);
        _identityUser.AccessFailedCount = 3;

        DateTimeOffset? lockoutEndAtUpdate = _identityUser.LockoutEnd;
        var accessFailedCountAtUpdate = _identityUser.AccessFailedCount;

        A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(_identityUser)).Returns("identity-reset-token");
        A.CallTo(() => _userManager.ResetPasswordAsync(_identityUser, "identity-reset-token", NewPassword))
            .Invokes(
                (IdentityUser user, string _, string _) =>
                {
                    lockoutEndAtUpdate = user.LockoutEnd;
                    accessFailedCountAtUpdate = user.AccessFailedCount;
                }
            )
            .Returns(IdentityResult.Success);

        var handler = GetUpdatePasswordCommandHandler();

        // Act
        await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(lockoutEndAtUpdate);
        Assert.Equal(0, accessFailedCountAtUpdate);
        A.CallTo(() => _userManager.SetLockoutEndDateAsync(A<IdentityUser>._, A<DateTimeOffset?>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.UpdateAsync(A<IdentityUser>._)).MustNotHaveHappened();
    }

    private PasswordResetEntity SetUpPasswordResetEntity(bool verified, bool applied, DateTime expires)
    {
        var existingEntity = new PasswordResetEntity
        {
            Id = Guid.NewGuid(),
            RequestId = ResetRequestIdHash,
            UserId = _identityUser.Id,
            Verified = verified,
            Applied = applied,
            Expires = expires,
        };

        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>.That.Matches(e => e.RequestId == ResetRequestIdHash),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(existingEntity);

        return existingEntity;
    }

    private void SetUpSuccessfulReset()
    {
        A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(_identityUser)).Returns("identity-reset-token");
        A.CallTo(() => _userManager.ResetPasswordAsync(_identityUser, "identity-reset-token", NewPassword)).Returns(IdentityResult.Success);
    }

    private UpdatePasswordCommand GetUpdatePasswordCommand() =>
        new()
        {
            ResetRequestId = ResetRequestId,
            NewPassword = NewPassword,
            RegisteredApp = _registeredApp,
        };

    private UpdatePasswordCommandHandler GetUpdatePasswordCommandHandler(IOptions<IdentityOptions>? identityOptions = null) =>
        new(
            identityOptions ?? Options.Create(new IdentityOptions()),
            _userManager,
            _tokenFactory,
            _unitOfWork,
            _passwordResetRepository,
            _refreshTokenRepository,
            _userRepository,
            _startSessionCommandHandler,
            _timeProvider
        );
}
