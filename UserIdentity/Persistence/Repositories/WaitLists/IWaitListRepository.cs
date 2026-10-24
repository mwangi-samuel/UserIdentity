using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.WaitLists;

namespace UserIdentity.Persistence.Repositories.WaitLists;

public interface IWaitListRepository
  : IEntityRepository<WaitListEntity, long>,
  IItemEntityRepository<WaitListEntity, long>,
  IItemEntityDtoRepository<WaitListDto, long>
{
}
