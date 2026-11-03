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

    public void Submit(Guid actorId, string actorDisplayName, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.Draft, PostStatus.Returned);
        ValidateContent(Title, Body);

        ChangeStatus(PostStatus.InReview, actorId, actorDisplayName, reason: null, now);
    }

    public void Withdraw(Guid actorId, string actorDisplayName, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.InReview);

        ChangeStatus(PostStatus.Draft, actorId, actorDisplayName, reason: null, now);
    }

    public void Approve(Guid editorId, string editorDisplayName, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.InReview);

        if (editorId == AuthorId)
        {
            throw new PostDomainException(
                PostErrorCode.SelfApprovalNotAllowed,
                "نویسندهٔ پست نمی‌تواند پست خودش را تأیید کند.");
        }

        ChangeStatus(PostStatus.Approved, editorId, editorDisplayName, reason: null, now);
    }

    public void Return(Guid editorId, string editorDisplayName, string? reason, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.InReview);

        var trimmedReason = reason?.Trim();

        if (string.IsNullOrEmpty(trimmedReason))
        {
            throw new PostDomainException(
                PostErrorCode.ReturnReasonRequired,
                "دلیل بازگرداندن الزامی است.");
        }

        ChangeStatus(PostStatus.Returned, editorId, editorDisplayName, trimmedReason, now);
        LastReturnReason = trimmedReason;
    }

    public void Publish(Guid editorId, string editorDisplayName, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.Approved);

        ChangeStatus(PostStatus.Published, editorId, editorDisplayName, reason: null, now);
        LastPublishedAt = now;
    }

    public void Unpublish(Guid editorId, string editorDisplayName, DateTimeOffset now)
    {
        EnsureStatus(PostStatus.Published);

        ChangeStatus(PostStatus.Approved, editorId, editorDisplayName, reason: null, now);
    }

    /// <summary>
    /// Edits an Approved or Published post. Calling this is the author's confirmation that the post goes back
    /// to Draft (FR-012).
    /// </summary>
    public void EditAndRevertToDraft(
        Guid authorId,
        string authorDisplayName,
        string? title,
        string? body,
        DateTimeOffset now)
    {
        EnsureStatus(PostStatus.Approved, PostStatus.Published);
        var (validTitle, validBody) = ValidateContent(title, body);

        Title = validTitle;
        Body = validBody;
        ChangeStatus(PostStatus.Draft, authorId, authorDisplayName, reason: null, now);
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

    private void EnsureStatus(params PostStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new PostDomainException(
                PostErrorCode.InvalidStatusTransition,
                "این عملیات در وضعیت فعلی پست مجاز نیست.");
        }
    }

    private void ChangeStatus(
        PostStatus toStatus,
        Guid actorId,
        string actorDisplayName,
        string? reason,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);

        _statusChanges.Add(new StatusChange(Id, actorId, actorDisplayName, now, Status, toStatus, reason));
        Status = toStatus;
        Touch(now);
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
