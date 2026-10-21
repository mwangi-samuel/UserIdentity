using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Settings;
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
  [ValidateNever]
  public RegisteredAppEntity? RegisteredApp { get; internal set; }

  [Required]
  public string UserName { get; init; } = null!;
}

public class ResetPasswordCommandHandler(
    IOptions<PasswordResetSettings> passwordResetSettingsOptions,
    UserManager<IdentityUser> userManager,
    IMachineDateTime machineDateTime,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IPasswordResetRepository passwordResetRepository,
    IEventHandler<PasswordResetEvent> passwordResetEventHandler,
    IConfiguration configuration,
    ILogHelper<ResetPasswordCommandHandler> logHelper,
    IUserRegisteredAppRepository userRegisteredAppRepository
    ) : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(unitOfWork, passwordResetRepository, machineDateTime),
      ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>
{
  private readonly PasswordResetSettings _passwordResetSettings = passwordResetSettingsOptions.Value;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly ITokenFactory _tokenFactory = tokenFactory;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly IEventHandler<PasswordResetEvent> _passwordResetEventHandler = passwordResetEventHandler;
  private readonly IConfiguration _configuration = configuration;
  private readonly ILogHelper<ResetPasswordCommandHandler> _logHelper = logHelper;

  public async Task<ResetPasswordViewModel> CreateItemAsync(ResetPasswordCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var resetPasswordMessage = _configuration.GetEnvironmentVariable("DefaultResetPasswordMessage", "If an account matches the details provided, you will receive a code to reset your password");

    // Same response whether or not the user exists
    var resetRequestId = _tokenFactory.GenerateToken();

    var vm = new ResetPasswordViewModel
    {
      ResetPasswordDetails = new ResetPasswordDTO
      {
        EmailMessage = resetPasswordMessage,
        ResetRequestId = resetRequestId
      }
    };

    var existingUser = await _userManager.FindByNameAsync(command.UserName) ?? await _userManager.FindByEmailAsync(command.UserName);

    if (existingUser is null || !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(existingUser.Id, command.RegisteredApp!.AppName, cancellationToken))
      return vm;

    var latestEntity = await _repository.GetLatestEntityItemAsync(existingUser.Id, cancellationToken);

    if (latestEntity?.CreatedAt > _machineDateTime.Now.AddSeconds(-_passwordResetSettings.RequestCooldownSeconds))
      return vm;

    // Each request gets its own code and attempts, so the requests per day cap the guesses per day
    var requestsInWindow = await _repository.CountEntityItemsCreatedAfterAsync(existingUser.Id, _machineDateTime.Now - PasswordResetSettings.RequestCountWindow, cancellationToken);

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
      Expires = _machineDateTime.Now.AddSeconds(_passwordResetSettings.ValidForSeconds)
    };

    await _repository.RemoveExpiredEntityItemsAsync(existingUser.Id, _machineDateTime.Now - PasswordResetSettings.RequestCountWindow, cancellationToken);

    await CreateEntityItemAsync(passwordResetEntity, existingUser.Id, cancellationToken);

    // Publish Password Reset Event - not awaited
    _ = HandleEventAsync(resetRequestId, userEntity, existingUser, command.RegisteredApp!);

    return vm;
  }

  private async Task HandleEventAsync(string resetRequestId, UserEntity userEntity, IdentityUser identityUser, RegisteredAppEntity registeredAppEntity)
  {
    var eventItem = new PasswordResetEvent
    {
      ResetRequestId = resetRequestId,
      UserContent = new UserEventContent
      {
        UserIdentityId = identityUser.Id,
        FirstName = userEntity.FirstName,
        LastName = userEntity.LastName,
        UserName = identityUser.UserName,
        PhoneNumber = identityUser.PhoneNumber,
        PhoneNumberConfirmed = identityUser.PhoneNumberConfirmed,
        UserEmail = identityUser.Email,
        EmailConfirmed = identityUser.EmailConfirmed
      },
      RegisteredApp = registeredAppEntity
    };

    try
    {
      await _passwordResetEventHandler.HandleEventAsync(eventItem, CancellationToken.None);
    }
    catch (Exception ex)
    {
      _logHelper.LogEvent(LogLevel.Error, ex, "Failed to publish password reset event for user {UserId} of app {AppName}", identityUser.Id, registeredAppEntity.AppName);
    }
  }
}
