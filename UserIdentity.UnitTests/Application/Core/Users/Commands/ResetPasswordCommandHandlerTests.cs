using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ResetPasswordCommandHandlerTests : IClassFixture<TestSettingsFixture>
{
  private readonly TestSettingsFixture _testSettings;

  private readonly UserManager<IdentityUser> _userManager;
  private readonly IMachineDateTime _machineDateTime;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IUserRepository _userRepository;
  private readonly IConfiguration _configuration;

  public ResetPasswordCommandHandlerTests(TestSettingsFixture testSettings)
  {
    _testSettings = testSettings;

    _userManager = IdentityFakes.UserManager();
    _machineDateTime = A.Fake<IMachineDateTime>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _userRepository = A.Fake<IUserRepository>();
    _configuration = _testSettings.Configuration;
  }

  [Fact]
  public async Task ResetPassword_When_No_Existing_User_Returns_Default_Message()
  {
    // Arrange
    ResetPasswordCommand command = new()
    {
      UserEmail = "test@kl.com"
    };

    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail)).Returns(default(IdentityUser));

    var handler = new ResetPasswordCommandHandler(_userManager, _machineDateTime, _unitOfWork, _userRepository, _configuration);

    // Act 
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId);

    // Assert
    Assert.IsType<ResetPasswordViewModel>(vm);
    Assert.NotNull(vm.ResetPasswordDetails);
    A.CallTo(() => _userRepository.GetEntityItemAsync(A<string>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_With_Valid_Details_Updates_User_Record()
  {
    // Arrange
    ResetPasswordCommand command = new()
    {
      UserEmail = "test@lp.com"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = Guid.NewGuid().ToString(),
      Email = command.UserEmail,
      UserName = command.UserEmail
    };

    var resetPassWordToken = "sampleresetPasswordToken";
    var fixedNow = new DateTime(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
    var existingEntity = new UserEntity { Id = existingIdentityUser.Id, FirstName = "Test", LastName = "User" };

    A.CallTo(() => _machineDateTime.Now).Returns(fixedNow);

    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail)).Returns(existingIdentityUser);
    A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(existingIdentityUser)).Returns(resetPassWordToken);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id)).Returns(existingEntity);

    var handler = new ResetPasswordCommandHandler(_userManager, _machineDateTime, _unitOfWork, _userRepository, _configuration);

    // Act 
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId);

    // Assert
    Assert.IsType<ResetPasswordViewModel>(vm);
    Assert.NotNull(vm.ResetPasswordDetails);
    A.CallTo(() => _userRepository.UpdateEntityItem(A<UserEntity>.That.Matches(e => e.ForgotPasswordToken == resetPassWordToken && e.UpdatedBy == TestStringHelper.UserId)))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }
}
