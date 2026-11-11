using PolyzenKit.Common.Paging;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Repositories.InviteCodes;

public interface IInviteCodeRepository
    : IEntityRepository<InviteCodeEntity, long>,
        IItemEntityRepository<InviteCodeEntity, long>,
        IItemEntityDtoRepository<InviteCodeDto, long>
{
    Task<InviteCodeDto?> GetEntityDtoByEmailAsync(int appId, string userEmail, CancellationToken cancellationToken = default);

    Task<List<InviteCodeEntity>> GetUnusedEntityItemsByRoleAsync(string role, CancellationToken cancellationToken = default);

    Task<List<InviteCodeEntity>> GetAppliedEntityItemsForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<PagedList<InviteCodeDto>> SearchEntityDtosByAppIdAsync(
        int appId,
        string? search,
        bool? applied,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );
}
