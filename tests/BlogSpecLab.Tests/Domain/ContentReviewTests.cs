using BlogSpecLab.Core.Domain.ContentReviews;

namespace BlogSpecLab.Tests.Domain;

public class ContentReviewTests
{
    private static readonly Guid PostId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid PostVersion = Guid.NewGuid();
    private static readonly DateTimeOffset PostModifiedAt = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = PostModifiedAt.AddMinutes(5);

    private static ContentIssue Issue(ContentIssueSeverity severity = ContentIssueSeverity.Medium, string excerpt = "متن") =>
        new(ContentIssueCategory.Clarity, severity, ContentLocation.Body, excerpt, "توضیح", suggestion: null);

    private static MissingInformationItem Missing() => new("موضوع پست مشخص نیست.");

    private static ContentReview Create(
        ContentReviewOutcome outcome,
        IEnumerable<ContentIssue>? issues = null,
        IEnumerable<MissingInformationItem>? missing = null,
        string modelName = "grok-4.7") =>
        ContentReview.Create(
            PostId,
            AuthorId,
            PostVersion,
            PostModifiedAt,
            outcome,
            issues ?? [],
            missing ?? [],
            modelName,
            "content-review-prompt-v1",
            "content_review_v1",
            Now);

    // FR-006
    [Fact]
    public void IssuesFound_WithIssues_IsCreatedWithSnapshotAndMetadata()
    {
        var review = Create(ContentReviewOutcome.IssuesFound, [Issue()]);

        Assert.NotEqual(Guid.Empty, review.Id);
        Assert.Equal(PostId, review.PostId);
        Assert.Equal(AuthorId, review.AuthorId);
        Assert.Equal(PostVersion, review.ReviewedPostVersion);
        Assert.Equal(PostModifiedAt, review.ReviewedPostModifiedAt);
        Assert.Equal(Now, review.CreatedAt);
        Assert.Equal(ContentReviewOutcome.IssuesFound, review.Outcome);
        Assert.Single(review.Issues);
        Assert.Empty(review.MissingInformation);
        Assert.Equal("grok-4.7", review.ModelName);
        Assert.Equal("content-review-prompt-v1", review.PromptVersion);
        Assert.Equal("content_review_v1", review.SchemaVersion);
    }

    [Fact]
    public void IssuesFound_WithoutIssues_IsRejected()
    {
        AssertRejected(ContentReviewErrorCode.InconsistentOutcome, () => Create(ContentReviewOutcome.IssuesFound));
    }

    [Fact]
    public void IssuesFound_WithMissingInformation_IsRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.IssuesFound, [Issue()], [Missing()]));
    }

    [Fact]
    public void NoIssuesFound_WithNothing_IsCreated()
    {
        var review = Create(ContentReviewOutcome.NoIssuesFound);

        Assert.Equal(ContentReviewOutcome.NoIssuesFound, review.Outcome);
        Assert.Empty(review.Issues);
        Assert.Empty(review.MissingInformation);
    }

    [Fact]
    public void NoIssuesFound_WithIssuesOrMissingInformation_IsRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.NoIssuesFound, [Issue()]));
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.NoIssuesFound, missing: [Missing()]));
    }

    // FR-007
    [Fact]
    public void NeedsMoreInformation_WithMissingInformationOnly_IsCreated()
    {
        var review = Create(ContentReviewOutcome.NeedsMoreInformation, missing: [Missing()]);

        Assert.Equal(ContentReviewOutcome.NeedsMoreInformation, review.Outcome);
        Assert.Empty(review.Issues);
        Assert.Single(review.MissingInformation);
    }

    [Fact]
    public void NeedsMoreInformation_WithIssuesOrWithoutMissingInformation_IsRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.NeedsMoreInformation, [Issue()], [Missing()]));
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.NeedsMoreInformation));
    }

    [Fact]
    public void Issues_AreOrderedBySeverityKeepingOriginalOrderWithinSeverity()
    {
        var review = Create(
            ContentReviewOutcome.IssuesFound,
            [
                Issue(ContentIssueSeverity.Low, "a"),
                Issue(ContentIssueSeverity.High, "b"),
                Issue(ContentIssueSeverity.Medium, "c"),
                Issue(ContentIssueSeverity.High, "d")
            ]);

        Assert.Equal(["b", "d", "c", "a"], review.Issues.Select(issue => issue.Excerpt));
    }

    [Fact]
    public void TooManyIssuesOrMissingItems_AreRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.IssuesFound, Enumerable.Range(0, 51).Select(_ => Issue())));
        AssertRejected(
            ContentReviewErrorCode.InconsistentOutcome,
            () => Create(ContentReviewOutcome.NeedsMoreInformation, missing: Enumerable.Range(0, 11).Select(_ => Missing())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyModelName_IsRejected(string modelName)
    {
        AssertRejected(
            ContentReviewErrorCode.InvalidMetadata,
            () => Create(ContentReviewOutcome.NoIssuesFound, modelName: modelName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Issue_WithEmptyExcerptOrExplanation_IsRejected(string text)
    {
        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Title, text, "توضیح", null));
        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Title, "متن", text, null));
    }

    [Fact]
    public void Issue_LengthLimits_AreEnforced()
    {
        var atLimit = new ContentIssue(
            ContentIssueCategory.Grammar,
            ContentIssueSeverity.Low,
            ContentLocation.Body,
            new string('م', ContentIssue.MaxExcerptLength),
            new string('ت', ContentIssue.MaxTextLength),
            new string('پ', ContentIssue.MaxTextLength));
        Assert.Equal(ContentIssue.MaxExcerptLength, atLimit.Excerpt.Length);

        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Body,
                new string('م', ContentIssue.MaxExcerptLength + 1), "توضیح", null));
        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Body,
                "متن", new string('ت', ContentIssue.MaxTextLength + 1), null));
        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Body,
                "متن", "توضیح", new string('پ', ContentIssue.MaxTextLength + 1)));
    }

    [Fact]
    public void Issue_WithBlankSuggestion_HasNoSuggestion()
    {
        var issue = new ContentIssue(ContentIssueCategory.Grammar, ContentIssueSeverity.Low, ContentLocation.Body, "متن", "توضیح", " ");

        Assert.Null(issue.Suggestion);
    }

    [Fact]
    public void Issue_WithUndefinedEnumValue_IsRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InvalidIssue,
            () => new ContentIssue((ContentIssueCategory)99, ContentIssueSeverity.Low, ContentLocation.Body, "متن", "توضیح", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingInformation_WithEmptyDescription_IsRejected(string description)
    {
        AssertRejected(ContentReviewErrorCode.InvalidMissingInformation, () => new MissingInformationItem(description));
    }

    [Fact]
    public void MissingInformation_TooLong_IsRejected()
    {
        AssertRejected(
            ContentReviewErrorCode.InvalidMissingInformation,
            () => new MissingInformationItem(new string('ت', MissingInformationItem.MaxDescriptionLength + 1)));
    }

    private static void AssertRejected(ContentReviewErrorCode expected, Func<object> create)
    {
        var exception = Assert.Throws<ContentReviewDomainException>(create);
        Assert.Equal(expected, exception.Code);
    }
}
