namespace BlogSpecLab.Core.Application.Posts;

/// <summary><paramref name="PublishedAt"/> is the post's latest publication time.</summary>
public sealed record PublicPostSummary(Guid Id, string Title, string AuthorDisplayName, DateTimeOffset PublishedAt);

public sealed record PublicPostDetail(
    Guid Id,
    string Title,
    string Body,
    string AuthorDisplayName,
    DateTimeOffset PublishedAt);
