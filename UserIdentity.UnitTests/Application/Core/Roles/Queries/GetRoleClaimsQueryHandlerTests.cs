using System.Security.Claims;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.RoleClaims;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Queries;

public class GetRoleClaimsQueryHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  public GetRoleClaimsQueryHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
  }

  [Fact]
  public async Task Get_RoleClaims_When_Role_Is_Not_Found_Throws_NoRecordException()
  {
    // Arrange
    GetRoleClaimsQuery query = new() { RoleId = "1" };

    A.CallTo(() => _roleManager.FindByIdAsync(query.RoleId)).Returns((default(IdentityRole)));

    GetRoleClaimsQueryHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemsAsync(
      query,
      TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Get_RoleClaims_For_Another_Apps_Role_Throws_NoRecordException()
  {
    // Arrange
    GetRoleClaimsQuery query = new() { RoleId = "1", AppName = "testapp" };

    A.CallTo(() => _roleManager.FindByIdAsync(query.RoleId))
      .Returns(new IdentityRole { Id = query.RoleId, Name = "otherapp:owner" });

    GetRoleClaimsQueryHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemsAsync(
      query,
      TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Get_RoleClaims_Returns_RoleClaims()
  {
    // Arrange
    GetRoleClaimsQuery query = new() { RoleId = "1" };

    IdentityRole role = new() { Id = "1", Name = "Admin" };

    var resource = "resource";
    var action = "action";

    List<Claim> roleClaims = [new("scope", $"{resource}:{action}")];

    A.CallTo(() => _roleManager.FindByIdAsync(query.RoleId)).Returns(role);

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);

    A.CallTo(() => _jwtTokenHandler.DecodeScopeClaim(roleClaims[0])).Returns((resource, action));

    GetRoleClaimsQueryHandler handler = new(_roleManager, _jwtTokenHandler);

    // Act
    var vm = await handler.GetItemsAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleClaimsViewModel>(vm);
    Assert.IsType<ICollection<RoleClaimDto>>(vm.RoleClaims, exactMatch: false);
    Assert.Equal(roleClaims.Count, vm.RoleClaims.Count);
  }
}
