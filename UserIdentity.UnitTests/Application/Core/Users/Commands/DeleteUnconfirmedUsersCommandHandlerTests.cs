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

        return new(
            _userManager,
            Options.Create(identityOptions),
            Options.Create(new CleanupSettings { IntervalInMinutes = 60, UnconfirmedUserLifetimeInDays = 30 }),
            _unitOfWork,
            _userRepository,
            _inviteCodeRepository,
            _timeProvider
        );
    }

    [Fact]
    public async Task DeleteItemAsync_Frees_The_Invite_Of_The_Users_App_And_Deletes_The_Identity_User()
    {
        // Arrange
        var userEntity = new UserEntity { Id = Guid.NewGuid().ToString() };
        var identityUser = new IdentityUser { Id = userEntity.Id, Email = "user@test.com" };
        var inviteCode = new InviteCodeEntity
        {
            UserEmail = identityUser.Email,
            InviteCode = "code",
            Applied = true,
        };

        A.CallTo(() =>
                _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(
                    _fixedNow.AddDays(-30),
                    CleanupBatch.Size,
                    A<CancellationToken>._
                )
            )
            .Returns([userEntity]);
        A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);
        A.CallTo(() => _userManager.DeleteAsync(identityUser)).Returns(IdentityResult.Success);
        A.CallTo(() => _inviteCodeRepository.GetAppliedEntityItemsForUserAsync(userEntity.Id, A<CancellationToken>._))
            .Returns([inviteCode]);

        // Act
        await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(inviteCode.Applied);
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCode))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _userManager.DeleteAsync(identityUser)).MustHaveHappenedOnceExactly());
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteItemAsync_DoesNothing_WhenConfirmationIsNotRequired()
    {
        // Act
        await CreateHandler(requireConfirmedAccount: false)
            .DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteItemAsync_Skips_A_User_Confirmed_After_The_Batch_Was_Read()
    {
        // Arrange
        var userEntity = new UserEntity { Id = Guid.NewGuid().ToString() };
        var identityUser = new IdentityUser { Id = userEntity.Id, EmailConfirmed = true };

        A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(A<DateTime>._, A<int>._, A<CancellationToken>._))
            .Returns([userEntity]);
        A.CallTo(() => _userManager.FindByIdAsync(userEntity.Id)).Returns(identityUser);

        // Act
        await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _inviteCodeRepository.GetAppliedEntityItemsForUserAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteItemAsync_Skips_A_User_Changed_During_Its_Delete_And_Deletes_The_Next_One()
    {
        // Arrange
        var changedUser = new UserEntity { Id = Guid.NewGuid().ToString() };
        var nextUser = new UserEntity { Id = Guid.NewGuid().ToString() };
        var changedIdentityUser = new IdentityUser { Id = changedUser.Id };
        var nextIdentityUser = new IdentityUser { Id = nextUser.Id };

        A.CallTo(() => _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(A<DateTime>._, A<int>._, A<CancellationToken>._))
            .Returns([changedUser, nextUser]);
        A.CallTo(() => _userManager.FindByIdAsync(changedUser.Id)).Returns(changedIdentityUser);
        A.CallTo(() => _userManager.FindByIdAsync(nextUser.Id)).Returns(nextIdentityUser);
        A.CallTo(() => _userManager.DeleteAsync(changedIdentityUser))
            .Returns(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        A.CallTo(() => _userManager.DeleteAsync(nextIdentityUser)).Returns(IdentityResult.Success);

        // Act
        await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.DeleteAsync(changedIdentityUser))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.RollbackTransactionAsync(CancellationToken.None)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userRepository.ClearTrackedEntities()).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.DeleteAsync(nextIdentityUser)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteItemAsync_Keeps_Reading_Batches_Until_One_Is_Short()
    {
        // Arrange
        static List<UserEntity> Build(int count) =>
            [.. Enumerable.Range(0, count).Select(_ => new UserEntity { Id = Guid.NewGuid().ToString() })];

        A.CallTo(() =>
                _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(A<DateTime>._, CleanupBatch.Size, A<CancellationToken>._)
            )
            .ReturnsNextFromSequence(Build(CleanupBatch.Size), Build(1));
        A.CallTo(() => _userManager.FindByIdAsync(A<string>._)).ReturnsLazily((string id) => new IdentityUser { Id = id });
        A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).Returns(IdentityResult.Success);

        // Act
        await CreateHandler().DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).MustHaveHappened(CleanupBatch.Size + 1, Times.Exactly);
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappened(CleanupBatch.Size + 1, Times.Exactly);
    }
}
