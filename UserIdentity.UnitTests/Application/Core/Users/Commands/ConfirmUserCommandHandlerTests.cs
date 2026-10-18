using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
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
    var command = new ConfirmUserCommand { UserId = "missing" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(default(IdentityUser));

    var handler = new ConfirmUserCommandHandler(_userManager);

    await Assert.ThrowsAsync<NoRecordException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UpdateItemAsync_WhenUpdateFails_ThrowsRecordUpdateException()
  {
    var command = new ConfirmUserCommand { UserId = "user-id" };
    var existingUser = new IdentityUser { Id = command.UserId, EmailConfirmed = false };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingUser);
    A.CallTo(() => _userManager.UpdateAsync(existingUser)).Returns(IdentityResult.Failed(new IdentityError { Description = "Failed" }));

    var handler = new ConfirmUserCommandHandler(_userManager);

    await Assert.ThrowsAsync<RecordUpdateException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UpdateItemAsync_WhenUpdateSucceeds_ReturnsConfirmedResult()
  {
    var command = new ConfirmUserCommand { UserId = "user-id" };
    var existingUser = new IdentityUser { Id = command.UserId, EmailConfirmed = false };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingUser);
    A.CallTo(() => _userManager.UpdateAsync(existingUser)).Returns(IdentityResult.Success);

    var handler = new ConfirmUserCommandHandler(_userManager);

    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    Assert.IsType<ConfirmUserViewModel>(vm);
    Assert.True(vm.ConfirmUserResult.UserConfirmed);
    Assert.True(existingUser.EmailConfirmed);
    A.CallTo(() => _userManager.UpdateAsync(existingUser)).MustHaveHappenedOnceExactly();
  }
}
