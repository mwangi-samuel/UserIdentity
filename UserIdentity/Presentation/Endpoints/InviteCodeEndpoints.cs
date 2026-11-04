using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Paging;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Application.Core.InviteCodes.ViewModels;

namespace UserIdentity.Presentation.Endpoints;

public static class InviteCodeEndpoints
{
  public static RouteGroupBuilder MapInviteCodeEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapApiGroup("InviteCode").RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin);

    group.MapGet("", GetInviteCodes).Produces<InviteCodesViewModel>();
    group.MapGet("{id}", GetInviteCode).Produces<InviteCodeViewModel>();
    group.MapGet("email/{email}", GetInviteCodeByEmail).Produces<InviteCodeViewModel>();
    group.MapPost("", CreateInviteCode).Produces<InviteCodeViewModel>();
    group.MapDelete("{id}", DeleteInviteCode).Produces<DeleteRecordViewModel>();

    return group;
  }

  internal static async Task<JsonHttpResult<InviteCodesViewModel>> GetInviteCodes(
    ApiRequest request,
    IRegisteredAppResolver registeredAppResolver,
    IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> handler,
    CancellationToken cancellationToken,
    [FromQuery, Range(1, Paging.MaxPage)] int page = 1,
    [FromQuery, Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
    [FromQuery, MaxLength(100)] string? search = null,
    [FromQuery] bool? applied = null)
  {
    var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

    var query = new GetInviteCodesQuery
    {
      AppId = registeredApp.Id,
      Page = page,
      PageSize = pageSize,
      Search = search,
      Applied = applied
    };

    var vm = await handler.GetItemsAsync(query, cancellationToken);

    return ViewModelResults.GetItems(request, vm);
  }

  internal static async Task<JsonHttpResult<InviteCodeViewModel>> GetInviteCode(
    long id,
    ApiRequest request,
    IRegisteredAppResolver registeredAppResolver,
    IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> handler,
    CancellationToken cancellationToken)
  {
    var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

    var vm = await handler.GetItemAsync(
      new GetInviteCodeQuery { InviteCodeId = id, AppId = registeredApp.Id },
      cancellationToken);

    return ViewModelResults.GetItem(request, vm, true);
  }

  internal static async Task<JsonHttpResult<InviteCodeViewModel>> GetInviteCodeByEmail(
    string email,
    ApiRequest request,
    IRegisteredAppResolver registeredAppResolver,
    IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> handler,
    CancellationToken cancellationToken)
  {
    var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

    var vm = await handler.GetItemAsync(
      new GetInviteCodeQuery { UserEmail = email, AppId = registeredApp.Id },
      cancellationToken);

    return ViewModelResults.GetItem(request, vm, true);
  }

  internal static async Task<JsonHttpResult<InviteCodeViewModel>> CreateInviteCode(
    CreateInviteCodeCommand command,
    ApiRequest request,
    IRegisteredAppResolver registeredAppResolver,
    ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> handler,
    CancellationToken cancellationToken)
  {
    var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

    command.AppId = registeredApp.Id;
    command.AppName = registeredApp.AppName;

    var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

    return ViewModelResults.CreateItem(request, vm, true);
  }

  internal static async Task<JsonHttpResult<DeleteRecordViewModel>> DeleteInviteCode(
    long id,
    ApiRequest request,
    IRegisteredAppResolver registeredAppResolver,
    IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> handler,
    CancellationToken cancellationToken)
  {
    var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

    var vm = await handler.DeleteItemAsync(
      new DeleteInviteCodeCommand { InviteCodeId = id, AppId = registeredApp.Id },
      request.LoggedInUserId,
      cancellationToken);

    return ViewModelResults.DeleteItem(vm);
  }
}
