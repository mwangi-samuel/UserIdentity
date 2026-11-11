using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Persistence.Repositories.WaitLists;

namespace UserIdentity.Application.Core.WaitLists.Queries;

public record GetWaitListQuery : IBaseQuery
{
    public long? WaitListId { get; init; }

    public string? UserEmail { get; init; }

    public required int AppId { get; init; }
}

public class GetWaitListQueryHandler(IWaitListRepository waitListRepository) : IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel>
{
    private readonly IWaitListRepository _waitListRepository = waitListRepository;

    public async Task<WaitListViewModel> GetItemAsync(GetWaitListQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.WaitListId.HasValue
            ? await _waitListRepository.GetEntityDtoByIdAsync(query.WaitListId!.Value, cancellationToken)
            : await _waitListRepository.GetEntityDtoByEmailAsync(query.AppId, query.UserEmail!, cancellationToken)
                ?? throw NoRecordException.ForIdentifier(query.UserEmail!);

        if (dto.AppId != query.AppId)
            throw NoRecordException.ForIdentifier($"{query.WaitListId}");

        return new WaitListViewModel { WaitList = dto };
    }
}
