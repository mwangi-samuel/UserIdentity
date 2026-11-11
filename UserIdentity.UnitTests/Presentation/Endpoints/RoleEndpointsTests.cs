using System.Net;
using FakeItEasy;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
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

    private readonly RoleViewModel _roleViewModel = new()
    {
        Role = new RoleDto { Id = "role-id", Name = "testapp:owner" },
    };

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

        A.CallTo(() => _getRolesQueryHandler.GetItemsAsync(A<GetRolesQuery>._, A<CancellationToken>._))
            .Returns(new RolesViewModel { Roles = [] });
        A.CallTo(() => _getRoleQueryHandler.GetItemAsync(A<GetRoleQuery>._, A<CancellationToken>._)).Returns(_roleViewModel);
        A.CallTo(() => _createRoleCommandHandler.CreateItemAsync(A<CreateRoleCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(_roleViewModel);
        A.CallTo(() => _updateRoleCommandHandler.UpdateItemAsync(A<UpdateRoleCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(_roleViewModel);
        A.CallTo(() => _deleteRoleCommandHandler.DeleteItemAsync(A<DeleteRoleCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());
        A.CallTo(() => _getUserRolesQueryHandler.GetItemsAsync(A<GetUserRolesQuery>._, A<CancellationToken>._))
            .Returns(new UserRolesViewModel { UserRoles = [] });
        A.CallTo(() => _createUserRoleCommandHandler.CreateItemAsync(A<CreateUserRoleCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new UserRolesViewModel { UserRoles = [] });
        A.CallTo(() => _createRoleClaimCommandHandler.CreateItemAsync(A<CreateRoleClaimCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(
                new RoleClaimViewModel
                {
                    RoleClaim = new RoleClaimDto
                    {
                        Resource = "resource",
                        Action = "read",
                        Scope = "resource:read",
                    },
                }
            );
        A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsQuery>._, A<CancellationToken>._))
            .Returns(new RoleClaimsViewModel { RoleClaims = [] });
        A.CallTo(() => _deleteRoleClaimCommandHandler.DeleteItemAsync(A<DeleteRoleClaimCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());
    }

    [Fact]
    public async Task Get_Roles_Passes_The_Callers_App()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role");

        // Act
        var result = await RoleEndpoints.GetRolesAsync(request, _getRolesQueryHandler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Value?.DownloadEnabled);
        Assert.False(result.Value?.CreateEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() => _getRolesQueryHandler.GetItemsAsync(new GetRolesQuery { AppName = "testapp" }, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_Roles_For_A_Global_Admin_Passes_No_App_Name()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId, roles: [KnownRoles.GlobalAdmin]);

        // Act
        await RoleEndpoints.GetRolesAsync(request, _getRolesQueryHandler, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _getRolesQueryHandler.GetItemsAsync(new GetRolesQuery { AppName = null }, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_Role_Passes_The_Callers_App()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role");

        // Act
        var result = await RoleEndpoints.GetRoleAsync("role-id", request, _getRoleQueryHandler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Value?.EditEnabled);
        Assert.False(result.Value?.DeleteEnabled);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getRoleQueryHandler.GetItemAsync(new GetRoleQuery { RoleId = "role-id", AppName = "testapp" }, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_Role_Passes_The_Callers_App_And_Returns_Created()
    {
        // Arrange
        var command = new CreateRoleCommand { RoleName = "testapp:owner" };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.CreateRoleAsync(
            command,
            request,
            _createRoleCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal("testapp", command.AppName);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() => _createRoleCommandHandler.CreateItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Update_Role_Passes_The_Route_Role_And_The_Callers_App()
    {
        // Arrange
        var command = new UpdateRoleCommand { RoleName = "testapp:owner" };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.UpdateRoleAsync(
            "role-id",
            command,
            request,
            _updateRoleCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("role-id", command.RoleId);
        Assert.Equal("testapp", command.AppName);
        Assert.Equal(ItemStatusMessage.UpdateItemSuccessful.ToText(), result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Delete_Role_Passes_The_Callers_App()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.DeleteRoleAsync(
            "role-id",
            request,
            _deleteRoleCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.DeleteItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _deleteRoleCommandHandler.DeleteItemAsync(
                    new DeleteRoleCommand { RoleId = "role-id", AppName = "testapp" },
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_User_Roles_Passes_The_Callers_App()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role");

        // Act
        var result = await RoleEndpoints.GetUserRolesAsync(
            "user-id",
            request,
            _getUserRolesQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getUserRolesQueryHandler.GetItemsAsync(
                    new GetUserRolesQuery { UserId = "user-id", AppName = "testapp" },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_Role_Passes_The_Callers_App_And_Returns_Created()
    {
        // Arrange
        var command = new CreateUserRoleCommand { UserId = "user-id", RoleId = "role-id" };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.CreateUserRoleAsync(
            command,
            request,
            _createUserRoleCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal("testapp", command.AppName);
        Assert.False(result.Value?.CreateEnabled);
        Assert.False(result.Value?.DownloadEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Create_User_Role_Grants_Rights_To_A_Global_Admin()
    {
        // Arrange
        var command = new CreateUserRoleCommand { UserId = "user-id", RoleId = "role-id" };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId, roles: [KnownRoles.GlobalAdmin]);

        // Act
        var result = await RoleEndpoints.CreateUserRoleAsync(
            command,
            request,
            _createUserRoleCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(command.AppName);
        Assert.True(result.Value?.CreateEnabled);
        Assert.True(result.Value?.DownloadEnabled);
    }

    [Fact]
    public async Task Create_Role_Claim_Passes_The_Callers_App_And_Returns_Created()
    {
        // Arrange
        var command = new CreateRoleClaimCommand
        {
            RoleId = "role-id",
            Resource = "resource",
            Action = "read",
        };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.CreateRoleClaimAsync(
            command,
            request,
            _createRoleClaimCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal("testapp", command.AppName);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Get_Role_Claims_Passes_The_Callers_App()
    {
        // Arrange
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.GetRoleClaimsAsync(
            "role-id",
            request,
            _getRoleClaimsQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getRoleClaimsQueryHandler.GetItemsAsync(
                    new GetRoleClaimsQuery { RoleId = "role-id", AppName = "testapp" },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Delete_Role_Claim_Passes_The_Callers_App()
    {
        // Arrange
        var command = new DeleteRoleClaimCommand
        {
            RoleId = "role-id",
            Resource = "resource",
            Action = "read",
        };
        var request = ApiRequestFactory.Create("role", userId: TestStringHelper.UserId);

        // Act
        var result = await RoleEndpoints.DeleteRoleClaimAsync(
            command,
            request,
            _deleteRoleClaimCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("testapp", command.AppName);
        Assert.Equal(ItemStatusMessage.DeleteItemSuccessful.ToText(), result.Value?.StatusMessage);
    }
}
