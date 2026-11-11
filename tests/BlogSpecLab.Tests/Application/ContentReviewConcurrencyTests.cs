using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

/// <summary>
/// Changes that happen while the model is working (research.md R-11 and R-15). The fake model holds every call
/// until the test releases it, so the order is fixed without sleeping.
/// </summary>
public class ContentReviewConcurrencyTests
{
    private readonly ContentReviewServiceFixture _fixture = new();

    // R-15
    [Fact]
    public async Task TwoConcurrentRequests_CreateTwoSeparateReviews()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Hold();

        var first = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        var second = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        Assert.Equal(2, _fixture.Model.Inputs.Count);
        _fixture.Model.Release();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.NotEqual(results[0].Value!.Id, results[1].Value!.Id);
        Assert.Equal(2, _fixture.Store.SavedReviews.Count);
    }

    // Scenario 21, R-11
    [Fact]
    public async Task PostDeletedDuringModelCall_IsNotFoundAndNothingStored()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Hold();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        _fixture.Store.Delete(post.Id);
        _fixture.Model.Release();
        var result = await pending;

        Assert.Equal(ErrorCode.NotFound, result.Error?.Code);
        Assert.Empty(_fixture.Store.SavedReviews);
    }

    // R-11: the review belongs to the version it was made for
    [Fact]
    public async Task PostEditedDuringModelCall_StoresReviewForPreviousVersion()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA, "عنوان قبلی", "متن قبلی که بررسی می‌شود.");
        var reviewedVersion = post.Version;
        _fixture.Model.Returns(FakeContentReviewModel.Completed(FakeContentReviewModel.Issue("متن قبلی")));
        _fixture.Model.Hold();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        post.Edit("عنوان تازه", "متن تازه", _fixture.Time.GetUtcNow());
        _fixture.Model.Release();
        var result = await pending;

        Assert.True(result.IsSuccess);
        Assert.Equal(reviewedVersion, result.Value!.ReviewedVersion);
        Assert.NotEqual(post.Version, result.Value.ReviewedVersion);
        Assert.False(result.Value.IsCurrentVersion);
        Assert.Equal(new ContentReviewInput("عنوان قبلی", "متن قبلی که بررسی می‌شود."), Assert.Single(_fixture.Model.Inputs));
        Assert.Equal("متن قبلی", Assert.Single(result.Value.Issues).Excerpt);
    }

    // R-15: a review request does not lock the post; editing and submitting go on as usual
    [Fact]
    public async Task ReviewDoesNotSavePostChanges()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);

        await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.Equal(0, _fixture.Store.PostSaves);
    }
}
