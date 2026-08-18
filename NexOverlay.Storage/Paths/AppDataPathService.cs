using System;
using System.IO;

namespace NexOverlay.Storage.Paths;

public sealed class AppDataPathService
{
    private const string AppFolderName = "NexOverlay";

    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string AssetsDirectory { get; }
    public string CacheDirectory { get; }

    public AppDataPathService()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        RootDirectory =
            Path.Combine(
                localAppData,
                AppFolderName);

        DatabasePath =
            Path.Combine(
                RootDirectory,
                "nexoverlay.db");

        AssetsDirectory =
            Path.Combine(
                RootDirectory,
                "assets");

        CacheDirectory =
            Path.Combine(
                RootDirectory,
                "cache");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }
}