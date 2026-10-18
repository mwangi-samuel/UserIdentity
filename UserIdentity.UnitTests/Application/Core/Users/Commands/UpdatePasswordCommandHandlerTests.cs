using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
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
  private readonly IMachineDateTime _machineDateTime;

  private readonly DateTime _fixedNow = new(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
  private readonly IdentityUser _identityUser = new() { Id = Guid.NewGuid().ToString(), Email = "test@ml.clm" };

  public UpdatePasswordCommandHandlerTests()
  {
    _userManager = IdentityFakes.UserManager();
    _tokenFactory = A.Fake<ITokenFactory>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    _machineDateTime = A.Fake<IMachineDateTime>();

    A.CallTo(() => _machineDateTime.Now).Returns(_fixedNow);
    A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
    A.CallTo(() => _userManager.FindByIdAsync(_identityUser.Id)).Returns(_identityUser);
  }

  [Fact]
  public async Task UpdatePassword_When_No_Existing_Request_Returns_False()
  {
    // Arrange
    A.CallTo(() => _passwordResetRepository.GetEntityByAlternateIdAsync(A<PasswordResetEntity>._, QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._))
        .Returns((PasswordResetEntity?)null);

    var handler = GetUpdatePasswordCommandHandler();

    // Act
    var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UpdatePasswordViewModel>(vm);
    Assert.False(vm.UpdatePasswordResult.PassWordUpdated);
    A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(false, false, 60)]
  [InlineData(true, true, 60)]
  [InlineData(true, false, -1)]
  public async Task UpdatePassword_When_Request_Is_Not_Verified_Already_Applied_Or_Expired_Returns_False(bool verified, bool applied, int expiresInSeconds)
  {
    // Arrange
    SetUpPasswordResetEntity(verified, applied, _fixedNow.AddSeconds(expiresInSeconds));

    var handler = GetUpdatePasswordCommandHandler();

    // Act
    var vm = await handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(vm.UpdatePasswordResult.PassWordUpdated);
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
    Assert.False(vm.UpdatePasswordResult.PassWordUpdated);
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
    var exception = await Assert.ThrowsAsync<InvalidDataException>(() => handler.UpdateItemAsync(GetUpdatePasswordCommand(), TestStringHelper.UserId, TestContext.Current.CancellationToken));

    Assert.Equal("Passwords must have at least one digit ('0'-'9').", exception.Message);
    Assert.False(existingEntity.Applied);
    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItemsAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
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
    Assert.True(vm.UpdatePasswordResult.PassWordUpdated);
    Assert.True(existingEntity.Applied);
    Assert.Equal(_identityUser.Id, existingEntity.UpdatedBy);

    A.CallTo(() => _passwordResetRepository.UpdateEntityItem(existingEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItemsAsync(_identityUser.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.SetLockoutEndDateAsync(_identityUser, null)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.ResetAccessFailedCountAsync(_identityUser)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
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
      Expires = expires
    };

    A.CallTo(() => _passwordResetRepository.GetEntityByAlternateIdAsync(
        A<PasswordResetEntity>.That.Matches(e => e.RequestId == ResetRequestIdHash), QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._))
        .Returns(existingEntity);

    return existingEntity;
  }

  private static UpdatePasswordCommand GetUpdatePasswordCommand()
  {
    return new UpdatePasswordCommand
    {
      ResetRequestId = ResetRequestId,
      NewPassword = NewPassword
    };
  }

  private UpdatePasswordCommandHandler GetUpdatePasswordCommandHandler()
  {
    return new UpdatePasswordCommandHandler(
      _userManager,
      _tokenFactory,
      _unitOfWork,
      _passwordResetRepository,
      _refreshTokenRepository,
      _machineDateTime
    );
  }
}
