using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Jwt;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class CreateRoleClaimCommandHandlerTests
{

  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler;

  public CreateRoleClaimCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Does_Not_Exist_Throws_NoRecordException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));


    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Exists_Throws_RecordExistsException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [new("scope", scope)];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);


    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Creation_Fails_Throws_RecordCreationException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);

    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).Returns(Task.FromResult(IdentityResult.Failed()));

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act & Assert
    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Creation_Succeeds_Returns_RoleClaim()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);

    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).Returns(Task.FromResult(IdentityResult.Success));

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleClaimViewModel>(vm);
    Assert.Equal(command.Resource, vm.RoleClaim.Resource);
    Assert.Equal(command.Action, vm.RoleClaim.Action);
    Assert.Equal(scope, vm.RoleClaim.Scope);
  }
}
