using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;

using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.PasswordResets;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteExpiredPasswordResetsCommand : IBaseCommand;

public class DeleteExpiredPasswordResetsCommandHandler(
  IPasswordResetRepository passwordResetRepository,
  IMachineDateTime machineDateTime
  ) : IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredPasswordResetsCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var expiredBefore = _machineDateTime.Now - PasswordResetSettings.RequestCountWindow;
    int deleted;

    do
    {
      deleted = await _passwordResetRepository.DeleteEntityItemsExpiredBeforeAsync(expiredBefore, BatchSize, cancellationToken);
    } while (deleted == BatchSize);

    return new DeleteRecordViewModel();
  }
}
