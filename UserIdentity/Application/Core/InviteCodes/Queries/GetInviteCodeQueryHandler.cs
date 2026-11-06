using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Queries;

public record GetInviteCodeQuery : IBaseQuery
{
    public long? InviteCodeId { get; init; }

    public string? UserEmail { get; init; }

    public required int AppId { get; init; }
}

public class GetInviteCodeQueryHandler(IInviteCodeRepository inviteCodeRepository)
    : IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel>
{
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

    public async Task<InviteCodeViewModel> GetItemAsync(GetInviteCodeQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.InviteCodeId.HasValue
            ? await _inviteCodeRepository.GetEntityDtoByIdAsync(query.InviteCodeId!.Value, cancellationToken)
            : InviteCodeDto.FromEntityDetailed(
                (
                    await _inviteCodeRepository.GetEntityByAlternateIdAsync(
                        new() { AppId = query.AppId, UserEmail = query.UserEmail! },
                        QueryCondition.MustExist,
                        cancellationToken
                    )
                )!
            );

        if (dto.AppId != query.AppId)
            throw NoRecordException.ForIdentifier($"{query.InviteCodeId}");

        return new InviteCodeViewModel { InviteCode = dto };
    }
}
