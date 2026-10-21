using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteUnconfirmedUsersCommand : IBaseCommand;

public class DeleteUnconfirmedUsersCommandHandler(
  UserManager<IdentityUser> userManager,
  IOptions<IdentityOptions> identityOptions,
  IOptions<CleanupSettings> cleanupSettings,
  IUnitOfWork unitOfWork,
  IUserRepository userRepository,
  IInviteCodeRepository inviteCodeRepository,
  IRefreshTokenRepository refreshTokenRepository,
  IPasswordResetRepository passwordResetRepository,
  IMachineDateTime machineDateTime
  ) : IDeleteItemCommandHandler<DeleteUnconfirmedUsersCommand, DeleteRecordViewModel>
{
  public const int BatchSize = 100;

  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IdentityOptions _identityOptions = identityOptions.Value;
  private readonly CleanupSettings _cleanupSettings = cleanupSettings.Value;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteUnconfirmedUsersCommand command, string userId, CancellationToken cancellationToken = default)
  {
    // Without required confirmation, unconfirmed accounts can log in and must stay
    if (!_identityOptions.SignIn.RequireConfirmedAccount)
      return new DeleteRecordViewModel();

    var createdBefore = _machineDateTime.Now.AddDays(-_cleanupSettings.UnconfirmedUserLifetimeInDays);
    List<UserEntity> entities;

    do
    {
      entities = await _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(createdBefore, BatchSize, cancellationToken);

      foreach (var entity in entities)
        await DeleteUserAsync(entity, cancellationToken);
    } while (entities.Count == BatchSize);

    return new DeleteRecordViewModel();
  }

  private async Task DeleteUserAsync(UserEntity entity, CancellationToken cancellationToken)
  {
    var identityUser = await _userManager.FindByIdAsync(entity.Id);

    await _unitOfWork.BeginTransactionAsync(cancellationToken);

    try
    {
      if (identityUser?.Email is not null)
      {
        var inviteCode = await _inviteCodeRepository.GetEntityByAlternateIdAsync(new InviteCodeEntity { UserEmail = identityUser.Email }, QueryCondition.MAY_OR_MAY_NOT_EXIST, cancellationToken);

        if (inviteCode is { Applied: true })
        {
          inviteCode.Applied = false;
          _inviteCodeRepository.UpdateEntityItem(inviteCode);
        }
      }

      await _refreshTokenRepository.RemoveEntityItemsAsync(entity.Id, cancellationToken);
      await _passwordResetRepository.RemoveEntityItemsAsync(entity.Id, cancellationToken);
      _userRepository.RemoveEntityItem(entity);

      await _unitOfWork.SaveChangesAsync(cancellationToken);

      if (identityUser is not null)
      {
        var result = await _userManager.DeleteAsync(identityUser);

        if (!result.Succeeded)
          throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
      }

      await _unitOfWork.CommitTransactionAsync(cancellationToken);
    }
    catch
    {
      await _unitOfWork.RollbackTransactionAsync(cancellationToken);
      throw;
    }
  }
}
