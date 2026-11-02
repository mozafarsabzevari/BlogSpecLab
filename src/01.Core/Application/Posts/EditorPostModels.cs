using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public sealed record EditorPostSummary(
    Guid Id,
    string Title,
    string AuthorDisplayName,
    PostStatus Status,
    DateTimeOffset LastModifiedAt,
    DateTimeOffset? LastPublishedAt,
    Guid Version);

public sealed record EditorPostDetail(
    Guid Id,
    string Title,
    string Body,
    Guid AuthorId,
    string AuthorDisplayName,
    PostStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    DateTimeOffset? LastPublishedAt,
    Guid Version,
    IReadOnlyList<PostHistoryEntry> History);

/// <summary>Status history only; never carries the post's title or body (FR-011).</summary>
public sealed record EditorPostHistory(Guid Id, PostStatus Status, IReadOnlyList<PostHistoryEntry> History);

public sealed record PostHistoryEntry(
    Guid ActorId,
    string ActorDisplayName,
    DateTimeOffset OccurredAt,
    PostStatus? FromStatus,
    PostStatus ToStatus,
    string? Reason);
