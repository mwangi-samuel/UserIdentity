using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record DeleteExpiredRefreshTokensCommand : IBaseCommand;

public class DeleteExpiredRefreshTokensCommandHandler(
  IRefreshTokenRepository refreshTokenRepository,
  TimeProvider timeProvider
  ) : IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredRefreshTokensCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var now = _timeProvider.GetUtcNow().UtcDateTime;
    int deleted;

    do
    {
      deleted = await _refreshTokenRepository.DeleteEntityItemsExpiredBeforeAsync(now, BatchSize, cancellationToken);
    } while (deleted == BatchSize);

    return new DeleteRecordViewModel();
  }
}
