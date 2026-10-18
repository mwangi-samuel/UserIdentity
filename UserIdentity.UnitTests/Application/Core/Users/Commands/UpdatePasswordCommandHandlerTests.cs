using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class UpdatePasswordCommandHandlerTests
{

  private readonly UserManager<IdentityUser> _userManager;

  public UpdatePasswordCommandHandlerTests()
  {

    _userManager = IdentityFakes.UserManager();

  }

  [Fact]
  public async Task UpdatePassword_When_No_Existing_User_Returns_False()
  {
    // Arrange
    UpdatePasswordCommand command = new()
    {
      NewPassword = "test",
      UserId = "test",
      PasswordResetToken = "test"
    };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(default(IdentityUser));

    UpdatePasswordCommandHandler handler = new(_userManager);

    // Act 
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UpdatePasswordViewModel>(vm);
    Assert.NotNull(vm.UpdatePasswordResult);
    Assert.False(vm.UpdatePasswordResult.PassWordUpdated);
  }

  [Fact]
  public async Task UpdatePassword_With_Password_Reset_Failure_Returns_False()
  {
    // Arrange
    UpdatePasswordCommand command = new()
    {
      NewPassword = "test",
      UserId = "test",
      PasswordResetToken = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = command.UserId,
      Email = "test@ml.clm"
    };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userManager.ResetPasswordAsync(existingIdentityUser, command.PasswordResetToken, command.NewPassword)).Returns(IdentityResult.Failed([new IdentityError()]));

    UpdatePasswordCommandHandler handler = new(_userManager);

    // Act 
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UpdatePasswordViewModel>(vm);
    Assert.NotNull(vm.UpdatePasswordResult);
    Assert.False(vm.UpdatePasswordResult.PassWordUpdated);
  }

  [Fact]
  public async Task UpdatePassword_With_Valid_Details_Returns_True()
  {
    // Arrange
    UpdatePasswordCommand command = new()
    {
      NewPassword = "test",
      UserId = "test",
      PasswordResetToken = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = command.UserId,
      Email = "test@ml.clm"
    };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(existingIdentityUser);


    A.CallTo(() => _userManager.ResetPasswordAsync(existingIdentityUser, A<string>.Ignored, command.NewPassword)).Returns(IdentityResult.Success);

    UpdatePasswordCommandHandler handler = new(_userManager);

    // Act 
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UpdatePasswordViewModel>(vm);
    Assert.NotNull(vm.UpdatePasswordResult);
    Assert.True(vm.UpdatePasswordResult.PassWordUpdated);
  }
}
