using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

public class ContentReviewServiceTests
{
    private readonly ContentReviewServiceFixture _fixture = new();

    // FR-001, FR-003, FR-013
    [Fact]
    public async Task Request_ForOwnDraft_StoresValidReviewWithSnapshotVersion()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(FakeContentReviewModel.Completed(FakeContentReviewModel.Issue("متن پست")));

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.True(result.IsSuccess);
        var detail = result.Value!;
        Assert.Equal(post.Id, detail.PostId);
        Assert.Equal(ContentReviewOutcome.IssuesFound, detail.Outcome);
        Assert.Equal(post.Version, detail.ReviewedVersion);
        Assert.Equal(post.LastModifiedAt, detail.ReviewedPostModifiedAt);
        Assert.True(detail.IsCurrentVersion);
        Assert.Equal(_fixture.Time.GetUtcNow(), detail.CreatedAt);

        var stored = Assert.Single(_fixture.Store.SavedReviews);
        Assert.Equal(detail.Id, stored.Id);
        Assert.Equal(ContentReviewServiceFixture.AuthorA.UserId, stored.AuthorId);
    }

    // FR-001
    [Fact]
    public async Task Request_ForReturnedPost_IsAllowed()
    {
        var post = _fixture.AddReturned(ContentReviewServiceFixture.AuthorA);

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.Equal(ContentReviewOutcome.NoIssuesFound, result.Value!.Outcome);
    }

    // R-05
    [Fact]
    public async Task Request_SendsOnlyTitleAndBody()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA, "عنوان", "متن");

        await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.Equal(new ContentReviewInput("عنوان", "متن"), Assert.Single(_fixture.Model.Inputs));
    }

    // FR-002
    [Fact]
    public async Task Request_WithoutLogin_IsUnauthenticated()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);

        var result = await _fixture.CreateService(user: null).RequestAsync(post.Id);

        AssertFailedWithoutCall(result, ErrorCode.Unauthenticated);
    }

    // FR-002
    [Fact]
    public async Task Request_WithoutAuthorRole_IsForbidden()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);

        var result = await _fixture.CreateService(ContentReviewServiceFixture.EditorOnly).RequestAsync(post.Id);

        AssertFailedWithoutCall(result, ErrorCode.Forbidden);
    }

    // FR-002, SC-005
    [Fact]
    public async Task Request_ForOtherAuthorsOrMissingPost_IsNotFound()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        var service = _fixture.CreateService(ContentReviewServiceFixture.AuthorB);

        AssertFailedWithoutCall(await service.RequestAsync(post.Id), ErrorCode.NotFound);
        AssertFailedWithoutCall(await service.RequestAsync(Guid.NewGuid()), ErrorCode.NotFound);
    }

    // FR-002
    [Fact]
    public async Task Request_AfterSubmission_IsNotAvailableWithCurrentStatus()
    {
        var post = _fixture.AddInReview(ContentReviewServiceFixture.AuthorA);

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        AssertFailedWithoutCall(result, ErrorCode.ReviewNotAvailable);
        Assert.Equal("InReview", result.Error!.CurrentStatus);
        Assert.Null(result.Error.CurrentVersion);
    }

    // R-08
    public static TheoryData<ContentReviewModelOutcome, ErrorCode> FailedOutcomes => new()
    {
        { new ContentReviewModelOutcome.Refused(), ErrorCode.ReviewRefused },
        { new ContentReviewModelOutcome.ProviderUnavailable(), ErrorCode.ReviewServiceUnavailable },
        { new ContentReviewModelOutcome.InvalidOutput(), ErrorCode.ReviewOutputInvalid },
        { new ContentReviewModelOutcome.UnexpectedOutputForm(), ErrorCode.ReviewOutputInvalid },
        { FakeContentReviewModel.Completed(FakeContentReviewModel.Issue("نیست")), ErrorCode.ReviewOutputInvalid }
    };

    [Theory]
    [MemberData(nameof(FailedOutcomes))]
    public async Task Request_WithFailedOutcome_StoresNothing(ContentReviewModelOutcome outcome, ErrorCode expected)
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(outcome);

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.Equal(expected, result.Error?.Code);
        Assert.Empty(_fixture.Store.SavedReviews);
        Assert.Equal(0, _fixture.Store.PostSaves);
    }

    // R-11, quickstart scenario 21
    [Fact]
    public async Task Request_WhenPostDeletedDuringModelCall_IsNotFoundAndStoresNothing()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        _fixture.Model.Returns(_ =>
        {
            _fixture.Store.Delete(post.Id);
            return FakeContentReviewModel.Completed();
        });

        var result = await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        Assert.Equal(ErrorCode.NotFound, result.Error?.Code);
        Assert.Empty(_fixture.Store.SavedReviews);
    }

    // FR-013, R-11
    [Fact]
    public async Task List_ReturnsNewestFirstWithCurrentVersionFlag()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        var service = _fixture.CreateService(ContentReviewServiceFixture.AuthorA);
        var first = (await service.RequestAsync(post.Id)).Value!;

        post.Edit("عنوان تازه", "متن تازهٔ پست", _fixture.Time.GetUtcNow());
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var second = (await service.RequestAsync(post.Id)).Value!;

        var list = await service.ListAsync(post.Id);

        Assert.True(list.IsSuccess);
        Assert.Equal([second.Id, first.Id], list.Value!.Select(review => review.Id));
        Assert.True(list.Value![0].IsCurrentVersion);
        Assert.False(list.Value[1].IsCurrentVersion);
    }

    // FR-013, SC-008
    [Fact]
    public async Task List_ForOtherAuthorOrWithoutAuthorRole_IsRejected()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        await _fixture.CreateService(ContentReviewServiceFixture.AuthorA).RequestAsync(post.Id);

        var otherAuthor = await _fixture.CreateService(ContentReviewServiceFixture.AuthorB).ListAsync(post.Id);
        var editor = await _fixture.CreateService(ContentReviewServiceFixture.EditorOnly).ListAsync(post.Id);
        var anonymous = await _fixture.CreateService(user: null).ListAsync(post.Id);

        Assert.Equal(ErrorCode.NotFound, otherAuthor.Error?.Code);
        Assert.Equal(ErrorCode.Forbidden, editor.Error?.Code);
        Assert.Equal(ErrorCode.Unauthenticated, anonymous.Error?.Code);
    }

    // FR-013
    [Fact]
    public async Task List_IsAvailableAfterSubmission()
    {
        var post = _fixture.AddDraft(ContentReviewServiceFixture.AuthorA);
        var service = _fixture.CreateService(ContentReviewServiceFixture.AuthorA);
        await service.RequestAsync(post.Id);
        post.Submit(ContentReviewServiceFixture.AuthorA.UserId, "Author A", _fixture.Time.GetUtcNow());

        var list = await service.ListAsync(post.Id);

        Assert.Single(list.Value!);
    }

    private void AssertFailedWithoutCall(Result<ContentReviewDetail> result, ErrorCode expected)
    {
        Assert.Equal(expected, result.Error?.Code);
        Assert.Empty(_fixture.Model.Inputs);
        Assert.Empty(_fixture.Store.SavedReviews);
    }
}
