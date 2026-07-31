using System.Runtime.CompilerServices;

namespace Baseline.Harness;

/// <summary>
/// Resolves this harness project's own on-disk directory (and, from it, the sibling fixtures
/// directory) using [CallerFilePath] captured at compile time, so path resolution is stable
/// regardless of the working directory `dotnet test` actually runs from (typically the build
/// output folder, e.g. bin/Debug/net8.0, not this source directory).
/// </summary>
public static class Paths
{
    private static string ThisFileDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    /// <summary>This harness project's own directory: .specclaw/baseline/harness.</summary>
    public static readonly string HarnessDir = ThisFileDirectory();

    /// <summary>
    /// The sibling fixtures directory (.specclaw/baseline/fixtures) that
    /// `specclaw-baseline harness-collect` already created empty, per the collected JSON's
    /// fixtures_dir field.
    /// </summary>
    public static readonly string FixturesDir = Path.GetFullPath(Path.Combine(HarnessDir, "..", "fixtures"));
}
