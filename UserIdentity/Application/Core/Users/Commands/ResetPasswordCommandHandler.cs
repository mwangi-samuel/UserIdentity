using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ResetPasswordCommand : IBaseCommand
{
  [JsonIgnore]
  public RegisteredAppEntity? RegisteredApp { get; internal set; }

  [Required]
  public string UserName { get; init; } = null!;
}

public class ResetPasswordCommandHandler(
  IOptions<PasswordResetSettings> passwordResetSettingsOptions,
  UserManager<IdentityUser> userManager,
  TimeProvider timeProvider,
  ITokenFactory tokenFactory,
  IUnitOfWork unitOfWork,
  IUserRepository userRepository,
  IPasswordResetRepository passwordResetRepository,
  IMessageOutbox messageOutbox,
  IUserRegisteredAppRepository userRegisteredAppRepository
  ) : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(
      unitOfWork,
      passwordResetRepository,
      timeProvider),
      ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>
{
  private readonly PasswordResetSettings _passwordResetSettings = passwordResetSettingsOptions.Value;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly ITokenFactory _tokenFactory = tokenFactory;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly IMessageOutbox _messageOutbox = messageOutbox;

  public const string DefaultResetPasswordMessage =
    "If an account matches the details provided, you will receive a code to reset your password.";

  public async Task<ResetPasswordViewModel> CreateItemAsync(
    ResetPasswordCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    // Same response whether or not the user exists
    var resetRequestId = _tokenFactory.GenerateToken();

    var vm = new ResetPasswordViewModel
    {
      ResetPasswordDetails = new ResetPasswordDto
      {
        EmailMessage = DefaultResetPasswordMessage,
        ResetRequestId = resetRequestId
      }
    };

    var existingUser = await _userManager.FindByNameAsync(command.UserName)
      ?? await _userManager.FindByEmailAsync(command.UserName);

    if (existingUser is null
      || !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(
        existingUser.Id,
        command.RegisteredApp!.AppName,
        cancellationToken))
      return vm;

    var latestEntity = await Repository.GetLatestEntityItemAsync(existingUser.Id, cancellationToken);

    if (latestEntity?.CreatedAt > TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(
      -_passwordResetSettings.RequestCooldownSeconds))
      return vm;

    // Each request gets its own code and attempts, so the requests per day cap the guesses per day
    var requestsInWindow = await Repository.CountEntityItemsCreatedAfterAsync(
      existingUser.Id,
      TimeProvider.GetUtcNow().UtcDateTime - PasswordResetSettings.RequestCountWindow,
      cancellationToken);

    if (requestsInWindow >= _passwordResetSettings.MaxRequestsPerDay)
      return vm;

    UserEntity userEntity;

    try
    {
      userEntity = await _userRepository.GetEntityItemAsync(existingUser.Id, cancellationToken);
    }
    catch (NoRecordException)
    {
      return vm;
    }

    var passwordResetEntity = new PasswordResetEntity
    {
      RequestId = _tokenFactory.HashToken(resetRequestId),
      UserId = existingUser.Id,
      Expires = TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(_passwordResetSettings.ValidForSeconds)
    };

    _messageOutbox.Add(
      UserMessages.UserUpdated,
      UserMessages.PasswordReset(resetRequestId, existingUser, userEntity, command.RegisteredApp!.AppName));

    await CreateEntityItemAsync(passwordResetEntity, existingUser.Id, cancellationToken);

    return vm;
  }
}
