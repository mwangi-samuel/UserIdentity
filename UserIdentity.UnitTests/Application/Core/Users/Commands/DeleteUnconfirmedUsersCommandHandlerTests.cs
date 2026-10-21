using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class DeleteUnconfirmedUsersCommandHandlerTests
{
  private readonly DateTime _fixedNow = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
  private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
  private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
  private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
  private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
  private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
  private readonly IPasswordResetRepository _passwordResetRepository = A.Fake<IPasswordResetRepository>();
  private readonly IMachineDateTime _machineDateTime = A.Fake<IMachineDateTime>();

  public DeleteUnconfirmedUsersCommandHandlerTests()
  {
    A.CallTo(() => _machineDateTime.Now).Returns(_fixedNow);
  }

  private DeleteUnconfirmedUsersCommandHandler CreateHandler(bool requireConfirmedAccount = true)
  {
    var identityOptions = new IdentityOptions();
    identityOptions.SignIn.RequireConfirmedAccount = requireConfirmedAccount;

    return new(_userManager, Options.Create(identityOptions), Options.Create(new CleanupSettings { UnconfirmedUserLifetimeInDays = 30 }),
      _unitOfWork, _userRepository, _inviteCodeRepository, _refreshTokenRepository, _passwordResetRepository, _machineDateTime);
  }

  [Fact]
  public async Task DeleteItemAsync_DeletesTheUser_AndReopensTheirInviteCode()
  {
    // Arrange
    var userEntity = new UserEntity { Id = Guid.NewGuid().ToString() };
    var identityUser = new IdentityUser { Id = userEntity.Id, Email = "user@test.com" };
    var inviteCode = new InviteCodeEntity { UserEmail = identityUser.Email, InviteCode = "code", Applied = true };

    A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(_fixedNow.AddDays(-30), DeleteUnconfirmedUsersCommandHandler.BatchSize, A<CancellationToken>._))
      .Returns(new List<UserEntity> { userEntity });
    A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);
    A.CallTo(() => _userManager.DeleteAsync(identityUser)).Returns(IdentityResult.Success);
    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>.That.Matches(e => e.UserEmail == identityUser.Email), QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._))
      .Returns(inviteCode);

    // Act
    await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    Assert.False(inviteCode.Applied);
    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCode)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItemsAsync(userEntity.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _passwordResetRepository.RemoveEntityItemsAsync(userEntity.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userRepository.RemoveEntityItem(userEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.DeleteAsync(identityUser)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task DeleteItemAsync_DoesNothing_WhenConfirmationIsNotRequired()
  {
    // Act
    await CreateHandler(requireConfirmedAccount: false).DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(_userRepository).MustNotHaveHappened();
    A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task DeleteItemAsync_RollsBack_WhenTheIdentityUserCannotBeDeleted()
  {
    // Arrange
    var userEntity = new UserEntity { Id = Guid.NewGuid().ToString() };
    var identityUser = new IdentityUser { Id = userEntity.Id };

    A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(A<DateTime>._, A<int>._, A<CancellationToken>._))
      .Returns(new List<UserEntity> { userEntity });
    A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);
    A.CallTo(() => _userManager.DeleteAsync(identityUser)).Returns(IdentityResult.Failed(new IdentityError { Description = "nope" }));

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken));

    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }
}
