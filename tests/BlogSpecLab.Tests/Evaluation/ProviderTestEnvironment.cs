using BlogSpecLab.Infra.AI.ContentReviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogSpecLab.Tests.Evaluation;

/// <summary>
/// Settings for tests that call the real provider. They run only on explicit request and never in an ordinary
/// <c>dotnet test</c> (research.md, R-12). The key is read like the API reads it: user secrets or the
/// <c>ContentReview__ApiKey</c> environment variable.
/// </summary>
internal static class ProviderTestEnvironment
{
    public const string SwitchVariable = "BLOGSPECLAB_PROVIDER_TESTS";
    public const string ApiUserSecretsId = "blogspeclab-endpoints-api";

    private static readonly Lazy<IConfiguration> LazyConfiguration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build());

    public static IConfiguration Configuration => LazyConfiguration.Value;

    public static bool IsRequested => Environment.GetEnvironmentVariable(SwitchVariable) == "1";

    public static ContentReviewModelOptions Options
    {
        get
        {
            var options = new ContentReviewModelOptions();
            Configuration.GetSection(ContentReviewModelOptions.SectionName).Bind(options);
            return options;
        }
    }

    public static int TimeoutSeconds =>
        Configuration.GetValue("ContentReview:TimeoutSeconds", 60);

    public static string? SkipReason =>
        !IsRequested
            ? $"Real provider tests run only with {SwitchVariable}=1."
            : !Options.IsConfigured
                ? "ContentReview:ApiKey is not configured (user secrets or ContentReview__ApiKey)."
                : null;

    public static ResponsesContentReviewModel CreateModel() =>
        new(Options, NullLogger<ResponsesContentReviewModel>.Instance, TimeProvider.System);

    /// <summary>The folder that holds <c>BlogSpecLab.slnx</c>.</summary>
    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BlogSpecLab.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("BlogSpecLab.slnx was not found above the test output folder.");
        }
    }
}

/// <summary>A fact that is skipped unless real provider tests were requested and a key is configured.</summary>
public sealed class ProviderFactAttribute : FactAttribute
{
    public ProviderFactAttribute()
    {
        Skip = ProviderTestEnvironment.SkipReason;
    }
}
