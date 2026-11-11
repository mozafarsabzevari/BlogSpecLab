using System.Net;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;
using static BlogSpecLab.Tests.Api.Infrastructure.ContentReviewApi;

namespace BlogSpecLab.Tests.Api;

/// <summary>User story 2 over HTTP (quickstart.md, scenarios 5 and 6).</summary>
public class ContentReviewNeedsInputTests : IAsyncLifetime
{
    private readonly BlogSpecLabApiFactory _factory = new();
    private TestUsers _users = null!;

    public async Task InitializeAsync()
    {
        _users = await TestUsers.CreateAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Scenario 5
    [Fact]
    public async Task ShortContent_ReturnsNeedsMoreInformation()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA, "سلام", "تست");
        _factory.ContentReviewModel.Returns(FakeContentReviewModel.NeedsInput("موضوع اصلی پست مشخص نیست."));

        var review = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal("NeedsMoreInformation", review.GetProperty("outcome").GetString());
        Assert.Empty(review.GetProperty("issues").EnumerateArray());
        var item = Assert.Single(review.GetProperty("missingInformation").EnumerateArray());
        Assert.Equal("موضوع اصلی پست مشخص نیست.", item.GetProperty("description").GetString());

        var stored = Assert.Single(await ListReviewsAsync(authorA, id));
        Assert.Equal("NeedsMoreInformation", stored.GetProperty("outcome").GetString());
    }

    // FR-003: after adding the missing information the author asks again
    [Fact]
    public async Task ReviewAfterEdit_UsesNewVersionAndMarksPreviousAsStale()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA, "سلام", "تست");
        _factory.ContentReviewModel.Returns(FakeContentReviewModel.NeedsInput("موضوع اصلی پست مشخص نیست."));
        var first = await RequestReviewSucceedsAsync(authorA, id);

        await EditAsync(authorA, id, "معرفی کتاب", "این پست کتابی دربارهٔ تاریخ را معرفی می‌کند.");
        var edited = await GetOwnPostAsync(authorA, id);
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        _factory.ContentReviewModel.Returns(FakeContentReviewModel.Completed());
        var second = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal(edited.GetProperty("version").GetGuid(), second.GetProperty("reviewedVersion").GetGuid());
        Assert.NotEqual(first.GetProperty("reviewedVersion").GetGuid(), second.GetProperty("reviewedVersion").GetGuid());
        Assert.Equal(new ContentReviewInput("معرفی کتاب", "این پست کتابی دربارهٔ تاریخ را معرفی می‌کند."), _factory.ContentReviewModel.Inputs[^1]);

        var listed = await ListReviewsAsync(authorA, id);
        Assert.Equal(
            [second.GetProperty("id").GetGuid(), first.GetProperty("id").GetGuid()],
            listed.Select(review => review.GetProperty("id").GetGuid()));
        Assert.True(listed[0].GetProperty("isCurrentVersion").GetBoolean());
        Assert.False(listed[1].GetProperty("isCurrentVersion").GetBoolean());
    }

    // Scenario 6
    [Fact]
    public async Task InconsistentNeedsInput_IsOutputInvalidAndNothingStored()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        _factory.ContentReviewModel.Returns(new ContentReviewModelOutcome.Candidate(
            FakeContentReviewModel.Candidate("needs_input", [FakeContentReviewModel.Issue("جملهٔ دوم")], ["موضوع مشخص نیست."])));

        await AssertProblemAsync(await RequestReviewAsync(authorA, id), HttpStatusCode.BadGateway, "ReviewOutputInvalid");

        _factory.ContentReviewModel.Returns(FakeContentReviewModel.NeedsInput());

        await AssertProblemAsync(await RequestReviewAsync(authorA, id), HttpStatusCode.BadGateway, "ReviewOutputInvalid");
        Assert.Empty(await ListReviewsAsync(authorA, id));
    }
}
