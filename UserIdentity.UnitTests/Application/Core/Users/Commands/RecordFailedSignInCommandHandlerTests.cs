using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Common.Enums;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class RecordFailedSignInCommandHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = A.Fake<ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel>>();

    private readonly IdentityUser _user = new() { Id = "user-1", UserName = "test" };

    [Fact]
    public async Task A_Failed_Password_Counts_Toward_Lockout_Only()
    {
        // Arrange
        A.CallTo(() => _userManager.AccessFailedAsync(_user)).Returns(IdentityResult.Success);

        // Act
        await GetHandler().CreateItemAsync(new RecordFailedSignInCommand(_user, false), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.AccessFailedAsync(_user)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    A<CreateAccountLimitEventCommand>._,
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Failed_Second_Step_Counts_Toward_Lockout_And_The_Daily_Cap()
    {
        // Arrange
        A.CallTo(() => _userManager.AccessFailedAsync(_user)).Returns(IdentityResult.Success);

        // Act
        await GetHandler().CreateItemAsync(new RecordFailedSignInCommand(_user, true), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.AccessFailedAsync(_user)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    new CreateAccountLimitEventCommand(_user.Id, AccountLimitKind.FailedSecondStep),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task A_Failure_Saved_First_By_Another_Request_Is_Counted_On_The_Reloaded_User()
    {
        // Arrange
        var reloadedUser = new IdentityUser { Id = _user.Id };

        A.CallTo(() => _userManager.AccessFailedAsync(_user))
            .Returns(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(reloadedUser);
        A.CallTo(() => _userManager.AccessFailedAsync(reloadedUser)).Returns(IdentityResult.Success);

        // Act
        await GetHandler().CreateItemAsync(new RecordFailedSignInCommand(_user, false), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.AccessFailedAsync(_user))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _userRepository.ClearTrackedEntities()).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.AccessFailedAsync(reloadedUser)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task A_Failure_That_Cannot_Be_Counted_Throws_InvalidOperationException()
    {
        // Arrange
        A.CallTo(() => _userManager.AccessFailedAsync(_user))
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Other", Description = "nope" }));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GetHandler().CreateItemAsync(new RecordFailedSignInCommand(_user, true), _user.Id, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userRepository.ClearTrackedEntities()).MustNotHaveHappened();
    }

    private RecordFailedSignInCommandHandler GetHandler() =>
        new(_userManager, _unitOfWork, _userRepository, _createAccountLimitEventCommandHandler);
}
