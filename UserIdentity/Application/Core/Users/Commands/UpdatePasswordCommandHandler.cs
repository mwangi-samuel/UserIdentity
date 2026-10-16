using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record UpdatePasswordCommand : IBaseCommand
{
  [Required]
  public string NewPassword { get; init; } = null!;

  [Required]
  public string UserId { get; init; } = null!;

  [Required]
  public string PasswordResetToken { get; init; } = null!;
}

public class UpdatePasswordCommandHandler(
    UserManager<IdentityUser> userManager
    ) : IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel>
{
  private readonly UserManager<IdentityUser> _userManager = userManager;

  public async Task<UpdatePasswordViewModel> UpdateItemAsync(UpdatePasswordCommand command, string userId, CancellationToken cancellationToken = default)
  {
    try
    {

      var userDetails = await _userManager.FindByIdAsync(command.UserId) ?? throw new NoRecordException(command.UserId + "", "User");

      var rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(command.PasswordResetToken));

      var reSetPassWordTokenresult = await _userManager.ResetPasswordAsync(userDetails, rawToken, command.NewPassword);

      return new UpdatePasswordViewModel
      {
        UpdatePasswordResult = new UpdatePasswordDTO
        {
          PassWordUpdated = reSetPassWordTokenresult.Succeeded
        }
      };

    }
    catch (Exception)
    {
      return new UpdatePasswordViewModel
      {
        UpdatePasswordResult = new UpdatePasswordDTO
        {
          PassWordUpdated = false
        }
      };

    }
  }
}

