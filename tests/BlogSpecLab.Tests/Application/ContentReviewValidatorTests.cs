using System.Text;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

/// <summary>Rules 1, 4, 5, 6 and 7 of research.md R-07.</summary>
public class ContentReviewValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ContentReviewSnapshot Snapshot = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "عنوان آزمایشی",
        "ما میخواهیم این متن را بررسی کنیم. جملهٔ دوم هم اینجاست.",
        Guid.NewGuid(),
        Now.AddHours(-1));

    private readonly ContentReviewValidator _validator = new();

    private Result<ContentReview> Validate(params CandidateIssue[] issues) =>
        _validator.Validate(FakeContentReviewModel.Candidate("completed", issues, []), Snapshot, Now);

    [Fact]
    public void Completed_WithMatchingIssue_IsIssuesFoundWithSnapshot()
    {
        var result = Validate(FakeContentReviewModel.Issue(
            "میخواهیم", category: "spelling_and_writing", suggestion: "می‌خواهیم"));

        Assert.True(result.IsSuccess);
        var review = result.Value!;
        Assert.Equal(ContentReviewOutcome.IssuesFound, review.Outcome);
        Assert.Equal(Snapshot.PostId, review.PostId);
        Assert.Equal(Snapshot.AuthorId, review.AuthorId);
        Assert.Equal(Snapshot.Version, review.ReviewedPostVersion);
        Assert.Equal(Snapshot.LastModifiedAt, review.ReviewedPostModifiedAt);
        Assert.Equal(Now, review.CreatedAt);
        Assert.Equal(FakeContentReviewModel.ModelName, review.ModelName);
        var issue = Assert.Single(review.Issues);
        Assert.Equal(ContentIssueCategory.SpellingAndWriting, issue.Category);
        Assert.Equal(ContentLocation.Body, issue.Location);
        Assert.Equal("می‌خواهیم", issue.Suggestion);
    }

    [Fact]
    public void Completed_WithoutIssues_IsNoIssuesFound()
    {
        var result = Validate();

        Assert.Equal(ContentReviewOutcome.NoIssuesFound, result.Value!.Outcome);
    }

    // Rule 1
    [Theory]
    [InlineData("done", "clarity", "medium", "body")]
    [InlineData("completed", "style", "medium", "body")]
    [InlineData("completed", "clarity", "critical", "body")]
    [InlineData("completed", "clarity", "medium", "summary")]
    [InlineData("Completed", "clarity", "medium", "body")]
    [InlineData("completed", "Clarity", "medium", "body")]
    public void UnknownValues_MakeTheWholeOutputInvalid(string outcome, string category, string severity, string location)
    {
        var candidate = FakeContentReviewModel.Candidate(
            outcome,
            [FakeContentReviewModel.Issue("میخواهیم", location, severity, category)],
            []);

        AssertInvalid(_validator.Validate(candidate, Snapshot, Now));
    }

    // Rule 4
    [Fact]
    public void TextLimits_AreEnforced()
    {
        AssertInvalid(Validate(FakeContentReviewModel.Issue("میخواهیم", explanation: "")));
        AssertInvalid(Validate(FakeContentReviewModel.Issue("میخواهیم", explanation: new string('ت', 1001))));
        AssertInvalid(Validate(FakeContentReviewModel.Issue("میخواهیم", suggestion: new string('پ', 1001))));
        AssertInvalid(Validate(FakeContentReviewModel.Issue(" ")));
        AssertInvalid(Validate(Enumerable.Range(0, 51).Select(_ => FakeContentReviewModel.Issue("میخواهیم")).ToArray()));
    }

    // Rule 5
    [Fact]
    public void Excerpt_IsSearchedOnlyInItsOwnLocation()
    {
        var inTitle = Validate(FakeContentReviewModel.Issue("عنوان", location: "title"));
        Assert.Equal(ContentLocation.Title, Assert.Single(inTitle.Value!.Issues).Location);

        // "عنوان" is in the title, not in the body.
        AssertInvalid(Validate(FakeContentReviewModel.Issue("عنوان", location: "body")));
    }

    // Rule 5, FR-008, SC-004
    [Fact]
    public void IssueWithoutRealReference_IsDroppedAndTheRestKept()
    {
        var result = Validate(
            FakeContentReviewModel.Issue("جملهٔ دوم"),
            FakeContentReviewModel.Issue("این عبارت در پست نیست"));

        Assert.True(result.IsSuccess);
        Assert.Equal("جملهٔ دوم", Assert.Single(result.Value!.Issues).Excerpt);
    }

    // Rule 5
    [Fact]
    public void Excerpt_IsComparedOrdinallyAfterNormalizationFormC()
    {
        var decomposedTitle = "Cafe\u0301 خوب";
        var snapshot = Snapshot with { Title = decomposedTitle };

        var composed = _validator.Validate(
            FakeContentReviewModel.Candidate("completed", [FakeContentReviewModel.Issue("Café", location: "title")], []),
            snapshot,
            Now);
        Assert.True(composed.IsSuccess);

        var differentCase = _validator.Validate(
            FakeContentReviewModel.Candidate("completed", [FakeContentReviewModel.Issue("CAFÉ", location: "title")], []),
            snapshot,
            Now);
        AssertInvalid(differentCase);
        Assert.NotEqual(decomposedTitle, decomposedTitle.Normalize(NormalizationForm.FormC));
    }

    // Rule 6, quickstart scenario 8
    [Fact]
    public void AllIssuesDropped_IsInvalidNotNoIssuesFound()
    {
        AssertInvalid(Validate(
            FakeContentReviewModel.Issue("ساختگی اول"),
            FakeContentReviewModel.Issue("ساختگی دوم")));
    }

    // Rule 7
    [Fact]
    public void Issues_AreOrderedBySeverity()
    {
        var result = Validate(
            FakeContentReviewModel.Issue("ما", severity: "low"),
            FakeContentReviewModel.Issue("این متن", severity: "high"),
            FakeContentReviewModel.Issue("بررسی", severity: "medium"),
            FakeContentReviewModel.Issue("جملهٔ دوم", severity: "high"));

        Assert.Equal(
            ["این متن", "جملهٔ دوم", "بررسی", "ما"],
            result.Value!.Issues.Select(issue => issue.Excerpt));
    }

    private static void AssertInvalid(Result<ContentReview> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ReviewOutputInvalid, result.Error.Code);
    }
}
