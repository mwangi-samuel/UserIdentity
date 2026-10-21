using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Attributes;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Common;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Repositories.WaitLists;

namespace UserIdentity.Application.Core.WaitLists.Queries;

public class GetWaitListQuery : IBaseQuery
{
  [EitherOr(nameof(GetWaitListQuery.WaitListId), nameof(GetWaitListQuery.UserEmail))]
  public long? WaitListId { get; set; }

  [EitherOr(nameof(GetWaitListQuery.WaitListId), nameof(GetWaitListQuery.UserEmail))]
  public string? UserEmail { get; set; }

  public required int AppId { get; init; }
}

public class GetWaitListQueryHandler(
  IWaitListRepository waitListRepository
  ) : IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel>
{
  private readonly IWaitListRepository _waitListRepository = waitListRepository;

  public async Task<WaitListViewModel> GetItemAsync(GetWaitListQuery query, CancellationToken cancellationToken = default)
  {

    var dto = query.WaitListId.HasValue
      ? await _waitListRepository.GetEntityDTOByIdAsync(query.WaitListId!.Value, cancellationToken)
      : WaitListDTO.FromEntityDetailed((await _waitListRepository.GetEntityByAlternateIdAsync(new() { AppId = query.AppId, UserEmail = query.UserEmail! }, QueryCondition.MUST_EXIST, cancellationToken))!);

    if (dto.AppId != query.AppId)
      throw new NoRecordException($"{query.WaitListId}", EntityTypes.WAIT_LIST.Description());

    return new WaitListViewModel { WaitList = dto };
  }
}
