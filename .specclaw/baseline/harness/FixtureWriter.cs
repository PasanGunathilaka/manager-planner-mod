using System.Text.Json;

namespace Baseline.Harness;

/// <summary>
/// Writes one golden-master fixture JSON file per scenario, in the exact shape
/// `specclaw-baseline record` expects: flat top-level scalar metadata fields (scenario_id,
/// captured_at, anchor_date, legacy_commit_sha, runtime_version) plus a flat `normalized_fields`
/// string array, so they can be extracted with a simple grep/sed pass on the bash side — no JSON
/// parser needed there. Do not rename these fields or nest them differently; doing so breaks
/// `specclaw-baseline record`'s field extraction and the resulting manifest.json will silently
/// show them as empty/unknown, not error.
/// </summary>
public static class FixtureWriter
{
    /// <summary>
    /// The anchor date every scenario's relative dates (e.g. "promised date = anchor - 1 day")
    /// should be computed from, so a replay on a different calendar day reproduces the same
    /// boundary being exercised. Read once per capture run (static readonly, initialized on
    /// first touch) — individual scenario Facts must use this instead of calling
    /// DateTime.UtcNow themselves; see seams.md's Capture Blockers CB-1/CB-2 for why the legacy
    /// PlanningService/PlanningRules code itself has no injectable clock to hang a better
    /// mitigation on.
    /// </summary>
    public static readonly DateTime AnchorDate = DateTime.UtcNow.Date;

    public static void Write(
        string scenarioId,
        string fixturesDir,
        object input,
        object output,
        IEnumerable<string>? normalizedFields = null)
    {
        Directory.CreateDirectory(fixturesDir);

        var fixture = new
        {
            scenario_id = scenarioId,
            captured_at = DateTime.UtcNow.ToString("o"),
            anchor_date = AnchorDate.ToString("yyyy-MM-dd"),
            legacy_commit_sha = GetLegacyCommitSha(),
            runtime_version = Environment.Version.ToString(),
            normalized_fields = (normalizedFields ?? Array.Empty<string>()).ToArray(),
            input,
            output
        };

        var path = Path.Combine(fixturesDir, $"{scenarioId}.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(fixture, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GetLegacyCommitSha()
    {
        try
        {
            // The legacy repo (manager-planner) is the sibling checkout the ProjectReference
            // points into, not this harness's own repo (manager-planner-mod) — run `git` with
            // that directory explicitly so the recorded SHA is the legacy app's own commit, not
            // the rebuild repo's.
            var legacyRepoDir = Path.GetFullPath(Path.Combine(Paths.HarnessDir, "..", "..", "..", "..", "manager-planner"));
            var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = legacyRepoDir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return "unknown";
            var sha = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return string.IsNullOrEmpty(sha) ? "unknown" : sha;
        }
        catch
        {
            return "unknown";
        }
    }
}
