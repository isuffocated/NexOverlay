namespace NexOverlay.Core.Snippets;

public sealed record SnippetItem(
    Guid Id,
    string Title,
    string Content,
    string Category);
