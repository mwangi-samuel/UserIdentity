using System.Runtime.CompilerServices;

// Expose internals to unit tests & integration tests
[assembly: InternalsVisibleTo("UserIdentity.UnitTests")]
[assembly: InternalsVisibleTo("UserIdentity.IntegrationTests")]

