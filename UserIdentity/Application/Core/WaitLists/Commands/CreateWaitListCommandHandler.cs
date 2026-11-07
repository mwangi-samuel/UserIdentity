using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Repositories.WaitLists;

namespace UserIdentity.Application.Core.WaitLists.Commands;

public record CreateWaitListCommand : IBaseCommand
{
    [
        Required,
        StringLength(WaitListLimits.UserEmailMaxLength),
        RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)
    ]
    public string UserEmail { get; init; } = null!;

    [JsonIgnore]
    public int AppId { get; internal set; }
}

public class CreateWaitListCommandHandler(IUnitOfWork unitOfWork, IWaitListRepository waitListRepository, TimeProvider timeProvider)
    : BaseAuditableItemCommandHandler<long, WaitListEntity, IWaitListRepository>(unitOfWork, waitListRepository, timeProvider),
        ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel>
{
    public async Task<WaitListViewModel> CreateItemAsync(
        CreateWaitListCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new WaitListEntity { UserEmail = command.UserEmail, AppId = command.AppId };

        await CreateEntityItemAsync(entity, userId, cancellationToken);

        return new WaitListViewModel { WaitList = await Repository.GetEntityDtoByIdAsync(entity.Id, cancellationToken) };
    }
}
