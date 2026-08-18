using System;
using System.IO;

namespace NexOverlay.App;

public enum BackgroundTheme
{
    Particles,
    Aurora
}

public sealed class BackgroundThemeService
{
    private readonly string _path;

    public BackgroundThemeService()
    {
        var root =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexOverlay");

        Directory.CreateDirectory(root);

        _path =
            Path.Combine(
                root,
                "background-theme.txt");
    }

    public BackgroundTheme Load()
    {
        try
        {
            if (!File.Exists(_path))
                return BackgroundTheme.Aurora;

            var value =
                File.ReadAllText(_path)
                    .Trim();

            return
                value.Equals(
                    "particles",
                    StringComparison.OrdinalIgnoreCase)
                    ? BackgroundTheme.Particles
                    : BackgroundTheme.Aurora;
        }
        catch
        {
            return BackgroundTheme.Aurora;
        }
    }

    public void Save(
        BackgroundTheme theme)
    {
        File.WriteAllText(
            _path,
            theme == BackgroundTheme.Particles
                ? "particles"
                : "aurora");
    }
}