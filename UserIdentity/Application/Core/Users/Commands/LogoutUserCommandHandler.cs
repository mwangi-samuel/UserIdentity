using System.ComponentModel.DataAnnotations;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Users.Commands;

public record LogoutUserCommand : IBaseCommand
{
  [Required]
  public string RefreshToken { get; init; } = null!;
}

public class LogoutUserCommandHandler(
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IRefreshTokenRepository refreshTokenRepository
    ) : IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel>
{
  private readonly ITokenFactory _tokenFactory = tokenFactory;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(LogoutUserCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var tokenEntity = new RefreshTokenEntity
    {
      UserId = userId,
      Token = _tokenFactory.HashToken(command.RefreshToken)
    };

    var existingEntity = await _refreshTokenRepository.GetEntityByAlternateIdAsync(tokenEntity, QueryCondition.MAY_OR_MAY_NOT_EXIST, cancellationToken);

    if (existingEntity is not null)
    {
      _refreshTokenRepository.RemoveEntityItem(existingEntity);

      await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    return new DeleteRecordViewModel();
  }
}
