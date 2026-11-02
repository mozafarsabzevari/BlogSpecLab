namespace BlogSpecLab.Core.Domain.Posts;

/// <summary>
/// One entry of a post's status history. Entries are only ever appended.
/// </summary>
public sealed class StatusChange
{
    private StatusChange()
    {
    }

    internal StatusChange(
        Guid postId,
        Guid actorId,
        string actorDisplayName,
        DateTimeOffset occurredAt,
        PostStatus? fromStatus,
        PostStatus toStatus,
        string? reason)
    {
        Id = Guid.NewGuid();
        PostId = postId;
        ActorId = actorId;
        ActorDisplayName = actorDisplayName;
        OccurredAt = occurredAt;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
    }

    public Guid Id { get; private set; }

    public Guid PostId { get; private set; }

    public Guid ActorId { get; private set; }

    public string ActorDisplayName { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public PostStatus? FromStatus { get; private set; }

    public PostStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }
}
