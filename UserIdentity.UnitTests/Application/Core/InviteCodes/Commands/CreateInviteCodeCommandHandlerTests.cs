using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Commands;

public class CreateInviteCodeCommandHandlerTests
{
  private readonly IOptions<RoleSettings> _roleSettings;
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IInviteCodeRepository _inviteCodeRepository;
  private readonly IMachineDateTime _machineDateTime;

  public CreateInviteCodeCommandHandlerTests()
  {
    _roleSettings = Options.Create(new RoleSettings { ServiceName = "UserIdentity", AdminRoles = "admin,superuser", DefaultRole = "user" });
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    _machineDateTime = A.Fake<IMachineDateTime>();

    A.CallTo(() => _machineDateTime.Now).Returns(DateTime.UtcNow);
  }

  [Fact]
  public async Task CreateItemAsync_Creates_InviteCode_And_Returns_ViewModel()
  {
    // Arrange
    var command = new CreateInviteCodeCommand
    {
      InviteCode = "INVITE123",
      UserEmail = "test@example.com",
      AppId = 1,
      Applied = false
    };

    var expectedDto = new InviteCodeDto
    {
      Id = 1,
      InviteCode = command.InviteCode,
      UserEmail = command.UserEmail,
      AppId = command.AppId,
      Applied = command.Applied,
      AppName = "TestApp"
    };

    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(expectedDto);

    var handler = GetHandler();

    // Act
    var result = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.InviteCode);
    Assert.Equal(expectedDto.InviteCode, result.InviteCode.InviteCode);
    Assert.Equal(expectedDto.UserEmail, result.InviteCode.UserEmail);
    Assert.Equal(expectedDto.AppId, result.InviteCode.AppId);

    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task CreateItemAsync_Sets_Audit_Fields()
  {
    // Arrange
    var now = DateTime.UtcNow;
    A.CallTo(() => _machineDateTime.Now).Returns(now);

    var command = new CreateInviteCodeCommand
    {
      InviteCode = "INVITE456",
      UserEmail = "audit@example.com",
      AppId = 2
    };

    InviteCodeEntity? capturedEntity = null;
    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._))
        .Invokes((InviteCodeEntity e) => capturedEntity = e);

    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(new InviteCodeDto
    {
      Id = 1,
      InviteCode = command.InviteCode,
      UserEmail = command.UserEmail,
      AppId = command.AppId
    });

    var handler = GetHandler();

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(capturedEntity);
    Assert.Equal(TestStringHelper.UserId, capturedEntity!.CreatedBy);
    Assert.Equal(TestStringHelper.UserId, capturedEntity.UpdatedBy);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  public async Task CreateItemAsync_Without_Role_Stores_No_Role(string? role)
  {
    // Arrange
    var command = GetCommand(role);

    InviteCodeEntity? capturedEntity = null;
    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._))
        .Invokes((InviteCodeEntity e) => capturedEntity = e);

    var handler = GetHandler();

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Null(capturedEntity?.Role);
    A.CallTo(() => _roleManager.FindByNameAsync(A<string>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("testapp:owner")]
  [InlineData(" TestApp:Owner ")]
  public async Task CreateItemAsync_With_An_Existing_App_Role_Stores_The_Role(string role)
  {
    // Arrange
    var command = GetCommand(role);

    A.CallTo(() => _roleManager.FindByNameAsync(role.Trim())).Returns(new IdentityRole("testapp:owner"));

    InviteCodeEntity? capturedEntity = null;
    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._))
        .Invokes((InviteCodeEntity e) => capturedEntity = e);

    var handler = GetHandler();

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("testapp:owner", capturedEntity?.Role);
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("otherapp:owner")]
  [InlineData("owner")]
  [InlineData("testapp:admin")]
  [InlineData("TestApp:Admin")]
  [InlineData("testapp:superuser")]
  [InlineData("global:admin")]
  public async Task CreateItemAsync_With_Another_Apps_Role_Or_An_Admin_Role_Throws_InvalidDataException(string role)
  {
    // Arrange
    var command = GetCommand(role);

    A.CallTo(() => _roleManager.FindByNameAsync(A<string>._)).Returns(new IdentityRole(role));

    var handler = GetHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task CreateItemAsync_With_A_Missing_Role_Throws_InvalidDataException()
  {
    // Arrange
    var command = GetCommand("testapp:owner");

    A.CallTo(() => _roleManager.FindByNameAsync("testapp:owner")).Returns((IdentityRole?)null);

    var handler = GetHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
  }

  private static CreateInviteCodeCommand GetCommand(string? role) => new()
  {
    InviteCode = "INVITE789",
    UserEmail = "owner@example.com",
    AppId = 1,
    AppName = "TestApp",
    Role = role
  };

  private CreateInviteCodeCommandHandler GetHandler() =>
    new(_roleSettings, _roleManager, _unitOfWork, _inviteCodeRepository, _machineDateTime);
}
