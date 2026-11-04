namespace Voltura.HardTop.Platform;

internal sealed record AppPaths(string Data, bool Portable, bool Isolated);
internal sealed class ApplicationLog
{
    // No window titles, process paths or personal diagnostics are logged.
    public void Record(string operation, Exception error) => System.Diagnostics.Debug.WriteLine($"{operation}: {error.GetType().Name}");
}
