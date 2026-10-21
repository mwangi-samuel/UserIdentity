using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Users.Commands;

public record UpdatePasswordCommand : IBaseCommand
{
  [Required]
  public string ResetRequestId { get; init; } = null!;

  [Required]
  public string NewPassword { get; init; } = null!;
}

public class UpdatePasswordCommandHandler(
    UserManager<IdentityUser> userManager,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IPasswordResetRepository passwordResetRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IMachineDateTime machineDateTime
    ) : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(unitOfWork, passwordResetRepository, machineDateTime),
      IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel>
{
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly ITokenFactory _tokenFactory = tokenFactory;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

  public async Task<UpdatePasswordViewModel> UpdateItemAsync(UpdatePasswordCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var passwordResetEntity = new PasswordResetEntity
    {
      RequestId = _tokenFactory.HashToken(command.ResetRequestId)
    };

    var existingEntity = await _repository.GetEntityByAlternateIdAsync(passwordResetEntity, QueryCondition.MAY_OR_MAY_NOT_EXIST, cancellationToken);

    if (existingEntity is null || !existingEntity.Verified || existingEntity.Applied || existingEntity.Expires <= _machineDateTime.Now)
      return GetUpdatePasswordViewModel(false);

    var userDetails = await _userManager.FindByIdAsync(existingEntity.UserId);

    if (userDetails is null)
      return GetUpdatePasswordViewModel(false);

    // Start DB Transaction
    await _unitOfWork.BeginTransactionAsync(cancellationToken);

    try
    {
      var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(userDetails);

      var resetPasswordResult = await _userManager.ResetPasswordAsync(userDetails, resetPasswordToken, command.NewPassword);

      if (!resetPasswordResult.Succeeded)
        throw new InvalidDataException(string.Join(" ", resetPasswordResult.Errors.Select(e => e.Description)));

      existingEntity.Applied = true;

      await UpdateEntityItemAsync(existingEntity, userDetails.Id, cancellationToken);

      await _refreshTokenRepository.RemoveEntityItemsAsync(userDetails.Id, cancellationToken);

      await _userManager.SetLockoutEndDateAsync(userDetails, null);
      await _userManager.ResetAccessFailedCountAsync(userDetails);

      // Commit DB Transaction
      await _unitOfWork.CommitTransactionAsync(cancellationToken);
    }
    catch
    {
      await _unitOfWork.RollbackTransactionAsync();
      throw;
    }

    return GetUpdatePasswordViewModel(true);
  }

  private static UpdatePasswordViewModel GetUpdatePasswordViewModel(bool passwordUpdated)
  {
    return new UpdatePasswordViewModel
    {
      UpdatePasswordResult = new UpdatePasswordDTO
      {
        PassWordUpdated = passwordUpdated
      }
    };
  }
}
