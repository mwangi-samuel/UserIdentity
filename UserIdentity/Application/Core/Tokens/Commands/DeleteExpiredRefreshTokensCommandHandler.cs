using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;

using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record DeleteExpiredRefreshTokensCommand : IBaseCommand;

public class DeleteExpiredRefreshTokensCommandHandler(
  IRefreshTokenRepository refreshTokenRepository,
  IMachineDateTime machineDateTime
  ) : IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredRefreshTokensCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var now = _machineDateTime.Now;
    int deleted;

    do
    {
      deleted = await _refreshTokenRepository.DeleteEntityItemsExpiredBeforeAsync(now, BatchSize, cancellationToken);
    } while (deleted == BatchSize);

    return new DeleteRecordViewModel();
  }
}
