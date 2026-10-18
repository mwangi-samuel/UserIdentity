using System.ComponentModel.DataAnnotations;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
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
  IMachineDateTime machineDateTime
  ) : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(unitOfWork, passwordResetRepository, machineDateTime),
    IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel>
{
  private readonly ITokenFactory _tokenFactory = tokenFactory;

  public async Task<VerifyPasswordResetViewModel> UpdateItemAsync(VerifyPasswordResetCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var passwordResetEntity = new PasswordResetEntity
    {
      RequestId = _tokenFactory.HashToken(command.ResetRequestId)
    };

    var existingEntity = (await _repository.GetEntityByAlternateIdAsync(passwordResetEntity, QueryCondition.MUST_EXIST, cancellationToken))!;

    existingEntity.Verified = true;

    await UpdateEntityItemAsync(existingEntity, existingEntity.UserId, cancellationToken);

    return new VerifyPasswordResetViewModel
    {
      VerifyPasswordResetResult = new VerifyPasswordResetDTO
      {
        PasswordResetVerified = existingEntity.Verified
      }
    };
  }
}
