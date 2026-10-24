using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Attributes;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Common;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Queries;

public record GetInviteCodeQuery : IBaseQuery
{
  [EitherOr(nameof(GetInviteCodeQuery.InviteCodeId), nameof(GetInviteCodeQuery.UserEmail))]
  public long? InviteCodeId { get; init; }

  [EitherOr(nameof(GetInviteCodeQuery.InviteCodeId), nameof(GetInviteCodeQuery.UserEmail))]
  public string? UserEmail { get; init; }

  public required int AppId { get; init; }
}

public class GetInviteCodeQueryHandler(
  IInviteCodeRepository inviteCodeRepository
  ) : IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel>
{

  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

  public async Task<InviteCodeViewModel> GetItemAsync(GetInviteCodeQuery query, CancellationToken cancellationToken = default)
  {

    var dto = query.InviteCodeId.HasValue
      ? await _inviteCodeRepository.GetEntityDtoByIdAsync(query.InviteCodeId!.Value, cancellationToken)
      : InviteCodeDto.FromEntityDetailed((await _inviteCodeRepository.GetEntityByAlternateIdAsync(new() { AppId = query.AppId, UserEmail = query.UserEmail! }, QueryCondition.MUST_EXIST, cancellationToken))!);

    if (dto.AppId != query.AppId)
      throw new NoRecordException($"{query.InviteCodeId}", EntityTypes.INVITE_CODE.Description());

    return new InviteCodeViewModel
    {
      InviteCode = dto
    };
  }
}
