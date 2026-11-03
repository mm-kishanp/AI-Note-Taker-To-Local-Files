using Chroniq.Application.Classification;
using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;
using Chroniq.Domain.Rules;

namespace Chroniq.UnitTests.Classification;

public class ClassificationEngineTests
{
    private static readonly Guid Motor = Guid.NewGuid();
    private static readonly Guid Film = Guid.NewGuid();
    private static readonly Guid Premiere = Guid.NewGuid();
    private static readonly Guid ClientB = Guid.NewGuid();

    private static RuleSpec Rule(RuleKind kind, string value) => new(Guid.NewGuid(), kind, value, TextNormalizer.ForRule(kind, value));

    private static ProjectProfile Project(Guid id, string name, Guid? client = null, int priority = 100, bool active = true, params RuleSpec[] rules) =>
        new(id, name, client, priority, active, rules);

    private static MeetingFacts Facts(
        string title = "Weekly sync",
        (string? Name, string? Email)[]? attendees = null,
        Dictionary<string, int>? speakers = null,
        string[]? invitees = null,
        string[]? folders = null,
        string? summary = null) => new()
        {
            Title = title,
            OwnerName = "Kishan Parmar",
            OwnerEmail = "owner@me.com",
            Attendees = (attendees ?? []).Select(a => new ParticipantFact(a.Name, a.Email)).ToList(),
            IdentifiedSpeakers = speakers ?? new Dictionary<string, int>(),
            InviteeEmails = invitees ?? [],
            FolderNames = folders ?? [],
            FolderIds = [],
            SummaryText = summary,
        };

    private static readonly ClassificationSettings Defaults = new();

    [Fact]
    public void Exact_email_mapping_assigns_with_certainty()
    {
        var projects = new[] { Project(Motor, "Motor Dealer App", rules: Rule(RuleKind.Email, "Alice@Client-A.com")) };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Alice", "alice@client-a.com")]), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.Assigned, r.Status);
        Assert.Equal(Motor, r.PrimaryProjectId);
        Assert.Equal(Certainty.Certain, r.Certainty);
        Assert.Equal(ClassificationTier.ParticipantEmail, r.Tier);
        Assert.Contains("alice@client-a.com", r.Reason);
    }

    [Fact]
    public void Exact_speaker_mapping_assigns()
    {
        var projects = new[] { Project(Film, "FilmWale", rules: Rule(RuleKind.SpeakerName, "Ravi Kumar")) };
        var r = ClassificationEngine.Classify(Facts(speakers: new() { ["ravi  KUMAR"] = 12 }), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.Assigned, r.Status);
        Assert.Equal(Film, r.PrimaryProjectId);
        Assert.Equal(ClassificationTier.SpeakerIdentity, r.Tier);
    }

