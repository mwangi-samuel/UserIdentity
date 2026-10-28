using System.Collections.Generic;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.KeySets.Queries;
using PolyzenKit.Application.Core.KeySets.ViewModels;

using UserIdentity.Presentation.Controllers;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Controllers;

public class JwksControllerTests
{
  private readonly IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel> _getKeySetsQueryHandler;
  public JwksControllerTests()
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

    var jwksController = new JwksController(_getKeySetsQueryHandler);

    A.CallTo(() => _getKeySetsQueryHandler.GetItemsAsync(new GetKeySetsQuery { })).Returns(Task.FromResult(keySetsViewModel));

    var actionResult = await jwksController.GetKeySetsAsync();

    // Act
    var result = actionResult as ObjectResult;
    var kvp = result?.Value as Dictionary<string, IList<Dictionary<string, string>>>;

    // Assert
    Assert.NotNull(result);
    Assert.IsType<Dictionary<string, IList<Dictionary<string, string>>>>(result?.Value);
    Assert.NotNull(kvp?["keys"]);

    Assert.Equal(kvp?["keys"]?[0]["alg"], keySetsViewModel.KeySetList["keys"]?[0]["alg"]);
    Assert.Equal(kvp?["keys"]?[0]["kty"], keySetsViewModel.KeySetList["keys"]?[0]["kty"]);
  }
}

