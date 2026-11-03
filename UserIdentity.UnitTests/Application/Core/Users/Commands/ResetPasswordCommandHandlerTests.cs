using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.Messages.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ResetPasswordCommandHandlerTests
{
  private const string ResetRequestId = "SampleResetRequestId";
  private const string ResetRequestIdHash = "SampleResetRequestIdHash";

  private readonly IOptions<PasswordResetSettings> _passwordResetSettings;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly FakeTimeProvider _timeProvider;
  private readonly ITokenFactory _tokenFactory;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IUserRepository _userRepository;
  private readonly IPasswordResetRepository _passwordResetRepository;
  private readonly IMessageOutbox _messageOutbox;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();

  private readonly DateTime _fixedNow = new(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
  private readonly RegisteredAppEntity _registeredApp = new() { Id = 1, AppName = "TestApp" };

  public ResetPasswordCommandHandlerTests()
  {
    _passwordResetSettings = Options.Create(new PasswordResetSettings
    {
      ValidForSeconds = 900,
      RequestCooldownSeconds = 60
    });
    _userManager = IdentityFakes.UserManager();
    _timeProvider = new FakeTimeProvider();
    _tokenFactory = A.Fake<ITokenFactory>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _userRepository = A.Fake<IUserRepository>();
    _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    _messageOutbox = A.Fake<IMessageOutbox>();

    _timeProvider.SetUtcNow(_fixedNow);
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(ResetRequestId);
    A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(
      A<string>._,
      A<string>._,
      A<CancellationToken>._))
      .Returns(true);
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
    Assert.Equal(ResetPasswordCommandHandler.DefaultResetPasswordMessage, vm.ResetPasswordDetails.EmailMessage);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_With_Valid_Details_Stores_Request_And_Adds_A_Message_Before_Saving()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.Email!);
    var existingEntity = new UserEntity { Id = existingIdentityUser.Id, FirstName = "Test", LastName = "User" };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Returns((PasswordResetEntity?)null);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Returns(existingEntity);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);

    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>.That.Matches(e =>
        e.RequestId == ResetRequestIdHash
          && e.UserId == existingIdentityUser.Id
          && e.Expires == _fixedNow.AddSeconds(900)
          &&
        !e.Verified
          && !e.Applied
          && e.CreatedBy == existingIdentityUser.Id))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    A.CallTo(() => _messageOutbox.Add(UserMessages.UserUpdated, A<MessageEvent>.That.Matches(m =>
        m.Action == MessageAction.PasswordReset
        && m.CorrelationId == ResetRequestId
        && m.RegisteredApp == _registeredApp.AppName
        && m.Attributes[MessageAttributeKey.UserId] == existingIdentityUser.Id
        && m.Attributes[MessageAttributeKey.RecipientEmail] == existingIdentityUser.Email
        && m.Attributes[MessageAttributeKey.RecipientPhone] == existingIdentityUser.PhoneNumber
        && m.Attributes[MessageAttributeKey.FirstName] == existingEntity.FirstName)))
        .MustHaveHappenedOnceExactly()
        .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
  }

  [Fact]
  public async Task ResetPassword_For_A_User_Of_Another_App_Does_Not_Store_Request_Or_Add_A_Message()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(
      existingIdentityUser.Id,
      _registeredApp.AppName,
      A<CancellationToken>._))
      .Returns(false);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_After_The_Daily_Limit_Does_Not_Store_Request_Or_Add_A_Message()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Returns((PasswordResetEntity?)null);
    A.CallTo(() => _passwordResetRepository.CountEntityItemsCreatedAfterAsync(
      existingIdentityUser.Id,
      _fixedNow.AddDays(-1),
      A<CancellationToken>._))
      .Returns(5);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_Within_Cooldown_Does_Not_Store_Request_Or_Add_A_Message()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);
    var latestEntity = new PasswordResetEntity
    {
      UserId = existingIdentityUser.Id,
      CreatedAt = _fixedNow.AddSeconds(-30)
    };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Returns(latestEntity);

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ResetPassword_Without_User_Record_Does_Not_Store_Request_Or_Add_A_Message()
  {
    // Arrange
    var existingIdentityUser = GetIdentityUser();
    var command = GetResetPasswordCommand(existingIdentityUser.UserName!);

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _passwordResetRepository.GetLatestEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Returns((PasswordResetEntity?)null);
    A.CallTo(() => _userRepository.GetEntityItemAsync(existingIdentityUser.Id, A<CancellationToken>._))
      .Throws(NoRecordException.ForIdentifier(existingIdentityUser.Id));

    var handler = GetResetPasswordCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(ResetRequestId, vm.ResetPasswordDetails.ResetRequestId);
    A.CallTo(() => _passwordResetRepository.CreateEntityItem(A<PasswordResetEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  private ResetPasswordCommand GetResetPasswordCommand(string userName)
  {
    return new ResetPasswordCommand { UserName = userName, RegisteredApp = _registeredApp };
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
      _timeProvider,
      _tokenFactory,
      _unitOfWork,
      _userRepository,
      _passwordResetRepository,
      _messageOutbox,
      _userRegisteredAppRepository
    );
  }
}
