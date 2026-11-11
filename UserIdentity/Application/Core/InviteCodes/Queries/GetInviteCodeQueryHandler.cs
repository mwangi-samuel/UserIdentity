using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
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
            : await _inviteCodeRepository.GetEntityDtoByEmailAsync(query.AppId, query.UserEmail!, cancellationToken)
                ?? throw NoRecordException.ForIdentifier(query.UserEmail!);

        if (dto.AppId != query.AppId)
            throw NoRecordException.ForIdentifier($"{query.InviteCodeId}");

        return new InviteCodeViewModel { InviteCode = dto };
    }
}
