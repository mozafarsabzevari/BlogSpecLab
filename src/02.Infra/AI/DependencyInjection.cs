using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Infra.AI.ContentReviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlogSpecLab.Infra.AI;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the xAI content review model. A missing API key does not stop startup; review requests then get
    /// <see cref="ContentReviewModelOutcome.ProviderUnavailable"/> (research.md, R-13).
    /// </summary>
    public static IServiceCollection AddContentReviewModel(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ContentReviewModelOptions.SectionName).Get<ContentReviewModelOptions>()
            ?? new ContentReviewModelOptions();

        services.AddSingleton(options);
        services.AddSingleton<IContentReviewModel>(provider => new ResponsesContentReviewModel(
            options,
            provider.GetRequiredService<ILogger<ResponsesContentReviewModel>>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
