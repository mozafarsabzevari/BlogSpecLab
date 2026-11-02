namespace BlogSpecLab.Core.Domain.Posts;

/// <summary>
/// The only place where a post's content and status change. Every change gets a new <see cref="Version"/>.
/// </summary>
public sealed class Post
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 50_000;

    private readonly List<StatusChange> _statusChanges = [];

    private Post()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public Guid AuthorId { get; private set; }

    public string AuthorDisplayName { get; private set; } = string.Empty;

    public PostStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? LastPublishedAt { get; private set; }

    public string? LastReturnReason { get; private set; }

    public Guid Version { get; private set; }

    public IReadOnlyList<StatusChange> StatusChanges => _statusChanges;

    public bool HasEverBeenPublished => LastPublishedAt is not null;

    public static Post Create(
        Guid authorId,
        string authorDisplayName,
        string? title,
        string? body,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorDisplayName);
        var (validTitle, validBody) = ValidateContent(title, body);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = validTitle,
            Body = validBody,
            AuthorId = authorId,
            AuthorDisplayName = authorDisplayName,
            Status = PostStatus.Draft,
            CreatedAt = now,
            LastModifiedAt = now,
            Version = Guid.NewGuid()
        };

        post._statusChanges.Add(new StatusChange(
            post.Id, authorId, authorDisplayName, now, fromStatus: null, PostStatus.Draft, reason: null));

        return post;
    }

    public void Edit(string? title, string? body, DateTimeOffset now)
    {
        if (Status is not (PostStatus.Draft or PostStatus.Returned))
        {
            throw new PostDomainException(
                PostErrorCode.InvalidStatusTransition,
                "پست در این وضعیت قابل ویرایش نیست.");
        }

        var (validTitle, validBody) = ValidateContent(title, body);

        Title = validTitle;
        Body = validBody;
        Touch(now);
    }

    public void EnsureCanDelete()
    {
        if (Status is not (PostStatus.Draft or PostStatus.Returned))
        {
            throw new PostDomainException(
                PostErrorCode.InvalidStatusTransition,
                "فقط پیش‌نویس‌ها و پست‌های بازگشت‌خورده قابل حذف‌اند.");
        }

        if (HasEverBeenPublished)
        {
            throw new PostDomainException(
                PostErrorCode.DeleteNotAllowed,
                "پستی که حداقل یک بار منتشر شده است قابل حذف نیست.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        LastModifiedAt = now;
        Version = Guid.NewGuid();
    }

    private static (string Title, string Body) ValidateContent(string? title, string? body)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;

        if (trimmedTitle.Length is 0 or > MaxTitleLength)
        {
            throw new PostDomainException(
                PostErrorCode.InvalidTitle,
                $"عنوان الزامی است و حداکثر {MaxTitleLength} نویسه دارد.");
        }

        var trimmedBody = body?.Trim() ?? string.Empty;

        if (trimmedBody.Length is 0 or > MaxBodyLength)
        {
            throw new PostDomainException(
                PostErrorCode.InvalidBody,
                $"متن الزامی است و حداکثر {MaxBodyLength:N0} نویسه دارد.");
        }

        return (trimmedTitle, trimmedBody);
    }
}
