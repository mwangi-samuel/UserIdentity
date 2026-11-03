using System.Security.Claims;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.Messages.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class RegisterUserCommandHandlerTests
{
  private readonly IOptions<RoleSettings> _roleSettings;
  private readonly IOptions<IdentityOptions> _identityOptions;
  private readonly IOptions<RefreshTokenSettings> _refreshTokenSettings;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IUserRepository _userRepository;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;
  private readonly IInviteCodeRepository _inviteCodeRepository;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory;
  private readonly TimeProvider _timeProvider;
  private readonly IMessageOutbox _messageOutbox;
  private readonly IGoogleRecaptchaService _googleRecaptchaService;
  private readonly IRoleRepository _roleRepository;

  public RegisterUserCommandHandlerTests()
  {
    _roleSettings = A.Fake<IOptions<RoleSettings>>();
    _identityOptions = A.Fake<IOptions<IdentityOptions>>();
    _refreshTokenSettings = Options.Create(new RefreshTokenSettings { ValidForSeconds = 2592000 });
    _userManager = IdentityFakes.UserManager();
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _userRepository = A.Fake<IUserRepository>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    _tokenFactory = A.Fake<ITokenFactory>();
    _timeProvider = TimeProvider.System;
    _messageOutbox = A.Fake<IMessageOutbox>();
    _googleRecaptchaService = A.Fake<IGoogleRecaptchaService>();
    _roleRepository = A.Fake<IRoleRepository>();

    A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
  }

  [Fact]
  public async Task Create_User_When_Default_Role_Not_Created_Throws_RecordCreationException()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = GetDefaultRoleName();

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole)).Returns((IdentityRole?)null);
    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == defaultRole))).Returns(IdentityResult.Failed());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == defaultRole))).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_When_UserName_Exists_Throws_RecordExistsException()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(new IdentityUser());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_When_Email_Exists_Throws_RecordExistsException()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(new IdentityUser());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_When_Identity_User_Creation_Fails_Throws_InvalidDataException()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Failed());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_User_When_Assigning_Default_Role_Fails_Throws_RecordCreationException()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Failed());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_With_Valid_Details_Returns_AuthUser()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());
    var refreshToken = "refresh-token";
    var refreshTokenHash = "refresh-token-hash";
    var accessToken = "access-token";
    var expiresIn = 1200;

    string? userId = null;
    IdentityUser? createdUser = null;
    HashSet<string> scopes = ["role-claim-value"];

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
        .Invokes((IdentityUser u, string _) =>
        {
          createdUser = u;
          userId = u.Id;
        })
        .Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);
    A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName))).DoesNothing();
    A.CallTo(() => _roleRepository.GetUserRoleNamesAsync(A<string>._, A<CancellationToken>._)).Returns([defaultRole.Name!]);
    A.CallTo(() => _roleRepository.GetScopesAsync(A<IReadOnlyCollection<string>>._, A<CancellationToken>._)).Returns(scopes);
    A.CallTo(() => _jwtTokenHandler.CreateToken(A<string>._, A<string>._, A<string>._, A<HashSet<string>>._, A<HashSet<string>>._, A<HashSet<Claim>?>._)).Returns((accessToken, expiresIn));
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(refreshToken);
    A.CallTo(() => _tokenFactory.HashToken(refreshToken)).Returns(refreshTokenHash);
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).DoesNothing();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).DoesNothing();

    var handler = GetRegisterUserCommandHandler();

    var vm = await handler.CreateItemAsync(command, userId!, TestContext.Current.CancellationToken);

    Assert.NotNull(vm.User);
    Assert.Equal(userId, vm.User.Id);
    Assert.Equal(command.FirstName, vm.User.FirstName);
    Assert.Equal(command.LastName, vm.User.LastName);
    Assert.Equal(command.UserEmail, vm.User.Email);
    Assert.NotNull(vm.UserToken);
    Assert.Equal(refreshToken, vm.UserToken?.RefreshToken);
    Assert.Equal(accessToken, vm.UserToken?.AccessToken?.Token);
    Assert.Equal(expiresIn, vm.UserToken?.AccessToken?.ExpiresIn);

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _roleRepository.GetUserRoleNamesAsync(userId!, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _jwtTokenHandler.CreateToken(userId!, command.UserName, command.RegisteredApp!.AppName, A<HashSet<string>>.That.IsSameSequenceAs(new[] { defaultRole.Name! }), scopes, A<HashSet<Claim>?>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tokenFactory.GenerateToken(32)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>.That.Matches(rt =>
        rt.Token == refreshTokenHash && rt.Expires > DateTime.UtcNow.AddDays(29)))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_Adds_A_Welcome_Message_Before_Committing()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());
    string? userId = null;

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
        .Invokes((IdentityUser u, string _) => userId = u.Id)
        .Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);

    var handler = GetRegisterUserCommandHandler();

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _messageOutbox.Add(UserMessages.UserUpdated, A<MessageEvent>.That.Matches(m =>
        m.Action == MessageAction.WelcomeUser
        && m.CorrelationId == userId
        && m.RegisteredApp == command.RegisteredApp!.AppName
        && m.Attributes[MessageAttributeKey.RecipientEmail] == command.UserEmail
        && m.Attributes[MessageAttributeKey.RecipientPhone] == command.PhoneNumber
        && m.Attributes[MessageAttributeKey.FirstName] == command.FirstName
        && m.Attributes[MessageAttributeKey.LastName] == command.LastName
        && m.Attributes[MessageAttributeKey.UserName] == command.UserName
        && m.Attributes[MessageAttributeKey.UserId] == userId)))
        .MustHaveHappenedOnceExactly()
        .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    A.CallTo(() => _messageOutbox.Add(A<string>._, A<MessageEvent>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_From_The_Apps_UI_With_A_Callback_Url_Adds_A_Callback_Message()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.RequestSource = RequestSource.UI;
    command.RegisteredApp!.CallbackUrl = "https://callback.test";
    SetUpSuccessfulSignUp(command);

    A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync(A<string?>._, A<CancellationToken>._)).Returns(true);

    var handler = GetRegisterUserCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _messageOutbox.Add(UserMessages.CallbackRequested, A<MessageEvent>.That.Matches(m =>
        m.Action == MessageAction.WelcomeUser && m.CorrelationId == vm.User.Id && m.RegisteredApp == command.RegisteredApp.AppName)))
        .MustHaveHappenedOnceExactly()
        .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
  }

  [Theory]
  [InlineData(RequestSource.Api, "https://callback.test")]
  [InlineData(RequestSource.UI, null)]
  public async Task Create_User_For_A_Service_Or_An_App_Without_A_Callback_Url_Adds_No_Callback_Message(RequestSource requestSource, string? callbackUrl)
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.RequestSource = requestSource;
    command.RegisteredApp!.CallbackUrl = callbackUrl;
    SetUpSuccessfulSignUp(command);

    A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync(A<string?>._, A<CancellationToken>._)).Returns(true);

    var handler = GetRegisterUserCommandHandler();

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _messageOutbox.Add(UserMessages.UserUpdated, A<MessageEvent>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _messageOutbox.Add(UserMessages.CallbackRequested, A<MessageEvent>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_User_With_Invite_Code_Marks_Invite_Code_Applied_By_New_User()
  {
    var command = GetRegisterUserCommand();
    command.RegisteredApp!.RequireInviteCode = true;
    const string inviteCode = "INVITE01";
    command.InviteCode = inviteCode;

    var defaultRole = new IdentityRole(GetDefaultRoleName());
    var inviteCodeEntity = new InviteCodeEntity { Id = 7, InviteCode = inviteCode, UserEmail = command.UserEmail!, Applied = false };
    string? userId = null;

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(
        A<InviteCodeEntity>.That.Matches(e => e.UserEmail == command.UserEmail), QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);
    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
        .Invokes((IdentityUser u, string _) => userId = u.Id)
        .Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);

    var handler = GetRegisterUserCommandHandler();

    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    Assert.True(inviteCodeEntity.Applied);
    Assert.NotNull(userId);
    Assert.Equal(userId, inviteCodeEntity.UpdatedBy);
    Assert.NotNull(inviteCodeEntity.UpdatedAt);

    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_With_Applied_Invite_Code_Throws_InvalidDataException()
  {
    var command = GetRegisterUserCommand();
    command.RegisteredApp!.RequireInviteCode = true;
    const string inviteCode = "INVITE01";
    command.InviteCode = inviteCode;

    var inviteCodeEntity = new InviteCodeEntity { Id = 7, InviteCode = inviteCode, UserEmail = command.UserEmail!, Applied = true };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_User_With_Optional_Invite_Code_Grants_The_Invites_Role()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    const string inviteCode = "INVITE01";
    const string inviteRole = "testapp:owner";
    command.InviteCode = inviteCode.ToLowerInvariant();

    var defaultRole = new IdentityRole(GetDefaultRoleName());
    var inviteCodeEntity = new InviteCodeEntity { Id = 7, InviteCode = inviteCode, UserEmail = command.UserEmail!, Applied = false, Role = inviteRole };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(
        A<InviteCodeEntity>.That.Matches(e => e.UserEmail == command.UserEmail && e.AppId == command.RegisteredApp!.Id), QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);
    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns(new IdentityRole(inviteRole));
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, A<string>._)).Returns(IdentityResult.Success);

    var handler = GetRegisterUserCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(command.UserEmail, vm.User.Email);
    Assert.True(inviteCodeEntity.Applied);

    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, inviteRole)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_With_Invalid_Optional_Invite_Code_Throws_InvalidDataException()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.InviteCode = "WRONGCODE";

    var inviteCodeEntity = new InviteCodeEntity { Id = 7, InviteCode = "INVITE01", UserEmail = command.UserEmail!, Applied = false, Role = "testapp:owner" };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);

    var handler = GetRegisterUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("missing")]
  [InlineData("wrong")]
  [InlineData("used")]
  public async Task Create_User_With_A_Missing_Wrong_Or_Used_Invite_Code_Gives_The_Same_Error(string inviteState)
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.InviteCode = "ABCDEFGHJK";

    var inviteCodeEntity = inviteState switch
    {
      "missing" => null,
      "wrong" => new InviteCodeEntity { InviteCode = "KJHGFEDCBA", UserEmail = command.UserEmail!, Applied = false },
      _ => new InviteCodeEntity { InviteCode = "ABCDEFGHJK", UserEmail = command.UserEmail!, Applied = true }
    };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);

    var handler = GetRegisterUserCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    Assert.Equal("A valid invite code is required when signing up.", exception.Message);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_User_When_The_Invites_Role_Is_Missing_Throws_InvalidDataException_And_Rolls_Back()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    const string inviteRole = "testapp:owner";
    command.InviteCode = "INVITE01";

    var defaultRole = new IdentityRole(GetDefaultRoleName());
    var inviteCodeEntity = new InviteCodeEntity { Id = 7, InviteCode = "INVITE01", UserEmail = command.UserEmail!, Applied = false, Role = inviteRole };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, QueryCondition.MayOrMayNotExist, A<CancellationToken>._))
        .Returns(inviteCodeEntity);
    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns((IdentityRole?)null);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);

    var handler = GetRegisterUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, inviteRole)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task Create_User_Without_Invite_Code_When_Not_Required_Does_Not_Check_Invites(string? inviteCode)
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.InviteCode = inviteCode;

    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);

    var handler = GetRegisterUserCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(command.UserName, vm.User.UserName);

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, A<QueryCondition>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, A<string>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_From_UI_When_Recaptcha_Fails_Throws_InvalidDataException()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    command.RequestSource = RequestSource.UI;
    command.GoogleRecaptchaToken = "recaptcha-token";

    A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync("recaptcha-token", A<CancellationToken>._)).Returns(false);

    var handler = GetRegisterUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
    A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_User_From_Api_Does_Not_Verify_Recaptcha()
  {
    // Arrange
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(new IdentityUser());

    var handler = GetRegisterUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
    A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync(A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private RegisterUserCommandHandler GetRegisterUserCommandHandler()
  {
    return new RegisterUserCommandHandler(
        _roleSettings,
        _identityOptions,
        _refreshTokenSettings,
        _userManager,
        _roleManager,
        _unitOfWork,
        _userRepository,
        _refreshTokenRepository,
        _userRegisteredAppRepository,
        _inviteCodeRepository,
        _roleRepository,
        _jwtTokenHandler,
        _tokenFactory,
        _timeProvider,
        _messageOutbox,
        _googleRecaptchaService
    );
  }

  private void SetUpSuccessfulSignUp(RegisterUserCommand command)
  {
    var defaultRole = new IdentityRole(GetDefaultRoleName());

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);
  }

  private string GetDefaultRoleName()
  {
    var settings = _roleSettings.Value;
    var command = GetRegisterUserCommand();
    return $"{command.RegisteredApp!.AppName.Trim().ToLowerInvariant()}{Separators.ServiceRole}{settings.DefaultRole.Trim().ToLowerInvariant()}";
  }

  private static RegisterUserCommand GetRegisterUserCommand()
  {
    return new RegisterUserCommand
    {
      FirstName = "Test",
      LastName = "User",
      UserName = "test.user",
      PhoneNumber = "0712345678",
      UserEmail = "test.user@domain.com",
      UserPassword = "Password123",
      RegisteredApp = new RegisteredAppEntity { Id = 1, AppName = "TestApp" },
      RequestSource = RequestSource.Api
    };
  }
}
