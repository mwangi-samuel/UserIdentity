using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteExpiredPasswordResetsCommand : IBaseCommand;

public class DeleteExpiredPasswordResetsCommandHandler(
  IPasswordResetRepository passwordResetRepository,
  IUnitOfWork unitOfWork,
  IMachineDateTime machineDateTime
  ) : IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredPasswordResetsCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var now = _machineDateTime.Now;
    List<PasswordResetEntity> entities;

    do
    {
      entities = await _passwordResetRepository.GetEntityItemsExpiredBeforeAsync(now, BatchSize, cancellationToken);

      foreach (var entity in entities)
        _passwordResetRepository.RemoveEntityItem(entity);

      await _unitOfWork.SaveChangesAsync(cancellationToken);
    } while (entities.Count == BatchSize);

    return new DeleteRecordViewModel();
  }
}
