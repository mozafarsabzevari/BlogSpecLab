using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;

namespace BlogSpecLab.Tests.Application;

/// <summary>The application deadline and client cancellation (FR-011, research.md R-09). No test sleeps.</summary>
public class ContentReviewDeadlineTests
{
    private readonly ContentReviewServiceFixture _fixture = new();

    [Fact]
    public async Task ReachingDeadline_IsTimedOutAndNothingStored()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        var versionBefore = post.Version;
        _fixture.Model.Hold();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        _fixture.Time.Advance(_fixture.Deadline.Timeout);
        var result = await pending;

        Assert.Equal(ErrorCode.ReviewTimedOut, result.Error?.Code);
        Assert.Empty(_fixture.Store.SavedReviews);
        Assert.Equal(versionBefore, post.Version);
        Assert.Equal(0, _fixture.Store.PostSaves);
    }

    [Fact]
    public async Task AnswerBeforeDeadline_IsStored()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Hold();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        _fixture.Time.Advance(_fixture.Deadline.Timeout - TimeSpan.FromSeconds(1));
        _fixture.Model.Release();
        var result = await pending;

        Assert.True(result.IsSuccess);
        Assert.Single(_fixture.Store.SavedReviews);
    }

    [Fact]
    public async Task ConfiguredDeadline_IsUsed()
    {
        _fixture.Deadline = new ContentReviewDeadlineOptions { TimeoutSeconds = 5 };
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Hold();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);
        await _fixture.Model.WhenCalled;
        _fixture.Time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(ErrorCode.ReviewTimedOut, (await pending).Error?.Code);
    }

    [Fact]
    public async Task ClientCancellation_PropagatesAndNothingStored()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Hold();
        using var client = new CancellationTokenSource();

        var pending = _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id, client.Token);
        await _fixture.Model.WhenCalled;
        await client.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(_fixture.Store.SavedReviews);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void TimeoutOutsideRange_IsRejected(int seconds)
    {
        var options = new ContentReviewDeadlineOptions { TimeoutSeconds = seconds };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void TimeoutInsideRange_IsAccepted(int seconds)
    {
        new ContentReviewDeadlineOptions { TimeoutSeconds = seconds }.Validate();
    }
}
