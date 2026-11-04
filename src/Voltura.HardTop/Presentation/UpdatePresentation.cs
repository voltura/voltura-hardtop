using Voltura.HardTop.Updates;

namespace Voltura.HardTop.Presentation;

internal static class UpdatePresentation
{
    internal static string StatusKey(UpdateStatus status) => status switch
    {
        UpdateStatus.ManualUpdates => "DownloadUpdatesManuallyForThisCopy",
        UpdateStatus.Checking => "CheckingForUpdatesMore",
        UpdateStatus.Available => "UpdateAvailable",
        UpdateStatus.Downloading => "DownloadingUpdateMore",
        UpdateStatus.Ready => "UpdateReady",
        UpdateStatus.Current => "UpToDate",
        UpdateStatus.Installing => "Installing",
        UpdateStatus.UpdateCheckFailed => "UpdateCheckFailed",
        UpdateStatus.UpdateDownloadFailed => "DownloadFailedTryAgain",
        UpdateStatus.UpdateVerificationFailed => "VerificationFailed",
        UpdateStatus.UpdateInstallFailed => "InstallationFailedTryAgain",
        _ => "ReadyToCheck",
    };
}
