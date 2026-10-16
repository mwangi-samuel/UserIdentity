using System.Linq;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Queries;

public class GetUserQueryHandlerTests
{
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IUserRepository _userRepository;
  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler;


  public GetUserQueryHandlerTests()
  {
    _userManager = IdentityFakes.UserManager();
    _userRepository = A.Fake<IUserRepository>();
    _getRoleClaimsQueryHandler = A.Fake<IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels>>();
  }

  [Fact]
  public async Task Get_User_When_Non_Existent_In_User_Manager_Throws_NoRecordException()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(default(IdentityUser));

    GetUserQueryHandler handler = new(_userManager, _userRepository, _getRoleClaimsQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query));
  }

  [Fact]
  public async Task Get_User_When_Non_Existent_In_User_Repo_Throws_NoRecordException()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = "test",
      UserName = "test",
      Email = "test@lp.mll",
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userRepository.GetEntityItemAsync(query.UserId)).Throws(new NoRecordException(query.UserId, "User"));

    GetUserQueryHandler handler = new(_userManager, _userRepository, _getRoleClaimsQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query));
  }

  [Fact]
  public async Task Get_User_Existing_User_Returns_User()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = "test",
      UserName = "test",
      Email = "test@lp.mll",
    };

    UserEntity existingUser = new()
    {
      Id = existingIdentityUser.Id,
      FirstName = "test",
      LastName = "test",
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userRepository.GetEntityItemAsync(query.UserId)).Returns(existingUser);
    A.CallTo(() => _userManager.GetRolesAsync(existingIdentityUser)).Returns(["admin"]);
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsForRolesQuery>.That.Matches(q => q.Roles.Contains("admin"))))
        .Returns(new RoleClaimsForRolesViewModels { RoleClaims = ["scope:user:read"] });

    GetUserQueryHandler handler = new(_userManager, _userRepository, _getRoleClaimsQueryHandler);

    // Act 
    var vm = await handler.GetItemAsync(query);

    // Assert
    Assert.IsType<UserViewModel>(vm);
    Assert.IsType<UserDTO>(vm.User);
    Assert.Equal(existingUser.Id, vm.User?.Id);
    Assert.Equal("admin", vm.User?.Roles.First());
    Assert.Contains("scope:user:read", vm.User?.RoleClaims ?? []);
  }
}
