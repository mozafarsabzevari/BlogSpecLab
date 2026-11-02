using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public sealed record AuthorPostSummary(
    Guid Id,
    string Title,
    PostStatus Status,
    string? LastReturnReason,
    DateTimeOffset LastModifiedAt,
    DateTimeOffset? LastPublishedAt,
    Guid Version);

public sealed record AuthorPostDetail(
    Guid Id,
    string Title,
    string Body,
    PostStatus Status,
    string? LastReturnReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    DateTimeOffset? LastPublishedAt,
    Guid Version);
