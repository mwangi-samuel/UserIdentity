using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record RecordFailedSignInCommand(IdentityUser User, bool SecondStep) : IBaseCommand;

public class RecordFailedSignInCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel> createAccountLimitEventCommandHandler
) : ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = createAccountLimitEventCommandHandler;

    public async Task<AccountLimitViewModel> CreateItemAsync(
        RecordFailedSignInCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        await RecordFailedAccessAsync(command.User);

        if (!command.SecondStep)
            return new AccountLimitViewModel();

        var limit = await _createAccountLimitEventCommandHandler.CreateItemAsync(
            new CreateAccountLimitEventCommand(command.User.Id, AccountLimitKind.FailedSecondStep),
            userId,
            cancellationToken
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return limit;
    }

    private async Task RecordFailedAccessAsync(IdentityUser user)
    {
        var result = await _userManager.AccessFailedAsync(user);

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            _userRepository.ClearTrackedEntities();

            var reloadedUser = await _userManager.FindByIdAsync(user.Id);

            if (reloadedUser is null)
                return;

            result = await _userManager.AccessFailedAsync(reloadedUser);
        }

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
