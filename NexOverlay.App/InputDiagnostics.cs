using System;
using System.IO;
using System.Text;

namespace NexOverlay.App;

public static class InputDiagnostics
{
    private static readonly object Sync = new();

    private static readonly string DirectoryPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexOverlay",
            "diagnostics");

    public static string LogPath { get; } =
        Path.Combine(
            DirectoryPath,
            "input.log");

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(
                    DirectoryPath);

                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never take the app down.
        }
    }
}