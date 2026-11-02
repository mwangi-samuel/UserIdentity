using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Queries;

public record GetUserQuery : IBaseQuery
{
  public required string UserId { get; init; }

  public string? AppName { get; init; }
}

public class GetUserQueryHandler(
    IUserRepository userRepository
    ) : IGetItemQueryHandler<GetUserQuery, UserViewModel>
{
  private readonly IUserRepository _userRepository = userRepository;

  public async Task<UserViewModel> GetItemAsync(GetUserQuery query, CancellationToken cancellationToken = default)
  {
    var user = await _userRepository.GetUserDtoAsync(query.UserId, query.AppName, cancellationToken)
      ?? throw NoRecordException.ForIdentifier(query.UserId);

    return new UserViewModel
    {
      User = user
    };
  }
}
