using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
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
    TimeProvider timeProvider
) : IDeleteItemCommandHandler<DeleteUnconfirmedUsersCommand, DeleteRecordViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    private readonly CleanupSettings _cleanupSettings = cleanupSettings.Value;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteUnconfirmedUsersCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        // Without required confirmation, unconfirmed accounts can log in and must stay
        if (!_identityOptions.SignIn.RequireConfirmedAccount)
            return new DeleteRecordViewModel();

        var createdBefore = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_cleanupSettings.UnconfirmedUserLifetimeInDays);
        List<UserEntity> entities;

        do
        {
            entities = await _userRepository.GetUnconfirmedEntityItemsCreatedBeforeAsync(
                createdBefore,
                CleanupBatch.Size,
                cancellationToken
            );

            foreach (var entity in entities)
                await DeleteUserAsync(entity, userId, cancellationToken);
        } while (entities.Count == CleanupBatch.Size);

        return new DeleteRecordViewModel();
    }

    // One transaction per user, so a failure leaves the other users' deletes in place
    private async Task DeleteUserAsync(UserEntity entity, string userId, CancellationToken cancellationToken)
    {
        var identityUser = await _userManager.FindByIdAsync(entity.Id);

        if (identityUser is null || AccountConfirmation.IsConfirmed(_identityOptions, identityUser))
            return;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var inviteCode in await _inviteCodeRepository.GetAppliedEntityItemsForUserAsync(entity.Id, cancellationToken))
            {
                inviteCode.Applied = false;
                inviteCode.UpdateEntityAuditFields(userId, _timeProvider.GetUtcNow().UtcDateTime);

                _inviteCodeRepository.UpdateEntityItem(inviteCode);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var result = await _userManager.DeleteAsync(identityUser);

            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                _userRepository.ClearTrackedEntities();

                return;
            }

            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
