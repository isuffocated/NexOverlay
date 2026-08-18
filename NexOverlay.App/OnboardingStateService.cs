using System;
using System.IO;

namespace NexOverlay.App;

public sealed class OnboardingStateService
{
    private readonly string _stateFile;

    public OnboardingStateService()
    {
        var root =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexOverlay");

        Directory.CreateDirectory(root);

        _stateFile =
            Path.Combine(
                root,
                "onboarding-v1.done");
    }

    public bool IsCompleted =>
        File.Exists(_stateFile);

    public void MarkCompleted()
    {
        File.WriteAllText(
            _stateFile,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    public void Reset()
    {
        if (File.Exists(_stateFile))
        {
            File.Delete(_stateFile);
        }
    }
}