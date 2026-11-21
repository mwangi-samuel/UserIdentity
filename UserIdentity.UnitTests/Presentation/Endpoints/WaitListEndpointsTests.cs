using System.Net;
using FakeItEasy;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Enums;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class WaitListEndpointsTests
{
    private readonly ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> _createWaitListCommandHandler;
    private readonly IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> _getWaitListQueryHandler;
    private readonly IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> _getRegisteredAppHandler;

    public WaitListEndpointsTests()
    {
        _createWaitListCommandHandler = A.Fake<ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel>>();
        _getWaitListQueryHandler = A.Fake<IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel>>();
        _getRegisteredAppHandler = A.Fake<IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel>>();

        A.CallTo(() => _getRegisteredAppHandler.GetItemAsync(A<GetRegisteredAppQuery>._, A<CancellationToken>._))
            .Returns(
                new RegisteredAppViewModel
                {
                    RegisteredApp = new RegisteredAppDto
                    {
                        Id = 4,
                        AppName = "TestApp",
                        BaseUrl = "https://app.test",
                    },
                }
            );
        A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(A<GetWaitListQuery>._, A<CancellationToken>._))
            .Returns(new WaitListViewModel { WaitList = new WaitListDto { Id = 1 } });
        A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(A<CreateWaitListCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new WaitListViewModel { WaitList = new WaitListDto { Id = 1 } });
    }

    [Fact]
    public async Task GetWaitList_Passes_The_Admins_App_And_Grants_Rights()
    {
        // Arrange
        var request = ApiRequestFactory.Create("wait-list");

        // Act
        var result = await WaitListEndpoints.GetWaitListAsync(
            1,
            request,
            _getRegisteredAppHandler,
            _getWaitListQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Value?.EditEnabled);
        Assert.True(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(new GetWaitListQuery { WaitListId = 1, AppId = 4 }, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetWaitListByEmail_Passes_The_Admins_App()
    {
        // Arrange
        var userEmail = "test@example.com";
        var request = ApiRequestFactory.Create("wait-list");

        // Act
        var result = await WaitListEndpoints.GetWaitListByEmailAsync(
            userEmail,
            request,
            _getRegisteredAppHandler,
            _getWaitListQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getWaitListQueryHandler.GetItemAsync(new GetWaitListQuery { UserEmail = userEmail, AppId = 4 }, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateWaitList_Passes_The_Requests_App_And_Returns_Created()
    {
        // Arrange
        var command = new CreateWaitListCommand { UserEmail = "new@example.com" };
        var request = ApiRequestFactory.Create("wait-list", userId: TestStringHelper.UserId, appName: "TestApp");

        // Act
        var result = await WaitListEndpoints.CreateWaitListAsync(
            command,
            request,
            _getRegisteredAppHandler,
            _createWaitListCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(4, command.AppId);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateWaitList_Without_UserId_Uses_Anonymous()
    {
        // Arrange
        var command = new CreateWaitListCommand { UserEmail = "anonymous@example.com" };
        var request = ApiRequestFactory.Create("wait-list", appName: "TestApp");

        // Act
        var result = await WaitListEndpoints.CreateWaitListAsync(
            command,
            request,
            _getRegisteredAppHandler,
            _createWaitListCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);

        A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(command, request.LoggedInUserId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
