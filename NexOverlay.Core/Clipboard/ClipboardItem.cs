using System;

namespace NexOverlay.Core.Clipboard;

public sealed record ClipboardItem(
    Guid Id,
    string Content,
    bool IsPinned,
    DateTimeOffset UpdatedAt)
{
    public string Preview
    {
        get
        {
            var normalized =
                Content
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();

            if (normalized.Length <= 110)
                return normalized;

            return
                normalized[..107] +
                "...";
        }
    }
}