using System.Text.RegularExpressions;

using Xunit;

namespace UserIdentity.UnitTests.Architecture;

public class LayerDependencyTests
{
  private static string ServiceDirectory()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UserIdentity.slnx")))
      directory = directory.Parent;

    return Path.Combine((directory ?? throw new DirectoryNotFoundException("UserIdentity.slnx")).FullName, "UserIdentity");
  }

  [Theory]
  [InlineData("Common", @"UserIdentity\.(Application|Domain|Infrastructure|Persistence|Presentation)|PolyzenKit\.(Application|Infrastructure|Persistence|Presentation)|Microsoft\.(EntityFrameworkCore|AspNetCore)")]
  [InlineData("Domain", @"UserIdentity\.(Application|Infrastructure|Persistence|Presentation)|PolyzenKit\.(Application|Infrastructure|Persistence|Presentation)|Microsoft\.(EntityFrameworkCore|AspNetCore)")]
  // Handlers can raise concurrency conflicts; EF queries and contexts stay in Persistence.
  [InlineData("Application", @"UserIdentity\.(Infrastructure|Presentation)|PolyzenKit\.(Infrastructure|Presentation)|Microsoft\.EntityFrameworkCore(?!\.DbUpdateConcurrencyException\b)|\bAppDbContext\b|\bnew\s+\w+Repository\s*\(")]
  [InlineData("Persistence", @"UserIdentity\.(Infrastructure|Presentation)")]
  [InlineData("Infrastructure", @"UserIdentity\.(Persistence|Presentation)")]
  [InlineData("Presentation", @"UserIdentity\.(Infrastructure|Persistence)")]
  public void Layers_KeepTheirDeclaredDependencies(string layer, string forbidden)
  {
    // Arrange
    var migrations = Path.Combine(ServiceDirectory(), "Persistence", "Migrations") + Path.DirectorySeparatorChar;
    var files = Directory.GetFiles(Path.Combine(ServiceDirectory(), layer), "*.cs", SearchOption.AllDirectories)
      .Where(file => !file.StartsWith(migrations, StringComparison.OrdinalIgnoreCase));

    // Act
    var violations = files.Where(file => Regex.IsMatch(File.ReadAllText(file), forbidden)).ToList();

    // Assert
    Assert.Empty(violations);
  }
}
