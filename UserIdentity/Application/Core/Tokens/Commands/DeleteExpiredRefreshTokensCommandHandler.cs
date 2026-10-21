using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record DeleteExpiredRefreshTokensCommand : IBaseCommand;

public class DeleteExpiredRefreshTokensCommandHandler(
  IRefreshTokenRepository refreshTokenRepository,
  IUnitOfWork unitOfWork,
  IMachineDateTime machineDateTime
  ) : IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteExpiredRefreshTokensCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var now = _machineDateTime.Now;
    List<RefreshTokenEntity> entities;

    do
    {
      entities = await _refreshTokenRepository.GetEntityItemsExpiredBeforeAsync(now, BatchSize, cancellationToken);

      foreach (var entity in entities)
        _refreshTokenRepository.RemoveEntityItem(entity);

      await _unitOfWork.SaveChangesAsync(cancellationToken);
    } while (entities.Count == BatchSize);

    return new DeleteRecordViewModel();
  }
}
