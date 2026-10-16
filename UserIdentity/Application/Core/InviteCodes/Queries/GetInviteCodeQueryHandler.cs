using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Attributes;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Queries;

public record GetInviteCodeQuery : IBaseQuery
{
  [EitherOr(nameof(GetInviteCodeQuery.InviteCodeId), nameof(GetInviteCodeQuery.UserEmail))]
  public long? InviteCodeId { get; init; }

  [EitherOr(nameof(GetInviteCodeQuery.InviteCodeId), nameof(GetInviteCodeQuery.UserEmail))]
  public string? UserEmail { get; init; }
}

public class GetInviteCodeQueryHandler(
  IInviteCodeRepository inviteCodeRepository
  ) : IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel>
{

  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

  public async Task<InviteCodeViewModel> GetItemAsync(GetInviteCodeQuery query, CancellationToken cancellationToken = default)
  {

    var dto = query.InviteCodeId.HasValue
      ? await _inviteCodeRepository.GetEntityDTOByIdAsync(query.InviteCodeId!.Value, cancellationToken)
      : InviteCodeDTO.FromEntityDetailed((await _inviteCodeRepository.GetEntityByAlternateIdAsync(new() { UserEmail = query.UserEmail! }, QueryCondition.MUST_EXIST, cancellationToken))!);

    return new InviteCodeViewModel
    {
      InviteCode = dto
    };
  }
}
