using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.PasswordResets;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteExpiredPasswordResetsCommand : IBaseCommand;

public class DeleteExpiredPasswordResetsCommandHandler(
  IPasswordResetRepository passwordResetRepository,
  TimeProvider timeProvider
  ) : IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredPasswordResetsCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var expiredBefore = _timeProvider.GetUtcNow().UtcDateTime - PasswordResetSettings.RequestCountWindow;
    int deleted;

    do
    {
      deleted = await _passwordResetRepository.DeleteEntityItemsExpiredBeforeAsync(expiredBefore, BatchSize, cancellationToken);
    } while (deleted == BatchSize);

    return new DeleteRecordViewModel();
  }
}
