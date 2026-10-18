using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.WebUtilities;

using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ConfirmUpdatePasswordTokenCommandHandlerTests
{
  private readonly IUserRepository _userRepository;

  public ConfirmUpdatePasswordTokenCommandHandlerTests()
  {
    _userRepository = A.Fake<IUserRepository>();
  }

  [Fact]
  public async Task Confirm_UpdatePassword_Token_With_Bad_Token_Returns_False()
  {
    // Arrange
    ConfirmUpdatePasswordTokenCommand command = new()
    {
      ConfirmPasswordToken = "test/!!",
      UserId = "test"
    };


    ConfirmUpdatePasswordTokenCommandHandler handler = new(_userRepository);

    // Act
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ConfirmUpdatePasswordTokenViewModel>(vm);
    Assert.NotNull(vm.TokenPasswordResult);
    Assert.False(vm.TokenPasswordResult.UpdatePasswordTokenConfirmed);

  }

  [Fact]
  public async Task Confirm_UpdatePassword_Token_With_Non_Existing_Token_Returns_False()
  {
    // Arrange
    var rawToken = "test123+*";

    ConfirmUpdatePasswordTokenCommand command = new()
    {
      ConfirmPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken)),
      UserId = "test"
    };

    A.CallTo(() => _userRepository.GetEntityByAlternateIdAsync(
        A<UserEntity>.That.Matches(e => e.Id == command.UserId && e.ForgotPasswordToken == rawToken),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns((UserEntity?)null);

    ConfirmUpdatePasswordTokenCommandHandler handler = new(_userRepository);

    // Act
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ConfirmUpdatePasswordTokenViewModel>(vm);
    Assert.NotNull(vm.TokenPasswordResult);
    Assert.False(vm.TokenPasswordResult.UpdatePasswordTokenConfirmed);
  }

  [Fact]
  public async Task Confirm_UpdatePassword_Token_With_Query_Token_Exception_Returns_False()
  {
    // Arrange
    var rawToken = "test123+*";

    ConfirmUpdatePasswordTokenCommand command = new()
    {
      ConfirmPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken)),
      UserId = "test"
    };

    A.CallTo(() => _userRepository.GetEntityByAlternateIdAsync(A<UserEntity>._, QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Throws(new System.Exception());

    ConfirmUpdatePasswordTokenCommandHandler handler = new(_userRepository);

    // Act
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ConfirmUpdatePasswordTokenViewModel>(vm);
    Assert.NotNull(vm.TokenPasswordResult);
    Assert.False(vm.TokenPasswordResult.UpdatePasswordTokenConfirmed);
  }

  [Fact]
  public async Task Confirm_UpdatePassword_Token_With_Existing_Token_Returns_True()
  {
    // Arrange
    var rawToken = "test123+*";

    ConfirmUpdatePasswordTokenCommand command = new()
    {
      ConfirmPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken)),
      UserId = "test"
    };

    A.CallTo(() => _userRepository.GetEntityByAlternateIdAsync(
        A<UserEntity>.That.Matches(e => e.Id == command.UserId && e.ForgotPasswordToken == rawToken),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns(new UserEntity { Id = command.UserId, ForgotPasswordToken = rawToken });

    ConfirmUpdatePasswordTokenCommandHandler handler = new(_userRepository);

    // Act
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ConfirmUpdatePasswordTokenViewModel>(vm);
    Assert.NotNull(vm.TokenPasswordResult);
    Assert.True(vm.TokenPasswordResult.UpdatePasswordTokenConfirmed);
  }
}
