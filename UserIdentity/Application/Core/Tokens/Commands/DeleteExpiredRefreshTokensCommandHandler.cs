using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record DeleteExpiredRefreshTokensCommand : IBaseCommand;

public class DeleteExpiredRefreshTokensCommandHandler(IRefreshTokenRepository refreshTokenRepository, TimeProvider timeProvider)
    : IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteExpiredRefreshTokensCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        int deleted;

        do
        {
            deleted = await _refreshTokenRepository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, cancellationToken);
        } while (deleted == CleanupBatch.Size);

        return new DeleteRecordViewModel();
    }
}
