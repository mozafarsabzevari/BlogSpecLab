namespace BlogSpecLab.Core.Domain.ContentReviews;

public enum ContentReviewOutcome
{
    IssuesFound,
    NoIssuesFound,
    NeedsMoreInformation
}

public enum ContentIssueCategory
{
    SpellingAndWriting,
    Grammar,
    Clarity,
    StructureAndCoherence,
    TitleBodyMismatch,
    ClaimNeedsSource
}

/// <summary>Declared from most to least severe; issues are ordered by this value.</summary>
public enum ContentIssueSeverity
{
    High,
    Medium,
    Low
}

public enum ContentLocation
{
    Title,
    Body
}
