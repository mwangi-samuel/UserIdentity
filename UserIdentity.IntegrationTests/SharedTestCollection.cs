using Xunit;

namespace UserIdentity.IntegrationTests;

[CollectionDefinition("Integration Tests")]
public class SharedTestCollection : ICollectionFixture<TestingWebAppFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
