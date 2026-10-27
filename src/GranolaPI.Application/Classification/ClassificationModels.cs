using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Classification;

public sealed record ParticipantFact(string? Name, string? Email);

/// <summary>Everything the engine may look at. Built from source metadata only; nothing is inferred.</summary>
public sealed record MeetingFacts
{
    public string Title { get; init; } = "";
    public string? OwnerName { get; init; }
    public string? OwnerEmail { get; init; }
    public IReadOnlyList<ParticipantFact> Attendees { get; init; } = [];
    public IReadOnlyList<string> InviteeEmails { get; init; } = [];
    /// <summary>Speaker names Granola resolved in the transcript, with segment counts. Anonymous labels are excluded.</summary>
    public IReadOnlyDictionary<string, int> IdentifiedSpeakers { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> FolderNames { get; init; } = [];
    public IReadOnlyList<string> FolderIds { get; init; } = [];
    public string? SummaryText { get; init; }
}

public sealed record RuleSpec(Guid Id, RuleKind Kind, string Value, string NormalizedValue);

public sealed record ProjectProfile(
    Guid Id,
    string Name,
    Guid? ClientId,
    int Priority,
    bool Active,
    IReadOnlyList<RuleSpec> Rules,
    string? Description = null);

public sealed record ClientProfile(Guid Id, string Name, IReadOnlyList<RuleSpec> Rules);

public sealed record ManualDecision(ManualAssignmentKind Kind, Guid? ProjectId);

public sealed record AiSuggestion(string? ProjectId, string Explanation, IReadOnlyList<string> Evidence);

public sealed record Evidence(
    ClassificationTier Tier,
    Guid ProjectId,
    string ProjectName,
    RuleKind? RuleKind,
    string RuleValue,
    string MatchedValue,
    Guid? RuleId,
    Guid? ClientId = null,
    string? ClientName = null)
{
    public string Describe() => Tier switch
    {
        ClassificationTier.Client =>
            $"{RuleKind} rule '{RuleValue}' of client '{ClientName}' matched '{MatchedValue}' (client project '{ProjectName}')",
        ClassificationTier.Ai => $"AI suggestion for '{ProjectName}': {MatchedValue}",
        _ => $"{RuleKind} rule '{RuleValue}' matched '{MatchedValue}' -> '{ProjectName}'",
    };
}

public sealed record ClassificationConflict(Guid ProjectId, string ProjectName, ClassificationTier Tier, string Description);

public sealed record ClassificationResult
{
    public ClassificationStatus Status { get; init; }
    public AssignmentMethod Method { get; init; }
    public Certainty Certainty { get; init; }
    public Guid? PrimaryProjectId { get; init; }
    /// <summary>All projects the meeting is exported to (primary first). Empty for Unassigned/NeedsReview/Shared.</summary>
    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
    public ClassificationTier? Tier { get; init; }
    public IReadOnlyList<Evidence> Evidence { get; init; } = [];
    public IReadOnlyList<Guid> CandidateProjectIds { get; init; } = [];
    public IReadOnlyList<ClassificationConflict> Conflicts { get; init; } = [];
    public bool NeedsReview { get; init; }
    public string Reason { get; init; } = "";
    public string? SuggestedNextAction { get; init; }
    public AiSuggestion? AiSuggestion { get; init; }
}
