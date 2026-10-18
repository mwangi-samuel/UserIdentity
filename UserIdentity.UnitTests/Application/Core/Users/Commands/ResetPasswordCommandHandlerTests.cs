using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ResetPasswordCommandHandlerTests : IClassFixture<TestSettingsFixture>
{
  private const string ResetRequestId = "SampleResetRequestId";
  private const string ResetRequestIdHash = "SampleResetRequestIdHash";

  private readonly TestSettingsFixture _testSettings;

  private readonly IOptions<PasswordResetSettings> _passwordResetSettings;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IMachineDateTime _machineDateTime;
  private readonly ITokenFactory _tokenFactory;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IUserRepository _userRepository;
  private readonly IPasswordResetRepository _passwordResetRepository;
  private readonly IEventHandler<PasswordResetEvent> _passwordResetEventHandler;
  private readonly IConfiguration _configuration;
  private readonly ILogHelper<ResetPasswordCommandHandler> _logHelper;

  private readonly DateTime _fixedNow = new(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
  private readonly RegisteredAppEntity _registeredApp = new() { Id = 1, AppName = "TestApp" };

  public ResetPasswordCommandHandlerTests(TestSettingsFixture testSettings)
  {
    _testSettings = testSettings;

    _passwordResetSettings = Options.Create(new PasswordResetSettings { ValidForSeconds = 900, RequestCooldownSeconds = 60 });
    _userManager = IdentityFakes.UserManager();
    _machineDateTime = A.Fake<IMachineDateTime>();
    _tokenFactory = A.Fake<ITokenFactory>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _userRepository = A.Fake<IUserRepository>();
    _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    _passwordResetEventHandler = A.Fake<IEventHandler<PasswordResetEvent>>();
    _configuration = _testSettings.Configuration;
    _logHelper = A.Fake<ILogHelper<ResetPasswordCommandHandler>>();

    A.CallTo(() => _machineDateTime.Now).Returns(_fixedNow);
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(ResetRequestId);
    A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
  }

  [Fact]
  public async Task ResetPassword_When_No_Existing_User_Returns_Request_Id_Without_Storing_It()
  {
    // Arrange
    var command = GetResetPasswordCommand("unknown.user");

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(default(IdentityUser));

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ResetPasswordViewModel>(vm);
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    Assert.NotNull(vm.ResetPasswordDetails.EmailMessage);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _passwordResetEventHandler.HandleEventAsync(A<PasswordResetEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_With_Valid_Details_Stores_Request_And_Publishes_Event()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.Email!);
    var existingEntity = new UserEntity { Id = existingIdentityUser.Id, FirstName = "Test", LastName = "User" };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns((PasswordResetEntity?)null);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns(existingEntity);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);

    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>.That.Matches(e =>
        e.RequestId == ResetRequestIdHash && e.UserId == existingIdentityUser.Id && e.Expires == _fixedNow.AddSeconds(900) &&
        !e.Verified && !e.Applied && e.CreatedBy == existingIdentityUser.Id))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _passwordResetRepository.RemoveExpiredEntityItemsAsync(existingIdentityUser.Id, _fixedNow, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    A.CallTo(() => _passwordResetEventHandler.HandleEventAsync(A<PasswordResetEvent>.That.Matches(e =>
        e.ResetRequestId == ResetRequestId && e.RegisteredApp == _registeredApp && e.UserContent.UserIdentityId == existingIdentityUser.Id &&
        e.UserContent.UserEmail == existingIdentityUser.Email && e.UserContent.PhoneNumber == existingIdentityUser.PhoneNumber &&
        e.UserContent.FirstName == existingEntity.FirstName), CancellationToken.None)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task ResetPassword_Within_Cooldown_Does_Not_Store_Request_Or_Publish_Event()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);
    var latestEntity = new PasswordResetEntity { UserId = existingIdentityUser.Id, CreatedAt = _fixedNow.AddSeconds(-30) };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns(latestEntity);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _passwordResetEventHandler.HandleEventAsync(A<PasswordResetEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_Without_User_Record_Does_Not_Store_Request_Or_Publish_Event()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns((PasswordResetEntity?)null);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Throws(new NoRecordException(existingIdentityUser.Id, "User"));

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _passwordResetEventHandler.HandleEventAsync(A<PasswordResetEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_When_Event_Publishing_Fails_Logs_Error_And_Returns_Request_Id()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns((PasswordResetEntity?)null);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns(new UserEntity { Id = existingIdentityUser.Id });
    A.CallTo(() => _passwordResetEventHandler.HandleEventAsync(A<PasswordResetEvent>._, A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("Broker unavailable"));

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _logHelper.LogEvent(LogLevel.Error, A<InvalidOperationException>._, A<string>._, A<object?[]>._)).MustHaveHappenedOnceExactly();
  }

  private ResetPasswordCommand GetResetPasswordCommand(string userName)
  {
    return new ResetPasswordCommand
    {
      UserName = userName,
      RegisteredApp = _registeredApp
    };
  }

  private static IdentityUser GetIdentityUser()
  {
    return new IdentityUser
    {
      Id = Guid.NewGuid().ToString(),
      Email = "test@lp.com",
      UserName = "test.user",
      PhoneNumber = "0123456789"
    };
  }

  private ResetPasswordCommandHandler GetResetPasswordCommandHandler()
  {
    return new ResetPasswordCommandHandler(
      _passwordResetSettings,
      _userManager,
      _machineDateTime,
      _tokenFactory,
      _unitOfWork,
      _userRepository,
      _passwordResetRepository,
      _passwordResetEventHandler,
      _configuration,
      _logHelper
    );
  }
}
