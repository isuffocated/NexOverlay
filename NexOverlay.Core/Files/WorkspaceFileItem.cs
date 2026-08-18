using System;

namespace NexOverlay.Core.Files;

public sealed record WorkspaceFileItem(
    Guid Id,
    string Name,
    string Path);