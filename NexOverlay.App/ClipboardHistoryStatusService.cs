using Microsoft.Win32;

namespace NexOverlay.App;

public enum ClipboardHistoryState
{
    Enabled,
    Disabled,
    BlockedByPolicy
}

public static class ClipboardHistoryStatusService
{
    public static ClipboardHistoryState GetState()
    {
        try
        {
            using var machinePolicy =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\System");

            var policyValue =
                machinePolicy?.GetValue(
                    "AllowClipboardHistory");

            if (policyValue is int policy &&
                policy == 0)
            {
                return
                    ClipboardHistoryState.BlockedByPolicy;
            }
        }
        catch
        {
            // Fall through to current-user state.
        }

        try
        {
            using var userClipboard =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Clipboard");

            var enabledValue =
                userClipboard?.GetValue(
                    "EnableClipboardHistory");

            if (enabledValue is int enabled &&
                enabled != 0)
            {
                return
                    ClipboardHistoryState.Enabled;
            }
        }
        catch
        {
            // Treat unknown as disabled and offer Settings.
        }

        return
            ClipboardHistoryState.Disabled;
    }
}