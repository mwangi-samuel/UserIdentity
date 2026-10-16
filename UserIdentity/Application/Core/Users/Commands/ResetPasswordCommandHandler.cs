using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entity;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ResetPasswordCommand : IBaseCommand
{
  [Required]
  [EmailAddress]
  public string UserEmail { get; init; } = null!;
}

public class ResetPasswordCommandHandler(
    UserManager<IdentityUser> userManager,
    IMachineDateTime machineDateTime,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IConfiguration configuration
    ) : ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>
{

  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IConfiguration _configuration = configuration;

  public async Task<ResetPasswordViewModel> CreateItemAsync(ResetPasswordCommand command, string userId, CancellationToken cancellationToken = default)
  {

    var resetPasswordMessage = _configuration.GetEnvironmentVariable("DefaultResetPasswordMessage", "Kindly check your email for instructions to reset your password");

    var vm = new ResetPasswordViewModel
    {
      ResetPasswordDetails = new ResetPasswordDTO
      {
        EmailMessage = resetPasswordMessage
      }
    };

    var existingUser = await _userManager.FindByEmailAsync(command.UserEmail);

    if (existingUser is null)
      return vm;

    // generate token
    var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);

    // update user details record
    var existingEntity = await _userRepository.GetEntityItemAsync(existingUser.Id, cancellationToken);

    existingEntity.ForgotPasswordToken = resetPasswordToken;

    existingEntity.UpdateEntityAuditFields(userId, _machineDateTime.Now);

    _userRepository.UpdateEntityItem(existingEntity);

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var emailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetPasswordToken));

    // Send Email Logic here

    return vm;

  }
}
