using System.Runtime.CompilerServices;

namespace Ufw.Web.Tests.Architecture;

[TestClass]
public sealed class PersistenceBoundaryTests
{
    [TestMethod]
    public void EntityFrameworkCoreUsage_IsConfinedToPersistenceAndHostComposition()
    {
        string[] violations = EnumerateProductionSourceFiles()
            .Where(static path => !IsUnderData(path) && !IsRelativePath(path, "Startup.cs"))
            .Where(static path => File.ReadAllText(path).Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Select(RelativeToWebProject)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(violations, $"EF Core usage escaped the persistence/host boundary: {string.Join(", ", violations)}");
    }

    [TestMethod]
    public void ApplicationDbContextUsage_IsConfinedToPersistenceCompositionAndAuthTransactionOwnership()
    {
        string[] violations = EnumerateProductionSourceFiles()
            .Where(static path => !IsUnderData(path)
                && !IsRelativePath(path, "Startup.cs")
                && !IsRelativePath(path, Path.Combine("Services", "Auth", "AuthenticationFlowService.cs")))
            .Where(static path => File.ReadAllText(path).Contains("ApplicationDbContext", StringComparison.Ordinal))
            .Select(RelativeToWebProject)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(violations, $"ApplicationDbContext escaped the persistence/composition boundary: {string.Join(", ", violations)}");
    }

    private static IEnumerable<string> EnumerateProductionSourceFiles() => Directory.EnumerateFiles(WebProjectDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static bool IsUnderData(string path) => Path.GetRelativePath(WebProjectDirectory, path)
        .StartsWith($"Data{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool IsRelativePath(string path, string relativePath) =>
        Path.GetRelativePath(WebProjectDirectory, path).Equals(relativePath, StringComparison.Ordinal);

    private static string RelativeToWebProject(string path) => Path.GetRelativePath(WebProjectDirectory, path);

    private static string WebProjectDirectory { get; } = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourceFilePath)!, "..", "..", "Ufw.Web"));

    private static string SourceFilePath => GetSourceFilePath();

    private static string GetSourceFilePath([CallerFilePath] string path = "") => path;
}
