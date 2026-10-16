using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ConfirmUpdatePasswordTokenCommand : IBaseCommand
{
  [Required]
  public string ConfirmPasswordToken { get; init; } = null!;

  [Required]
  public string UserId { get; init; } = null!;
}
public class ConfirmUpdatePasswordTokenCommandHandler(
    IUserRepository userRepository
    ) : IUpdateItemCommandHandler<ConfirmUpdatePasswordTokenCommand, ConfirmUpdatePasswordTokenViewModel>
{
  private readonly IUserRepository _userRepository = userRepository;

  public async Task<ConfirmUpdatePasswordTokenViewModel> UpdateItemAsync(ConfirmUpdatePasswordTokenCommand command, string userId, CancellationToken cancellationToken = default)
  {
    try
    {
      var rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(command.ConfirmPasswordToken));

      var userEntity = new UserEntity
      {
        Id = command.UserId,
        ForgotPasswordToken = rawToken,
      };

      var existingEntity = await _userRepository.GetEntityByAlternateIdAsync(userEntity, QueryCondition.MAY_OR_MAY_NOT_EXIST, cancellationToken);

      return new ConfirmUpdatePasswordTokenViewModel
      {
        TokenPasswordResult = new ConfirmUpdatePasswordDTO
        {
          UpdatePasswordTokenConfirmed = existingEntity is not null,
        }
      };

    }
    catch
    {
      return new ConfirmUpdatePasswordTokenViewModel
      {
        TokenPasswordResult = new ConfirmUpdatePasswordDTO
        {
          UpdatePasswordTokenConfirmed = false
        }
      };

    }
  }
}

