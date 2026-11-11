using System.Net;
using FakeItEasy;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class InviteCodeEndpointsTests
{
    private readonly ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> _createInviteCodeCommandHandler;
    private readonly IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> _getInviteCodeQueryHandler;
    private readonly IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> _getInviteCodesQueryHandler;
    private readonly IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> _deleteInviteCodeCommandHandler;
    private readonly IRegisteredAppResolver _registeredAppResolver;

    public InviteCodeEndpointsTests()
    {
        _createInviteCodeCommandHandler = A.Fake<ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel>>();
        _getInviteCodeQueryHandler = A.Fake<IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel>>();
        _getInviteCodesQueryHandler = A.Fake<IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel>>();
        _deleteInviteCodeCommandHandler = A.Fake<IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel>>();
        _registeredAppResolver = A.Fake<IRegisteredAppResolver>();

        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
            .Returns(
                new ActiveRegisteredApp
                {
                    Id = 4,
                    AppName = "TestApp",
                    AppSecretKey = "test-secret",
                    BaseUrl = "https://app.test",
                }
            );
    }

    [Fact]
    public async Task GetInviteCode_Passes_The_Admins_App_And_Grants_Rights()
    {
        // Arrange
        A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(A<GetInviteCodeQuery>._, A<CancellationToken>._))
            .Returns(new InviteCodeViewModel { InviteCode = new InviteCodeDto { Id = 1 } });

        var request = ApiRequestFactory.Create("invite-code");

        // Act
        var result = await InviteCodeEndpoints.GetInviteCodeAsync(
            1,
            request,
            _registeredAppResolver,
            _getInviteCodeQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Value?.EditEnabled);
        Assert.True(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getInviteCodeQueryHandler.GetItemAsync(new GetInviteCodeQuery { InviteCodeId = 1, AppId = 4 }, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetInviteCodeByEmail_Passes_The_Admins_App()
    {
        // Arrange
        var userEmail = "test@example.com";

        A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(A<GetInviteCodeQuery>._, A<CancellationToken>._))
            .Returns(new InviteCodeViewModel { InviteCode = new InviteCodeDto { Id = 1 } });

        var request = ApiRequestFactory.Create("invite-code");

        // Act
        var result = await InviteCodeEndpoints.GetInviteCodeByEmailAsync(
            userEmail,
            request,
            _registeredAppResolver,
            _getInviteCodeQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getInviteCodeQueryHandler.GetItemAsync(new GetInviteCodeQuery { UserEmail = userEmail, AppId = 4 }, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateInviteCode_Passes_The_Admins_App_To_The_Command_And_Returns_Created()
    {
        // Arrange
        var command = new CreateInviteCodeCommand
        {
            InviteCode = "NEWINVITE",
            UserEmail = "owner@example.com",
            Role = "testapp:owner",
        };

        A.CallTo(() =>
                _createInviteCodeCommandHandler.CreateItemAsync(
                    A<CreateInviteCodeCommand>._,
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .Returns(new InviteCodeViewModel { InviteCode = new InviteCodeDto { Id = 1 } });

        var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

        // Act
        var result = await InviteCodeEndpoints.CreateInviteCodeAsync(
            command,
            request,
            _registeredAppResolver,
            _createInviteCodeCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(4, command.AppId);
        Assert.Equal("TestApp", command.AppName);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetInviteCodes_Passes_The_Admins_App_And_The_Paging()
    {
        // Arrange
        A.CallTo(() => _getInviteCodesQueryHandler.GetItemsAsync(A<GetInviteCodesQuery>._, A<CancellationToken>._))
            .Returns(new InviteCodesViewModel { InviteCodes = [], TotalCount = 0 });

        var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

        // Act
        var result = await InviteCodeEndpoints.GetInviteCodesAsync(
            request,
            _registeredAppResolver,
            _getInviteCodesQueryHandler,
            TestContext.Current.CancellationToken,
            2,
            5,
            "owner",
            false
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() =>
                _getInviteCodesQueryHandler.GetItemsAsync(
                    new GetInviteCodesQuery
                    {
                        AppId = 4,
                        Page = 2,
                        PageSize = 5,
                        Search = "owner",
                        Applied = false,
                    },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteInviteCode_Deletes_The_Apps_InviteCode()
    {
        // Arrange
        A.CallTo(() =>
                _deleteInviteCodeCommandHandler.DeleteItemAsync(
                    A<DeleteInviteCodeCommand>._,
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .Returns(new DeleteRecordViewModel());

        var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

        // Act
        var result = await InviteCodeEndpoints.DeleteInviteCodeAsync(
            7,
            request,
            _registeredAppResolver,
            _deleteInviteCodeCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.DeleteItemSuccessful.ToText(), result.Value?.StatusMessage);
        A.CallTo(() =>
                _deleteInviteCodeCommandHandler.DeleteItemAsync(
                    new DeleteInviteCodeCommand { InviteCodeId = 7, AppId = 4 },
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }
}
