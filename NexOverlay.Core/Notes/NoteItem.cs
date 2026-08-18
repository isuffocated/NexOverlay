using System;

namespace NexOverlay.Core.Notes;

public sealed record NoteItem(
    Guid Id,
    string Title,
    string Content);