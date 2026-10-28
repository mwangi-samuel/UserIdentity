using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Paging;

using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Queries;

public record GetInviteCodesQuery : IBaseQuery
{
  public required int AppId { get; init; }

  public string? Search { get; init; }

  public bool? Applied { get; init; }

  public int Page { get; init; } = 1;

  public int PageSize { get; init; } = Paging.DefaultPageSize;
}

public class GetInviteCodesQueryHandler(
  IInviteCodeRepository inviteCodeRepository
  ) : IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel>
{
  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

  public async Task<InviteCodesViewModel> GetItemsAsync(GetInviteCodesQuery query, CancellationToken cancellationToken = default)
  {
    var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
    var page = search is null && !query.Applied.HasValue
      ? await _inviteCodeRepository.GetEntityDtosByAppIdAsync(query.AppId, query.Page, query.PageSize, cancellationToken)
      : await _inviteCodeRepository.SearchEntityDtosByAppIdAsync(query.AppId, search, query.Applied, query.Page, query.PageSize, cancellationToken);

    return new InviteCodesViewModel
    {
      InviteCodes = page.Items,
      TotalCount = page.TotalCount
    };
  }
}
