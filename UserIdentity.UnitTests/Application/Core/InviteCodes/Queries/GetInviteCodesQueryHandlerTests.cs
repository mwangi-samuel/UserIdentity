using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Common.Paging;

using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Queries;

public class GetInviteCodesQueryHandlerTests
{
  private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();

  [Fact]
  public async Task GetItemsAsync_Returns_The_Apps_Page_Of_InviteCodes()
  {
    // Arrange
    var dto = new InviteCodeDto { Id = 3, InviteCode = "INVITE", UserEmail = "owner@example.com", AppId = 1, Role = "testapp:owner" };

    A.CallTo(() => _inviteCodeRepository.GetEntityDtosByAppIdAsync(1, 2, 10, A<CancellationToken>._))
        .Returns(new PagedList<InviteCodeDto>([dto], 11));

    var handler = new GetInviteCodesQueryHandler(_inviteCodeRepository);

    // Act
    var result = await handler.GetItemsAsync(new GetInviteCodesQuery { AppId = 1, Page = 2, PageSize = 10 }, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(11, result.TotalCount);
    Assert.Equal("testapp:owner", Assert.Single(result.InviteCodes).Role);
  }
}