    [Fact]
    public void Manual_assignment_takes_priority_and_records_conflict()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: Rule(RuleKind.Email, "alice@a.com")),
            Project(Film, "Film"),
        };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Alice", "alice@a.com")]), projects, [], Defaults,
            new ManualDecision(ManualAssignmentKind.Project, Film));

        Assert.Equal(AssignmentMethod.Manual, r.Method);
        Assert.Equal(Film, r.PrimaryProjectId);
        Assert.Single(r.Conflicts);
        Assert.Equal(Motor, r.Conflicts[0].ProjectId);
    }

    [Fact]
    public void Stronger_tier_wins_over_weaker_conflicting_evidence()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: Rule(RuleKind.Email, "alice@a.com")),
            Project(Film, "Film", rules: Rule(RuleKind.SpeakerName, "Bob")),
        };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Alice", "alice@a.com")], speakers: new() { ["Bob"] = 5 }), projects, [], Defaults);

        Assert.Equal(Motor, r.PrimaryProjectId);
        Assert.Contains(r.Conflicts, c => c.ProjectId == Film && c.Tier == ClassificationTier.SpeakerIdentity);
    }

    [Fact]
    public void Conflicting_participant_mappings_in_same_tier_go_to_review()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: Rule(RuleKind.Email, "alice@a.com")),
            Project(Film, "Film", rules: Rule(RuleKind.Email, "bob@b.com")),
        };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Alice", "alice@a.com"), ("Bob", "bob@b.com")]), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.NeedsReview, r.Status);
        Assert.True(r.NeedsReview);
        Assert.Null(r.PrimaryProjectId);
        Assert.Equal(2, r.CandidateProjectIds.Count);
    }

    [Fact]
    public void Client_with_multiple_projects_is_ambiguous_without_distinguishing_rule()
    {
        var clients = new[] { new ClientProfile(ClientB, "Client B", [Rule(RuleKind.EmailDomain, "clientb.com")]) };
        var projects = new[] { Project(Film, "FilmWale", ClientB), Project(Premiere, "Premiere Management", ClientB) };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Raj", "raj@clientb.com")]), projects, clients, Defaults);

        Assert.Equal(ClassificationStatus.NeedsReview, r.Status);
        Assert.Equal(ClassificationTier.Client, r.Tier);
        Assert.Equal(new[] { Film, Premiere }.OrderBy(x => x), r.CandidateProjectIds.OrderBy(x => x));
    }

    [Fact]
    public void Client_with_multiple_projects_is_narrowed_by_keyword()
    {
        var clients = new[] { new ClientProfile(ClientB, "Client B", [Rule(RuleKind.EmailDomain, "@clientb.com")]) };
        var projects = new[]
        {
            Project(Film, "FilmWale", ClientB, rules: Rule(RuleKind.TitleKeyword, "filmwale")),
            Project(Premiere, "Premiere Management", ClientB, rules: Rule(RuleKind.TitleKeyword, "premiere")),
        };
        var r = ClassificationEngine.Classify(Facts(title: "Premiere roadmap", attendees: [("Raj", "raj@clientb.com")]), projects, clients, Defaults);

        Assert.Equal(ClassificationStatus.Assigned, r.Status);
        Assert.Equal(Premiere, r.PrimaryProjectId);
    }

    [Fact]
    public void Client_with_single_project_assigns_with_medium_certainty()
    {
        var clients = new[] { new ClientProfile(ClientB, "Client B", [Rule(RuleKind.EmailDomain, "clientb.com")]) };
        var projects = new[] { Project(Film, "FilmWale", ClientB) };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Raj", "raj@clientb.com")]), projects, clients, Defaults);

        Assert.Equal(Film, r.PrimaryProjectId);
        Assert.Equal(Certainty.Medium, r.Certainty);
    }

    [Fact]
    public void Anonymous_speaker_labels_never_identify_people()
    {
        // Only resolved names are passed as identified speakers; a rule named after a diarization label must not match attendees either.
        var projects = new[] { Project(Motor, "Motor", rules: Rule(RuleKind.SpeakerName, "Speaker A")) };
        var r = ClassificationEngine.Classify(Facts(attendees: [(null, "x@y.com")]), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.Unassigned, r.Status);
    }

    [Fact]
    public void Unknown_participant_falls_back_to_unassigned_with_explanation()
    {
        var projects = new[] { Project(Motor, "Motor", rules: Rule(RuleKind.Email, "alice@a.com")) };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Stranger", "s@unknown.org")]), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.Unassigned, r.Status);
        Assert.True(r.NeedsReview);
        Assert.False(string.IsNullOrWhiteSpace(r.Reason));
        Assert.NotNull(r.SuggestedNextAction);
    }

    [Fact]
    public void Owner_email_and_name_are_ignored_by_default()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: [Rule(RuleKind.Email, "owner@me.com"), Rule(RuleKind.SpeakerName, "Kishan Parmar")]),
        };
        var r = ClassificationEngine.Classify(Facts(attendees: [("Kishan Parmar", "owner@me.com")], speakers: new() { ["Kishan Parmar"] = 40 }), projects, [], Defaults);

        Assert.Equal(ClassificationStatus.Unassigned, r.Status);
    }

    [Fact]
    public void Ai_disabled_is_never_consulted_even_with_suggestion()
    {
        var projects = new[] { Project(Motor, "Motor") };
        var ai = new AiSuggestion(Motor.ToString(), "looks like motor", []);
        var r = ClassificationEngine.Classify(Facts(), projects, [], Defaults, ai: ai);

        Assert.Equal(ClassificationStatus.Unassigned, r.Status);
    }

    [Fact]
    public void Ai_suggestion_goes_to_review_unless_auto_assign()
    {
        var projects = new[] { Project(Motor, "Motor") };
        var ai = new AiSuggestion(Motor.ToString(), "mentions dealer inventory", []);
        var enabled = Defaults with { Ai = new AiSettings { Enabled = true } };

        var review = ClassificationEngine.Classify(Facts(), projects, [], enabled, ai: ai);
        Assert.Equal(ClassificationStatus.NeedsReview, review.Status);
        Assert.Equal([Motor], review.CandidateProjectIds);

        var auto = ClassificationEngine.Classify(Facts(), projects, [], enabled with { Ai = new AiSettings { Enabled = true, AutoAssign = true } }, ai: ai);
        Assert.Equal(ClassificationStatus.Assigned, auto.Status);
        Assert.Equal(AssignmentMethod.Ai, auto.Method);
        Assert.Equal(Certainty.Low, auto.Certainty);
    }

    [Fact]
    public void Invalid_ai_project_id_is_rejected()
    {
        var projects = new[] { Project(Motor, "Motor") };
        var enabled = Defaults with { Ai = new AiSettings { Enabled = true, AutoAssign = true } };
        var r = ClassificationEngine.Classify(Facts(), projects, [], enabled, ai: new AiSuggestion(Guid.NewGuid().ToString(), "made up", []));

        Assert.Equal(ClassificationStatus.Unassigned, r.Status);
    }

    [Fact]
    public void Ai_never_overrides_deterministic_rules_or_manual()
    {
        var projects = new[] { Project(Motor, "Motor", rules: Rule(RuleKind.Email, "a@a.com")), Project(Film, "Film") };
        var enabled = Defaults with { Ai = new AiSettings { Enabled = true, AutoAssign = true } };
        var ai = new AiSuggestion(Film.ToString(), "film", []);

        var r = ClassificationEngine.Classify(Facts(attendees: [("A", "a@a.com")]), projects, [], enabled, ai: ai);
        Assert.Equal(Motor, r.PrimaryProjectId);

        var manual = ClassificationEngine.Classify(Facts(), projects, [], enabled, new ManualDecision(ManualAssignmentKind.Project, Motor), ai);
        Assert.Equal(Motor, manual.PrimaryProjectId);
        Assert.Equal(AssignmentMethod.Manual, manual.Method);
    }

    [Fact]
    public void Archived_projects_are_not_auto_assigned_but_manual_still_works()
    {
        var projects = new[] { Project(Motor, "Motor", active: false, rules: Rule(RuleKind.Email, "a@a.com")) };
        var auto = ClassificationEngine.Classify(Facts(attendees: [("A", "a@a.com")]), projects, [], Defaults);
        Assert.Equal(ClassificationStatus.Unassigned, auto.Status);

        var manual = ClassificationEngine.Classify(Facts(), projects, [], Defaults, new ManualDecision(ManualAssignmentKind.Project, Motor));
        Assert.Equal(Motor, manual.PrimaryProjectId);
    }

    [Fact]
    public void Multi_project_modes()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", priority: 5, rules: Rule(RuleKind.Email, "a@a.com")),
            Project(Film, "Film", priority: 1, rules: Rule(RuleKind.Email, "b@b.com")),
        };
        var facts = Facts(attendees: [("A", "a@a.com"), ("B", "b@b.com")]);

        var shared = ClassificationEngine.Classify(facts, projects, [], Defaults with { MultiProjectMode = MultiProjectMode.Shared });
        Assert.Equal(ClassificationStatus.Shared, shared.Status);
        Assert.False(shared.NeedsReview);

        var multi = ClassificationEngine.Classify(facts, projects, [], Defaults with { MultiProjectMode = MultiProjectMode.MultipleExports });
        Assert.Equal(ClassificationStatus.MultiProject, multi.Status);
        Assert.Equal(Film, multi.PrimaryProjectId); // lower priority value wins
        Assert.Equal([Film, Motor], multi.ProjectIds);
    }

    [Fact]
    public void Manual_shared_and_excluded()
    {
        var projects = new[] { Project(Motor, "Motor") };
        Assert.Equal(ClassificationStatus.Shared, ClassificationEngine.Classify(Facts(), projects, [], Defaults, new ManualDecision(ManualAssignmentKind.Shared, null)).Status);
        Assert.Equal(ClassificationStatus.Excluded, ClassificationEngine.Classify(Facts(), projects, [], Defaults, new ManualDecision(ManualAssignmentKind.Excluded, null)).Status);
    }

    [Fact]
    public void Manual_assignment_to_deleted_project_falls_back_and_flags_review()
    {
        var projects = new[] { Project(Motor, "Motor", rules: Rule(RuleKind.Email, "a@a.com")) };
        var r = ClassificationEngine.Classify(Facts(attendees: [("A", "a@a.com")]), projects, [], Defaults, new ManualDecision(ManualAssignmentKind.Project, Guid.NewGuid()));
        Assert.Equal(Motor, r.PrimaryProjectId);
        Assert.True(r.NeedsReview);
    }

    [Fact]
    public void Disabled_tiers_and_custom_order_are_respected()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: Rule(RuleKind.Email, "a@a.com")),
            Project(Film, "Film", rules: Rule(RuleKind.TitleKeyword, "film")),
        };
        var facts = Facts(title: "Film review", attendees: [("A", "a@a.com")]);

        var keywordFirst = Defaults with { TierOrder = [ClassificationTier.Keyword, ClassificationTier.ParticipantEmail] };
        Assert.Equal(Film, ClassificationEngine.Classify(facts, projects, [], keywordFirst).PrimaryProjectId);

        var emailDisabled = Defaults with { DisabledTiers = [ClassificationTier.ParticipantEmail] };
        Assert.Equal(Film, ClassificationEngine.Classify(facts, projects, [], emailDisabled).PrimaryProjectId);
    }

    [Fact]
    public void Calendar_invitees_and_granola_folders_are_used()
    {
        var projects = new[]
        {
            Project(Motor, "Motor", rules: Rule(RuleKind.Email, "dealer@a.com")),
            Project(Film, "Film", rules: Rule(RuleKind.GranolaFolder, "FilmWale Calls")),
        };
        Assert.Equal(Motor, ClassificationEngine.Classify(Facts(invitees: ["Dealer@A.com"]), projects, [], Defaults).PrimaryProjectId);
        Assert.Equal(Film, ClassificationEngine.Classify(Facts(folders: ["filmwale calls"]), projects, [], Defaults).PrimaryProjectId);
        Assert.Equal(ClassificationStatus.Unassigned,
            ClassificationEngine.Classify(Facts(invitees: ["dealer@a.com"]), projects, [], Defaults with { UseCalendarInvitees = false }).Status);
    }

    [Theory]
    [InlineData("Premiere roadmap", "premiere", true)]
    [InlineData("Premiered roadmap", "premiere", false)]
    [InlineData("Weekly: tanyo/backend", "tanyo", true)]
    [InlineData("ગુજરાતી પ્રોજેક્ટ મીટિંગ", "પ્રોજેક્ટ", true)]
    [InlineData("हिंदी परियोजना", "परियोजना", true)]
    [InlineData("", "x", false)]
    public void Keyword_matching_uses_word_boundaries_and_unicode(string text, string keyword, bool expected) =>
        Assert.Equal(expected, KeywordMatcher.Contains(text, TextNormalizer.Name(keyword)));
}
