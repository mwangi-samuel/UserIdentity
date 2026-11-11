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
            KeySetList = new JsonWebKeySetDto
            {
                Keys =
                [
                    new JsonWebKeyDto
                    {
                        Algorithm = "Ed25519",
                        KeyType = "OKP",
                        KeyId = "identity-key-id-1",
                        Curve = "Ed25519",
                        PublicKey = "public-key",
                    },
                ],
            },
        };

        A.CallTo(() => _getKeySetsQueryHandler.GetItemsAsync(new GetKeySetsQuery { }, A<CancellationToken>._))
            .Returns(Task.FromResult(keySetsViewModel));

        // Act
        var result = await JwksEndpoints.GetKeySetsAsync(_getKeySetsQueryHandler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(keySetsViewModel.KeySetList, result.Value);
    }
}
