using System.Net;

using FakeItEasy;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Enums;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.RoleClaims;
using UserIdentity.Domain.Roles;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class RoleEndpointsTests
{
  private readonly ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel> _createRoleCommandHandler;
  private readonly ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel> _createUserRoleCommandHandler;
  private readonly IGetItemQueryHandler<GetRoleQuery, RoleViewModel> _getRoleQueryHandler;
  private readonly IGetItemsQueryHandler<GetRolesQuery, RolesViewModel> _getRolesQueryHandler;
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler;
  private readonly IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel> _updateRoleCommandHandler;
  private readonly IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel> _deleteRoleCommandHandler;
  private readonly ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel> _createRoleClaimCommandHandler;
  private readonly IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel> _getRoleClaimsQueryHandler;
  private readonly IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel> _deleteRoleClaimCommandHandler;

  public RoleEndpointsTests()
  {
    _createRoleCommandHandler = A.Fake<ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel>>();
    _createUserRoleCommandHandler = A.Fake<ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel>>();
    _getRoleQueryHandler = A.Fake<IGetItemQueryHandler<GetRoleQuery, RoleViewModel>>();
    _getRolesQueryHandler = A.Fake<IGetItemsQueryHandler<GetRolesQuery, RolesViewModel>>();
    _getUserRolesQueryHandler = A.Fake<IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel>>();
    _updateRoleCommandHandler = A.Fake<IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel>>();
    _deleteRoleCommandHandler = A.Fake<IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel>>();
    _createRoleClaimCommandHandler = A.Fake<ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel>>();
    _getRoleClaimsQueryHandler = A.Fake<IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel>>();
    _deleteRoleClaimCommandHandler = A.Fake<IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel>>();
  }

  [Fact]
  public async Task Get_Roles_Returns_Roles()
  {
    // Arrange
    var query = new GetRolesQuery { AppName = "testapp" };

    var rolesVM = new RolesViewModel
    {
      Roles =
                        [
                                new RoleDto
                                    {
                                            Id = Guid.NewGuid().ToString(),
                                            Name = "Admin"
                                    },
                                    new RoleDto
                                    {
                                            Id = Guid.NewGuid().ToString(),
                                            Name = "User"
                                    }
                        ]

    };

    // Act
    A.CallTo(() => _getRolesQueryHandler.GetItemsAsync(query, A<CancellationToken>._)).Returns(rolesVM);

    var request = ApiRequestFactory.Create("role");

    var result = await RoleEndpoints.GetRolesAsync(
      request,
      _getRolesQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(rolesVM.Roles.Count, vm?.Roles.Count);

    var allRecordsExist = false;

    foreach (var role in rolesVM.Roles)
    {
      allRecordsExist = vm?.Roles.Where(x => x.Id == role.Id && x.Name == role.Name).Any() ?? false;
    }

    Assert.True(allRecordsExist);

    Assert.False(vm?.DownloadEnabled);
    Assert.False(vm?.CreateEnabled);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemsSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Get_Role_Returns_Role()
  {
    // Arrange
    var roleId = Guid.NewGuid().ToString();
    var query = new GetRoleQuery { RoleId = roleId, AppName = "testapp" };

    var roleVM = new RoleViewModel
    {
      Role = new RoleDto
      {
        Id = roleId,
        Name = "Admin"
      }
    };

    // Act
    A.CallTo(() => _getRoleQueryHandler.GetItemAsync(query, A<CancellationToken>._)).Returns(roleVM);

    var request = ApiRequestFactory.Create("role");

    var result = await RoleEndpoints.GetRoleAsync(
      roleId,
      request,
      _getRoleQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(roleVM.Role.Id, vm?.Role.Id);
    Assert.Equal(roleVM.Role.Name, vm?.Role.Name);

    Assert.False(vm?.EditEnabled);
    Assert.False(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Create_Role_Creates_Role()
  {
    // Arrange

    var command = new CreateRoleCommand
    {
      RoleName = "Admin"
    };

    var roleVM = new RoleViewModel
    {
      Role = new RoleDto
      {
        Id = Guid.NewGuid().ToString(),
        Name = "Admin"
      }
    };

    // Act
    A.CallTo(() => _createRoleCommandHandler.CreateItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
      .Returns(roleVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.CreateRoleAsync(
      command,
      request,
      _createRoleCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(roleVM.Role.Id, vm?.Role.Id);
    Assert.Equal(roleVM.Role.Name, vm?.Role.Name);

    Assert.False(vm?.EditEnabled);
    Assert.False(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CreateItemSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Update_Role_Updates_Role()
  {
    // Arrange

    var roleId = Guid.NewGuid().ToString();

    var command = new UpdateRoleCommand
    {
      RoleId = roleId,
      RoleName = "Admin"
    };

    var roleVM = new RoleViewModel
    {
      Role = new RoleDto
      {
        Id = roleId,
        Name = "Admin"
      }
    };

    // Act
    A.CallTo(() => _updateRoleCommandHandler.UpdateItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
      .Returns(roleVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.UpdateRoleAsync(
      roleId,
      command,
      request,
      _updateRoleCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(roleVM.Role.Id, vm?.Role.Id);
    Assert.Equal(roleVM.Role.Name, vm?.Role.Name);

    Assert.False(vm?.EditEnabled);
    Assert.False(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.UpdateItemSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Delete_Role_Deletes_Role()
  {
    // Arrange
    var roleId = Guid.NewGuid().ToString();
    var command = new DeleteRoleCommand { RoleId = roleId };
    var deleteSuccesMessage = "Item deleted successfully";

    var deleteRoleVM = new DeleteRecordViewModel { };

    // Act
    A.CallTo(() => _deleteRoleCommandHandler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
      .Returns(deleteRoleVM);

    var request = ApiRequestFactory.Create("role");

    var result = await RoleEndpoints.DeleteRoleAsync(
      roleId,
      request,
      _deleteRoleCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(deleteSuccesMessage, vm?.StatusMessage);
  }

  [Fact]
  public async Task Get_User_Roles_Returns_User_Roles()
  {
    // Arrange
    var userId = Guid.NewGuid().ToString();
    var query = new GetUserRolesQuery { UserId = userId, AppName = "testapp" };

    var userRolesVM = new UserRolesViewModel
    {
      UserRoles = ["Admin", "User"],
    };

    // Act
    A.CallTo(() => _getUserRolesQueryHandler.GetItemsAsync(query, A<CancellationToken>._)).Returns(userRolesVM);

    var request = ApiRequestFactory.Create("role");

    var result = await RoleEndpoints.GetUserRolesAsync(
      userId,
      request,
      _getUserRolesQueryHandler,
      TestContext.Current.CancellationToken);

    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(userRolesVM.UserRoles.Count, vm?.UserRoles.Count);

    var allRolesExist = false;

    foreach (var role in userRolesVM.UserRoles)
    {
      if (vm?.UserRoles.Contains(role) != true)
      {
        allRolesExist = false;
        break;
      }
      else
      {
        allRolesExist = true;
      }
    }

    Assert.True(allRolesExist);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemsSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Create_User_Role_Creates_User_Role()
  {
    // Arrange
    var userId = Guid.NewGuid().ToString();
    var roleId = Guid.NewGuid().ToString();

    var command = new CreateUserRoleCommand
    {
      UserId = userId,
      RoleId = roleId
    };

    var userRolesVM = new UserRolesViewModel
    {
      UserRoles = ["Admin", "User"],
    };

    // Act
    A.CallTo(() => _createUserRoleCommandHandler.CreateItemAsync(
      command,
      TestStringHelper.UserId,
      A<CancellationToken>._))
      .Returns(userRolesVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.CreateUserRoleAsync(
      command,
      request,
      _createUserRoleCommandHandler,
      TestContext.Current.CancellationToken);

    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(userRolesVM.UserRoles.Count, vm?.UserRoles.Count);

    var allRolesExist = false;

    foreach (var role in userRolesVM.UserRoles)
    {
      if (vm?.UserRoles.Contains(role) != true)
      {
        allRolesExist = false;
        break;
      }
      else
      {
        allRolesExist = true;
      }
    }

    Assert.True(allRolesExist);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CreateItemSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Create_Role_Claim_Creates_Role_Claim()
  {
    // Arrange
    var roleId = Guid.NewGuid().ToString();
    var resource = "Resource";
    var action = "Action";

    var command = new CreateRoleClaimCommand
    {
      RoleId = roleId,
      Resource = resource,
      Action = action
    };

    var roleClaimVM = new RoleClaimViewModel
    {
      RoleClaim = new RoleClaimDto
      {
        Resource = resource,
        Action = action,
        Scope = resource + ":" + action
      }
    };

    // Act
    A.CallTo(() => _createRoleClaimCommandHandler.CreateItemAsync(
      command,
      TestStringHelper.UserId,
      A<CancellationToken>._))
      .Returns(roleClaimVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.CreateRoleClaimAsync(
      command,
      request,
      _createRoleClaimCommandHandler,
      TestContext.Current.CancellationToken);

    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);

    Assert.NotNull(vm);

    Assert.Equal(roleClaimVM.RoleClaim.Resource, vm?.RoleClaim.Resource);
    Assert.Equal(roleClaimVM.RoleClaim.Action, vm?.RoleClaim.Action);
    Assert.Equal(roleClaimVM.RoleClaim.Scope, vm?.RoleClaim.Scope);
  }

  [Fact]
  public async Task Get_Role_Claims_Returns_Role_Claims()
  {
    // Arrange
    var roleId = Guid.NewGuid().ToString();
    var query = new GetRoleClaimsQuery { RoleId = roleId, AppName = "testapp" };

    var roleClaimsVM = new RoleClaimsViewModel
    {
      RoleClaims =
        [
            new RoleClaimDto
                {
                    Resource = "Resource",
                    Action = "Action",
                    Scope = "Resource:Action"
                }
        ]
    };

    // Act
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(query, A<CancellationToken>._)).Returns(roleClaimsVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.GetRoleClaimsAsync(
      roleId,
      request,
      _getRoleClaimsQueryHandler,
      TestContext.Current.CancellationToken);

    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(roleClaimsVM.RoleClaims.Count, vm?.RoleClaims.Count);

    var allClaimsExist = false;

    foreach (var claim in roleClaimsVM.RoleClaims)
    {
      if (vm?.RoleClaims.Contains(claim) != true)
      {
        allClaimsExist = false;
        break;
      }
      else
      {
        allClaimsExist = true;
      }
    }

    Assert.True(allClaimsExist);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemsSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Delte_Role_Claims_Deletes_Role_Claims()
  {
    // Arrange
    var roleId = Guid.NewGuid().ToString();
    var resource = "Resource";
    var action = "Action";
    var deleteMessage = "Item deleted successfully";

    var command = new DeleteRoleClaimCommand
    {
      RoleId = roleId,
      Resource = resource,
      Action = action
    };

    var deleteRoleClaimVM = new DeleteRecordViewModel { };

    // Act
    A.CallTo(() => _deleteRoleClaimCommandHandler.DeleteItemAsync(
      command,
      TestStringHelper.UserId,
      A<CancellationToken>._))
      .Returns(deleteRoleClaimVM);

    var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

    var result = await RoleEndpoints.DeleteRoleClaimAsync(
      command,
      request,
      _deleteRoleClaimCommandHandler,
      TestContext.Current.CancellationToken);

    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);

    Assert.NotNull(vm);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(deleteMessage, vm?.StatusMessage);

  }
}
