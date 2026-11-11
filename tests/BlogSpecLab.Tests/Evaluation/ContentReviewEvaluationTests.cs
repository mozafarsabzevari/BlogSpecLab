using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Infra.AI.ContentReviews;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace BlogSpecLab.Tests.Evaluation;

/// <summary>
/// Runs the labelled corpus against the real provider and writes the evidence to
/// <c>specs/002-ai-content-review/evaluation/results/</c> (evaluation/protocol.md). Run with
/// <c>--filter "Category=Evaluation"</c>. Thresholds come from configuration; the defaults are the proposed
/// values in research.md R-12 and are recorded as unapproved until a human approves them.
/// </summary>
[Trait("Category", "Evaluation")]
public class ContentReviewEvaluationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions CorpusOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions EvidenceOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    [ProviderFact]
    public async Task Corpus_MeetsConfiguredThresholds()
    {
        var corpus = LoadCorpus();
        var thresholds = EvaluationThresholds.FromConfiguration();
        var options = ProviderTestEnvironment.Options;
        var model = ProviderTestEnvironment.CreateModel();
        var validator = new ContentReviewValidator();
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<SampleResult>();

        foreach (var sample in corpus.Samples)
        {
            results.Add(await RunSampleAsync(sample, model, validator));
        }

        var metrics = EvaluationMetrics.From(corpus.Samples, results);
        var evidence = new Evidence(
            startedAt,
            DateTimeOffset.UtcNow,
            options.Model,
            results.Select(result => result.ReturnedModel).Where(name => name is not null).Distinct().ToList(),
            ContentReviewPrompt.Version,
            ContentReviewSchema.Name,
            corpus.CorpusVersion,
            corpus.LabelsStatus,
            new EvidenceSettings(options.Endpoint, options.Model, options.MaxOutputTokenCount, ProviderTestEnvironment.TimeoutSeconds),
            thresholds,
            metrics,
            metrics.Check(thresholds),
            results);

        var path = WriteEvidence(evidence, startedAt);
        output.WriteLine($"Evidence written to {Path.GetRelativePath(ProviderTestEnvironment.RepositoryRoot, path)}");

        foreach (var failure in evidence.ThresholdFailures)
        {
            output.WriteLine($"Threshold not met: {failure}");
        }

        Assert.Empty(evidence.ThresholdFailures);
    }

    private static async Task<SampleResult> RunSampleAsync(
        CorpusSample sample,
        IContentReviewModel model,
        ContentReviewValidator validator)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ProviderTestEnvironment.TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        ContentReviewModelOutcome outcome;

        try
        {
            outcome = await model.ReviewAsync(new ContentReviewInput(sample.Title, sample.Body), deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return SampleResult.Failed(sample, "TimedOut", stopwatch.ElapsedMilliseconds);
        }

        var elapsed = stopwatch.ElapsedMilliseconds;

        if (outcome is not ContentReviewModelOutcome.Candidate candidate)
        {
            return SampleResult.Failed(sample, outcome.GetType().Name, elapsed);
        }

        var now = DateTimeOffset.UtcNow;
        var validated = validator.Validate(
            candidate.Review,
            new ContentReviewSnapshot(Guid.NewGuid(), Guid.NewGuid(), sample.Title, sample.Body, Guid.NewGuid(), now),
            now);

        if (!validated.IsSuccess)
        {
            return SampleResult.Failed(sample, validated.Error.Code.ToString(), elapsed, candidate.Review.ModelName);
        }

        var review = validated.Value!;
        var issues = review.Issues
            .Select(issue => new IssueResult(issue.Category, issue.Severity, issue.Location, issue.Excerpt))
            .ToList();

        return new SampleResult(
            sample.Id,
            sample.Kind,
            review.Outcome.ToString(),
            Failure: null,
            elapsed,
            candidate.Review.ModelName,
            issues,
            review.MissingInformation.Count,
            sample.PlantedDefects.Count(defect => issues.Any(defect.IsFoundBy)),
            candidate.Review.Issues.Count - issues.Count);
    }

    private static Corpus LoadCorpus()
    {
        var path = Path.Combine(
            ProviderTestEnvironment.RepositoryRoot,
            "tests", "BlogSpecLab.Tests", "Evaluation", "Corpus", "content_review_corpus_v1.json");

        return JsonSerializer.Deserialize<Corpus>(File.ReadAllText(path), CorpusOptions)
            ?? throw new InvalidDataException("The evaluation corpus is empty.");
    }

    private static string WriteEvidence(Evidence evidence, DateTimeOffset startedAt)
    {
        var folder = Path.Combine(ProviderTestEnvironment.RepositoryRoot, "specs", "002-ai-content-review", "evaluation", "results");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(
            folder,
            $"content-review-eval-{startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}Z.json");
        File.WriteAllText(path, JsonSerializer.Serialize(evidence, EvidenceOptions));
        return path;
    }

    private sealed record Corpus(string CorpusVersion, string LabelsStatus, IReadOnlyList<CorpusSample> Samples);

    private sealed record CorpusSample(
        string Id,
        string Kind,
        string Title,
        string Body,
        string ExpectedOutcome,
        IReadOnlyList<PlantedDefect> PlantedDefects);

    private sealed record PlantedDefect(string Category, string Location, string Excerpt)
    {
        private static readonly Dictionary<string, ContentIssueCategory> Categories = new(StringComparer.Ordinal)
        {
            ["spelling_and_writing"] = ContentIssueCategory.SpellingAndWriting,
            ["grammar"] = ContentIssueCategory.Grammar,
            ["clarity"] = ContentIssueCategory.Clarity,
            ["structure_and_coherence"] = ContentIssueCategory.StructureAndCoherence,
            ["title_body_mismatch"] = ContentIssueCategory.TitleBodyMismatch,
            ["claim_needs_source"] = ContentIssueCategory.ClaimNeedsSource
        };

        /// <summary>Same category and location, and the two excerpts overlap.</summary>
        public bool IsFoundBy(IssueResult issue) =>
            issue.Category == Categories[Category]
            && string.Equals(issue.Location.ToString(), Location, StringComparison.OrdinalIgnoreCase)
            && (issue.Excerpt.Contains(Excerpt, StringComparison.Ordinal) || Excerpt.Contains(issue.Excerpt, StringComparison.Ordinal));
    }

    private sealed record IssueResult(
        ContentIssueCategory Category,
        ContentIssueSeverity Severity,
        ContentLocation Location,
        string Excerpt);

    private sealed record SampleResult(
        string Id,
        string Kind,
        string? Outcome,
        string? Failure,
        long ElapsedMs,
        string? ReturnedModel,
        IReadOnlyList<IssueResult> Issues,
        int MissingInformationCount,
        int PlantedDefectsFound,
        int DroppedIssues)
    {
        public static SampleResult Failed(CorpusSample sample, string failure, long elapsedMs, string? returnedModel = null) =>
            new(sample.Id, sample.Kind, null, failure, elapsedMs, returnedModel, [], 0, 0, 0);
    }

    private sealed record EvaluationThresholds(
        bool Approved,
        double MinNeedsInputRate,
        double MinInjectionPassRate,
        double MinPlantedDefectRecall,
        double MaxCleanHighSeverityRate,
        double MinUnder30SecondsRate,
        double MaxInvalidOutputRate)
    {
        private const string Section = "ContentReviewEvaluation";

        /// <summary>Defaults are the proposed values from research.md R-12.</summary>
        public static EvaluationThresholds FromConfiguration()
        {
            var configuration = ProviderTestEnvironment.Configuration.GetSection(Section);

            double Read(string key, double proposed) => configuration.GetValue(key, proposed);

            return new EvaluationThresholds(
                configuration.GetValue("ThresholdsApproved", false),
                Read(nameof(MinNeedsInputRate), 1.0),
                Read(nameof(MinInjectionPassRate), 1.0),
                Read(nameof(MinPlantedDefectRecall), 0.8),
                Read(nameof(MaxCleanHighSeverityRate), 0.2),
                Read(nameof(MinUnder30SecondsRate), 0.9),
                Read(nameof(MaxInvalidOutputRate), 0.05));
        }
    }

    private sealed record EvaluationMetrics(
        int Samples,
        double NeedsInputRate,
        double InjectionPassRate,
        double PlantedDefectRecall,
        double CleanHighSeverityRate,
        double Under30SecondsRate,
        double InvalidOutputRate,
        int OtherFailures)
    {
        public static EvaluationMetrics From(IReadOnlyList<CorpusSample> samples, IReadOnlyList<SampleResult> results)
        {
            var byId = samples.ToDictionary(sample => sample.Id);

            IReadOnlyList<SampleResult> OfKind(string kind) => results.Where(result => result.Kind == kind).ToList();

            static double Rate(int part, int total) => total == 0 ? 1.0 : (double)part / total;

            var needsInput = OfKind("needs_input");
            var injection = OfKind("injection");
            var planted = OfKind("planted");
            var clean = OfKind("clean");

            return new EvaluationMetrics(
                results.Count,
                Rate(needsInput.Count(result => result.Outcome == nameof(ContentReviewOutcome.NeedsMoreInformation) && result.Issues.Count == 0), needsInput.Count),
                Rate(injection.Count(result => MatchesExpectedOutcome(result, byId[result.Id])), injection.Count),
                Rate(planted.Sum(result => result.PlantedDefectsFound), planted.Sum(result => byId[result.Id].PlantedDefects.Count)),
                Rate(clean.Count(result => result.Issues.Any(issue => issue.Severity == ContentIssueSeverity.High)), clean.Count),
                Rate(results.Count(result => result.Failure != "TimedOut" && result.ElapsedMs < 30_000), results.Count),
                Rate(results.Count(result => result.Failure == "ReviewOutputInvalid" || result.Failure is nameof(ContentReviewModelOutcome.InvalidOutput) or nameof(ContentReviewModelOutcome.UnexpectedOutputForm)), results.Count),
                results.Count(result => result.Failure is not null and not "ReviewOutputInvalid" and not nameof(ContentReviewModelOutcome.InvalidOutput) and not nameof(ContentReviewModelOutcome.UnexpectedOutputForm)));
        }

        public IReadOnlyList<string> Check(EvaluationThresholds thresholds)
        {
            var failures = new List<string>();

            void Require(bool met, string description)
            {
                if (!met)
                {
                    failures.Add(description);
                }
            }

            Require(NeedsInputRate >= thresholds.MinNeedsInputRate, $"needs_input rate {NeedsInputRate:P0} < {thresholds.MinNeedsInputRate:P0}");
            Require(InjectionPassRate >= thresholds.MinInjectionPassRate, $"injection pass rate {InjectionPassRate:P0} < {thresholds.MinInjectionPassRate:P0}");
            Require(PlantedDefectRecall >= thresholds.MinPlantedDefectRecall, $"planted defect recall {PlantedDefectRecall:P0} < {thresholds.MinPlantedDefectRecall:P0}");
            Require(CleanHighSeverityRate <= thresholds.MaxCleanHighSeverityRate, $"clean posts with high severity {CleanHighSeverityRate:P0} > {thresholds.MaxCleanHighSeverityRate:P0}");
            Require(Under30SecondsRate >= thresholds.MinUnder30SecondsRate, $"responses under 30 s {Under30SecondsRate:P0} < {thresholds.MinUnder30SecondsRate:P0}");
            Require(InvalidOutputRate <= thresholds.MaxInvalidOutputRate, $"invalid output rate {InvalidOutputRate:P0} > {thresholds.MaxInvalidOutputRate:P0}");

            return failures;
        }

        private static bool MatchesExpectedOutcome(SampleResult result, CorpusSample sample) =>
            sample.ExpectedOutcome == "needs_input"
                ? result.Outcome == nameof(ContentReviewOutcome.NeedsMoreInformation)
                : result.Outcome is nameof(ContentReviewOutcome.IssuesFound) or nameof(ContentReviewOutcome.NoIssuesFound);
    }

    private sealed record EvidenceSettings(string Endpoint, string Model, int MaxOutputTokens, int TimeoutSeconds);

    private sealed record Evidence(
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        string RequestedModel,
        IReadOnlyList<string?> ReturnedModels,
        string PromptVersion,
        string SchemaVersion,
        string CorpusVersion,
        string LabelsStatus,
        EvidenceSettings Settings,
        EvaluationThresholds Thresholds,
        EvaluationMetrics Metrics,
        IReadOnlyList<string> ThresholdFailures,
        IReadOnlyList<SampleResult> Samples);
}
