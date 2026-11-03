using System.ComponentModel.DataAnnotations;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;

namespace UserIdentity.Application.Core.Users.Commands;

public record VerifyPasswordResetCommand : IBaseCommand
{
  [Required]
  public string ResetRequestId { get; set; } = null!;
}

public class VerifyPasswordResetCommandHandler(
  ITokenFactory tokenFactory,
  IUnitOfWork unitOfWork,
  IPasswordResetRepository passwordResetRepository,
  TimeProvider timeProvider
  ) : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(
    unitOfWork,
    passwordResetRepository,
    timeProvider),
    IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel>
{
  private readonly ITokenFactory _tokenFactory = tokenFactory;

  public async Task<VerifyPasswordResetViewModel> UpdateItemAsync(
    VerifyPasswordResetCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    var passwordResetEntity = new PasswordResetEntity { RequestId = _tokenFactory.HashToken(command.ResetRequestId) };

    var existingEntity = (await Repository.GetEntityByAlternateIdAsync(
      passwordResetEntity,
      QueryCondition.MustExist,
      cancellationToken))!;

    existingEntity.Verified = true;

    await UpdateEntityItemAsync(existingEntity, existingEntity.UserId, cancellationToken);

    return new VerifyPasswordResetViewModel
    {
      VerifyPasswordResetResult = new VerifyPasswordResetDto { PasswordResetVerified = existingEntity.Verified }
    };
  }
}
