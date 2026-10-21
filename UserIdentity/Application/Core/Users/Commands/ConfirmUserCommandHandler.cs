using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common;

namespace UserIdentity.Application.Core.Users.Commands;

public record ConfirmUserCommand : IBaseCommand
{
  [Required]
  public string UserId { get; set; } = null!;
}

public class ConfirmUserCommandHandler(
  UserManager<IdentityUser> userManager
  ) : IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel>
{
  private readonly UserManager<IdentityUser> _userManager = userManager;

  public async Task<ConfirmUserViewModel> UpdateItemAsync(ConfirmUserCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var userDetails = await _userManager.FindByIdAsync(command.UserId) ?? throw new NoRecordException(command.UserId, EntityTypes.USER.Description());

    userDetails.EmailConfirmed = true;

    var confirmResult = await _userManager.UpdateAsync(userDetails);

    if (!confirmResult.Succeeded)
      throw new RecordUpdateException(string.Join(", ", confirmResult.Errors.Select(e => e.Description)));

    return new ConfirmUserViewModel
    {
      ConfirmUserResult = new ConfirmUserDTO
      {
        UserConfirmed = confirmResult.Succeeded
      }
    };
  }
}
