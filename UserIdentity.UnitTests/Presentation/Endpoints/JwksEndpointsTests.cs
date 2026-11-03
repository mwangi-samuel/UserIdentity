using FakeItEasy;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.KeySets.Queries;
using PolyzenKit.Application.Core.KeySets.ViewModels;

using UserIdentity.Presentation.Endpoints;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class JwksEndpointsTests
{
  private readonly IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel> _getKeySetsQueryHandler;

  public JwksEndpointsTests()
  {
    _getKeySetsQueryHandler = A.Fake<IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel>>();
  }

  [Fact]
  public async Task Get_KeySets_Returns_KeySets()
  {
    // Arrange
    var keySetsViewModel = new KeySetsViewModel
    {
      KeySetList = new Dictionary<string, IList<Dictionary<string, string>>>
      {
        ["keys"] =
        [
          new Dictionary<string, string>
          {
            ["alg"] = "EdDSA",
            ["kty"] = "OKP"
          }
        ]
      }
    };

    A.CallTo(() => _getKeySetsQueryHandler.GetItemsAsync(new GetKeySetsQuery { }, A<CancellationToken>._))
      .Returns(Task.FromResult(keySetsViewModel));

    // Act
    var result = await JwksEndpoints.GetKeySetsAsync(_getKeySetsQueryHandler, TestContext.Current.CancellationToken);
    var kvp = result.Value;

    // Assert
    Assert.IsType<Dictionary<string, IList<Dictionary<string, string>>>>(kvp);
    Assert.NotNull(kvp?["keys"]);

    Assert.Equal(kvp?["keys"]?[0]["alg"], keySetsViewModel.KeySetList["keys"]?[0]["alg"]);
    Assert.Equal(kvp?["keys"]?[0]["kty"], keySetsViewModel.KeySetList["keys"]?[0]["kty"]);
  }
}
