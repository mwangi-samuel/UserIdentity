using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Repositories.WaitLists;

namespace UserIdentity.Application.Core.WaitLists.Commands;

public class CreateWaitListCommand : WaitListEntity, IBaseCommand
{

}

public class CreateWaitListCommandHandler(
   IUnitOfWork unitOfWork,
   IWaitListRepository waitListRepository,
   IMachineDateTime machineDateTime
  ) : BaseAuditableItemCommandHandler<long, WaitListEntity, IWaitListRepository>(unitOfWork, waitListRepository, machineDateTime),
      ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel>
{
  public async Task<WaitListViewModel> CreateItemAsync(CreateWaitListCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var entity = (WaitListEntity)command;

    await CreateEntityItemAsync(entity, userId, cancellationToken);

    return new WaitListViewModel
    {
      WaitList = await _repository.GetEntityDtoByIdAsync(entity.Id, cancellationToken)
    };
  }
}
