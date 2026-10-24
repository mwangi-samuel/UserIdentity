using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Repositories.InviteCodes;

public interface IInviteCodeRepository
  : IEntityRepository<InviteCodeEntity, long>,
  IItemEntityRepository<InviteCodeEntity, long>,
  IItemEntityDtoRepository<InviteCodeDto, long>
{
}
