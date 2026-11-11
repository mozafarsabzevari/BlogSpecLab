using System.Net;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using static BlogSpecLab.Tests.Api.Infrastructure.ContentReviewApi;

namespace BlogSpecLab.Tests.Api;

/// <summary>Quickstart.md scenario 15 and startup validation of the deadline (FR-011, R-09).</summary>
public class ContentReviewTimeoutTests : IAsyncLifetime
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

    // Scenario 15
    [Fact]
    public async Task ModelPastDeadline_Is504AndNothingStored()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        var before = (await GetOwnPostAsync(authorA, id)).GetRawText();
        _factory.ContentReviewModel.Hold();

        var pending = RequestReviewAsync(authorA, id);
        await _factory.ContentReviewModel.WhenCalled;
        _factory.Time.Advance(TimeSpan.FromSeconds(ContentReviewDeadlineOptions.DefaultTimeoutSeconds));

        await AssertProblemAsync(await pending, HttpStatusCode.GatewayTimeout, "ReviewTimedOut");
        Assert.Empty(await ListReviewsAsync(authorA, id));
        Assert.Equal(before, (await GetOwnPostAsync(authorA, id)).GetRawText());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    public void TimeoutOutsideRange_StopsStartup(string seconds)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ContentReview:TimeoutSeconds", seconds));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ContentReview:TimeoutSeconds", exception.ToString());
    }
}
