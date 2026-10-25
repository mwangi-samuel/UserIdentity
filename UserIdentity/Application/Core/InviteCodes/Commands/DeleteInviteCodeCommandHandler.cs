using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Common;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Commands;

public record DeleteInviteCodeCommand : IBaseCommand
{
  public required long InviteCodeId { get; init; }

  public required int AppId { get; init; }
}

public class DeleteInviteCodeCommandHandler(
  IUnitOfWork unitOfWork,
  IInviteCodeRepository inviteCodeRepository
  ) : IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel>
{
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteInviteCodeCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var entity = await _inviteCodeRepository.GetEntityItemAsync(command.InviteCodeId, cancellationToken);

    if (entity.AppId != command.AppId)
      throw new NoRecordException($"{command.InviteCodeId}", EntityTypes.INVITE_CODE.Description());

    if (entity.Applied)
      throw new InvalidDataException("An invite code that has been used can't be revoked");

    _inviteCodeRepository.RemoveEntityItem(entity);

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return new DeleteRecordViewModel();
  }
}
