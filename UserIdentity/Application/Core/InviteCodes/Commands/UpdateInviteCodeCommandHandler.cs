using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Commands;

public class UpdateInviteCodeCommand : IBaseCommand
{
  public long InviteCodeId { get; set; }

  public bool Applied { get; set; }
}

public class UpdateInviteCodeCommandHandler(
   IUnitOfWork unitOfWork,
   IInviteCodeRepository inviteCodeRepository,
   IMachineDateTime machineDateTime
  ) : BaseAuditableItemCommandHandler<long, InviteCodeEntity, IInviteCodeRepository>(unitOfWork, inviteCodeRepository, machineDateTime),
     IUpdateItemCommandHandler<UpdateInviteCodeCommand, InviteCodeViewModel>
{
  public async Task<InviteCodeViewModel> UpdateItemAsync(UpdateInviteCodeCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var entity = await _repository.GetEntityItemAsync(command.InviteCodeId, cancellationToken);

    entity.Applied = command.Applied;

    await UpdateEntityItemAsync(entity, userId, cancellationToken);

    return new InviteCodeViewModel
    {
      InviteCode = await _repository.GetEntityDtoByIdAsync(command.InviteCodeId, cancellationToken)
    };
  }
}
