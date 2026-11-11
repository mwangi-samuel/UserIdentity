using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ConfirmUserCommandHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager;

    public ConfirmUserCommandHandlerTests()
    {
        _userManager = IdentityFakes.UserManager();
    }

    [Fact]
    public async Task UpdateItemAsync_WhenUserNotFound_ThrowsNoRecordException()
    {
        // Arrange
        var command = new ConfirmUserCommand { UserId = "missing" };

        A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(default(IdentityUser));

        var handler = new ConfirmUserCommandHandler(_userManager);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateItemAsync_WhenUpdateFails_ThrowsRecordUpdateException()
    {
        // Arrange
        var command = new ConfirmUserCommand { UserId = "user-id" };
        var existingUser = new IdentityUser { Id = command.UserId, EmailConfirmed = false };

        A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingUser);
        A.CallTo(() => _userManager.UpdateAsync(existingUser)).Returns(IdentityResult.Failed(new IdentityError { Description = "Failed" }));

        var handler = new ConfirmUserCommandHandler(_userManager);

        // Act & Assert
        await Assert.ThrowsAsync<RecordUpdateException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateItemAsync_WhenUpdateSucceeds_ConfirmsTheEmail()
    {
        // Arrange
        var command = new ConfirmUserCommand { UserId = "user-id" };
        var existingUser = new IdentityUser { Id = command.UserId, EmailConfirmed = false };

        A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingUser);
        A.CallTo(() => _userManager.UpdateAsync(existingUser)).Returns(IdentityResult.Success);

        var handler = new ConfirmUserCommandHandler(_userManager);

        // Act
        await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(existingUser.EmailConfirmed);
        A.CallTo(() => _userManager.UpdateAsync(existingUser)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateItemAsync_WhenUserIsAlreadyConfirmed_KeepsTheUserConfirmed()
    {
        // Arrange
        var command = new ConfirmUserCommand { UserId = "user-id" };
        var existingUser = new IdentityUser { Id = command.UserId, EmailConfirmed = true };

        A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingUser);
        A.CallTo(() => _userManager.UpdateAsync(existingUser)).Returns(IdentityResult.Success);

        var handler = new ConfirmUserCommandHandler(_userManager);

        // Act
        await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(existingUser.EmailConfirmed);
    }
}
