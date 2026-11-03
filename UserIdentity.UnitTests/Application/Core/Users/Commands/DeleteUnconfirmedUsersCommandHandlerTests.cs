using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
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
  private readonly FakeTimeProvider _timeProvider = new();

  public DeleteUnconfirmedUsersCommandHandlerTests()
  {
    _timeProvider.SetUtcNow(_fixedNow);
  }

  private DeleteUnconfirmedUsersCommandHandler CreateHandler(bool requireConfirmedAccount = true)
  {
    var identityOptions = new IdentityOptions();
    identityOptions.SignIn.RequireConfirmedAccount = requireConfirmedAccount;

    return new(_userManager, Options.Create(identityOptions), Options.Create(new CleanupSettings { UnconfirmedUserLifetimeInDays = 30 }),
      _unitOfWork, _userRepository, _inviteCodeRepository, _timeProvider);
  }

  [Fact]
  public async Task DeleteItemAsync_Frees_The_Invite_Of_The_Users_App_And_Deletes_The_Identity_User()
  {
    // Arrange
    var userEntity = new UserEntity { Id = Guid.NewGuid().ToString() };
    var identityUser = new IdentityUser { Id = userEntity.Id, Email = "user@test.com" };
    var inviteCode = new InviteCodeEntity { UserEmail = identityUser.Email, InviteCode = "code", Applied = true };

    A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(_fixedNow.AddDays(-30), DeleteUnconfirmedUsersCommandHandler.BatchSize, A<CancellationToken>._))
      .Returns([userEntity]);
    A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);
    A.CallTo(() => _userManager.DeleteAsync(identityUser)).Returns(IdentityResult.Success);
    A.CallTo(() => _inviteCodeRepository.GetAppliedEntityItemsForUserAsync(userEntity.Id, A<CancellationToken>._)).Returns([inviteCode]);

    // Act
    await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    Assert.False(inviteCode.Applied);
    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCode)).MustHaveHappenedOnceExactly()
      .Then(A.CallTo(() => _userManager.DeleteAsync(identityUser)).MustHaveHappenedOnceExactly());
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
      .Returns([userEntity]);
    A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);
    A.CallTo(() => _userManager.DeleteAsync(identityUser)).Returns(IdentityResult.Failed(new IdentityError { Description = "nope" }));

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken));

    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task DeleteItemAsync_Keeps_Reading_Batches_Until_One_Is_Short()
  {
    // Arrange
    static List<UserEntity> Build(int count) => [.. Enumerable.Range(0, count).Select(_ => new UserEntity { Id = Guid.NewGuid().ToString() })];

    A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(A<DateTime>._, DeleteUnconfirmedUsersCommandHandler.BatchSize, A<CancellationToken>._))
      .ReturnsNextFromSequence(Build(DeleteUnconfirmedUsersCommandHandler.BatchSize), Build(1));
    A.CallTo(() => _userManager.FindByIdAsync(A<string>._)).ReturnsLazily((string id) => new IdentityUser { Id = id });
    A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).Returns(IdentityResult.Success);

    // Act
    await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).MustHaveHappened(DeleteUnconfirmedUsersCommandHandler.BatchSize + 1, Times.Exactly);
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappened(DeleteUnconfirmedUsersCommandHandler.BatchSize + 1, Times.Exactly);
  }
}
