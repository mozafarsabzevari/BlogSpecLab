using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

/// <summary>User story 2: content that is too short or unclear (FR-006, R-07 rules 2 and 3).</summary>
public class ContentReviewNeedsInputTests
{
    private readonly ContentReviewServiceFixture _fixture = new();

    [Fact]
    public async Task NeedsInput_IsStoredAsNeedsMoreInformation()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA, "سلام", "تست");
        _fixture.Model.Returns(FakeContentReviewModel.NeedsInput("موضوع اصلی پست مشخص نیست.", "متن بسیار کوتاه است."));

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.True(result.IsSuccess);
        var detail = result.Value!;
        Assert.Equal(ContentReviewOutcome.NeedsMoreInformation, detail.Outcome);
        Assert.Empty(detail.Issues);
        Assert.Equal(
            ["موضوع اصلی پست مشخص نیست.", "متن بسیار کوتاه است."],
            detail.MissingInformation.Select(item => item.Description));
        Assert.Single(_fixture.Store.SavedReviews);
    }

    // R-07 rule 2
    [Fact]
    public async Task NeedsInput_WithIssues_IsOutputInvalid()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(new ContentReviewModelOutcome.Candidate(
            FakeContentReviewModel.Candidate(
                "needs_input",
                [FakeContentReviewModel.Issue("متن پست")],
                ["موضوع مشخص نیست."])));

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        AssertOutputInvalid(result);
    }

    // R-07 rule 2
    [Fact]
    public async Task NeedsInput_WithoutMissingInformation_IsOutputInvalid()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(FakeContentReviewModel.NeedsInput());

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        AssertOutputInvalid(result);
    }

    // R-07 rule 3
    [Fact]
    public async Task Completed_WithMissingInformation_IsOutputInvalid()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(new ContentReviewModelOutcome.Candidate(
            FakeContentReviewModel.Candidate("completed", [], ["موضوع مشخص نیست."])));

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        AssertOutputInvalid(result);
    }

    // R-07 rule 4
    [Fact]
    public async Task NeedsInput_WithTooManyItems_IsOutputInvalid()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        var items = Enumerable.Range(0, ContentReview.MaxMissingInformation + 1).Select(i => $"مورد {i}").ToArray();
        _fixture.Model.Returns(FakeContentReviewModel.NeedsInput(items));

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        AssertOutputInvalid(result);
    }

    private void AssertOutputInvalid<T>(Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ReviewOutputInvalid, result.Error!.Code);
        Assert.Empty(_fixture.Store.SavedReviews);
    }
}
