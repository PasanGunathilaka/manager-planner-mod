namespace Baseline.Harness;

/// <summary>
/// Records whether a legacy call threw, and what it threw, so scenario Facts don't each
/// hand-roll their own try/catch shape. Several scenarios (GM-001 through GM-007's validators,
/// GM-030 through GM-032's Restrict-rule deletes) exist specifically to pin exception behavior
/// as the captured output, not to assert it against a pre-decided expected message.
/// </summary>
public sealed record CaptureResult(
    bool Threw,
    string? ExceptionType,
    string? Message,
    string? InnerExceptionType,
    string? InnerExceptionMessage);

public static class Capture
{
    public static CaptureResult Run(Action action)
    {
        try
        {
            action();
            return new CaptureResult(false, null, null, null, null);
        }
        catch (Exception ex)
        {
            return new CaptureResult(
                true, ex.GetType().FullName, ex.Message,
                ex.InnerException?.GetType().FullName, ex.InnerException?.Message);
        }
    }

    public static async Task<CaptureResult> RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            return new CaptureResult(false, null, null, null, null);
        }
        catch (Exception ex)
        {
            return new CaptureResult(
                true, ex.GetType().FullName, ex.Message,
                ex.InnerException?.GetType().FullName, ex.InnerException?.Message);
        }
    }
}
