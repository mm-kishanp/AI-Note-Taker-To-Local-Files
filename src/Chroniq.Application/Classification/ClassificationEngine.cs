using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;
using Chroniq.Domain.Rules;

namespace Chroniq.Application.Classification;

/// <summary>
/// Deterministic, explainable project classification.
///
/// Tiers are evaluated in the configured order (default: participant email, participant identity,
/// verified speaker identity, client mapping, Granola metadata, keywords, AI). The first tier with any
/// evidence decides. When that tier points at several projects, later tiers may narrow the candidate
/// set; if they cannot, the configured multi-project mode decides (default: send to review).
/// A manual decision always wins; automatic results that disagree are recorded as conflicts.
/// </summary>
public static class ClassificationEngine
{
    public static ClassificationResult Classify(
        MeetingFacts facts,
        IReadOnlyList<ProjectProfile> projects,
        IReadOnlyList<ClientProfile> clients,
        ClassificationSettings settings,
        ManualDecision? manual = null,
        AiSuggestion? ai = null)
    {
        var auto = ClassifyAutomatically(facts, projects, clients, settings, ai);
        if (manual is null || manual.Kind == ManualAssignmentKind.None) return auto;
        return ApplyManual(manual, auto, projects);
    }

    private static ClassificationResult ApplyManual(ManualDecision manual, ClassificationResult auto, IReadOnlyList<ProjectProfile> projects)
    {
        var autoProjects = auto.ProjectIds.Count > 0 ? auto.ProjectIds : auto.CandidateProjectIds;
        List<ClassificationConflict> ConflictsAgainst(Guid? manualProject) =>
            autoProjects.Where(p => p != manualProject)
                .Select(p => new ClassificationConflict(p, NameOf(projects, p), auto.Tier ?? ClassificationTier.Keyword,
                    $"Automatic rules point to '{NameOf(projects, p)}' but the manual assignment is kept."))
                .ToList();

        switch (manual.Kind)
        {
            case ManualAssignmentKind.Excluded:
                return new ClassificationResult
                {
                    Status = ClassificationStatus.Excluded,
                    Method = AssignmentMethod.Manual,
                    Certainty = Certainty.Certain,
                    Reason = "Manually excluded from export.",
                    Evidence = auto.Evidence,
                };
            case ManualAssignmentKind.Shared:
                return new ClassificationResult
                {
                    Status = ClassificationStatus.Shared,
                    Method = AssignmentMethod.Manual,
                    Certainty = Certainty.Certain,
                    Reason = "Manually assigned to Shared.",
                    Evidence = auto.Evidence,
                    Conflicts = ConflictsAgainst(null),
                };
            case ManualAssignmentKind.Project when manual.ProjectId is Guid pid && projects.Any(p => p.Id == pid):
                return new ClassificationResult
                {
                    Status = ClassificationStatus.Assigned,
                    Method = AssignmentMethod.Manual,
                    Certainty = Certainty.Certain,
                    PrimaryProjectId = pid,
                    ProjectIds = [pid],
                    Reason = $"Manually assigned to '{NameOf(projects, pid)}'.",
                    Evidence = auto.Evidence,
                    Conflicts = ConflictsAgainst(pid),
                };
            default:
                // Manual project no longer exists: fall back to automatic, but flag for review.
                return auto with
                {
                    NeedsReview = true,
                    Reason = "The manually assigned project no longer exists. " + auto.Reason,
                    SuggestedNextAction = "Reassign this meeting.",
                };
        }
    }

    private static ClassificationResult ClassifyAutomatically(
        MeetingFacts facts,
        IReadOnlyList<ProjectProfile> projects,
        IReadOnlyList<ClientProfile> clients,
        ClassificationSettings settings,
        AiSuggestion? ai)
    {
        var active = projects.Where(p => p.Active).ToList();
        var tiers = settings.TierOrder.Distinct().Where(t => !settings.DisabledTiers.Contains(t)).ToList();
        if (!settings.Ai.Enabled) tiers.Remove(ClassificationTier.Ai);

        var participants = Participants(facts, settings);
        var evidenceByTier = tiers.ToDictionary(t => t, t => Collect(t, facts, participants, active, clients, settings, ai));

        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            var evidence = evidenceByTier[tier];
            if (evidence.Count == 0) continue;

            var candidates = evidence.Select(e => e.ProjectId).Distinct().ToList();
            var used = new List<Evidence>(evidence);

            // Narrow ambiguous results using weaker tiers, restricted to the current candidates.
            for (var j = i + 1; j < tiers.Count && candidates.Count > 1; j++)
            {
                if (tiers[j] == ClassificationTier.Ai) continue;
                var narrowing = evidenceByTier[tiers[j]].Where(e => candidates.Contains(e.ProjectId)).ToList();
                var narrowed = narrowing.Select(e => e.ProjectId).Distinct().ToList();
                if (narrowed.Count > 0 && narrowed.Count < candidates.Count)
                {
                    candidates = narrowed;
                    used.AddRange(narrowing);
                }
            }

            var conflicts = evidenceByTier
                .Where(kv => kv.Key != tier)
                .SelectMany(kv => kv.Value)
                .Where(e => !candidates.Contains(e.ProjectId))
                .GroupBy(e => (e.ProjectId, e.Tier))
                .Select(g => new ClassificationConflict(g.Key.ProjectId, g.First().ProjectName, g.Key.Tier,
                    string.Join("; ", g.Select(e => e.Describe()))))
                .ToList();

            if (tier == ClassificationTier.Ai)
                return FromAi(candidates[0], used, conflicts, active, settings, ai!);

            if (candidates.Count == 1)
            {
                var pid = candidates[0];
                var mine = used.Where(e => e.ProjectId == pid).ToList();
                return new ClassificationResult
                {
                    Status = ClassificationStatus.Assigned,
                    Method = AssignmentMethod.Rule,
                    Certainty = mine.Max(CertaintyOf),
                    PrimaryProjectId = pid,
                    ProjectIds = [pid],
                    Tier = tier,
                    Evidence = used,
                    CandidateProjectIds = [pid],
                    Conflicts = conflicts,
                    NeedsReview = false,
                    Reason = $"{TierLabel(tier)}: " + string.Join("; ", mine.Select(e => e.Describe())),
                };
            }

            var ordered = candidates
                .Select(id => active.First(p => p.Id == id))
                .OrderBy(p => p.Priority).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Id).ToList();
            var names = string.Join(", ", ordered.Select(id => $"'{NameOf(active, id)}'"));
            var baseResult = new ClassificationResult
            {
                Method = AssignmentMethod.Rule,
                Tier = tier,
                Evidence = used,
                CandidateProjectIds = ordered,
                Conflicts = conflicts,
            };

            return settings.MultiProjectMode switch
            {
                MultiProjectMode.Shared => baseResult with
                {
                    Status = ClassificationStatus.Shared,
                    Certainty = Certainty.Medium,
                    Reason = $"{TierLabel(tier)} matched several projects ({names}); saved to Shared.",
                },
                MultiProjectMode.MultipleExports => baseResult with
                {
                    Status = ClassificationStatus.MultiProject,
                    Certainty = used.Max(CertaintyOf),
                    PrimaryProjectId = ordered[0],
                    ProjectIds = ordered,
                    Reason = $"{TierLabel(tier)} matched several projects ({names}); exported to each.",
                },
                _ => baseResult with
                {
                    Status = ClassificationStatus.NeedsReview,
                    Certainty = Certainty.Low,
                    NeedsReview = true,
                    Reason = $"Ambiguous: {TierLabel(tier).ToLowerInvariant()} matched several projects ({names}) and no other rule could decide.",
                    SuggestedNextAction = "Choose the project, or add a keyword/email rule that distinguishes these projects.",
                },
            };
        }

        return new ClassificationResult
        {
            Status = ClassificationStatus.Unassigned,
            Method = AssignmentMethod.None,
            Certainty = Certainty.None,
            NeedsReview = true,
            Reason = participants.Count == 0 && facts.IdentifiedSpeakers.Count == 0
                ? "No participant emails, attendee names or identified speakers were available, and no metadata or keyword rule matched."
                : "No configured rule matched any participant, speaker, folder or keyword.",
            SuggestedNextAction = "Assign a project, or save a participant mapping from this meeting.",
            AiSuggestion = ai,
        };
    }

    private static ClassificationResult FromAi(
        Guid projectId, List<Evidence> used, List<ClassificationConflict> conflicts,
        IReadOnlyList<ProjectProfile> active, ClassificationSettings settings, AiSuggestion ai)
    {
        var name = NameOf(active, projectId);
        if (settings.Ai.AutoAssign)
        {
            return new ClassificationResult
            {
                Status = ClassificationStatus.Assigned,
                Method = AssignmentMethod.Ai,
                Certainty = Certainty.Low,
                PrimaryProjectId = projectId,
                ProjectIds = [projectId],
                Tier = ClassificationTier.Ai,
                Evidence = used,
                CandidateProjectIds = [projectId],
                Conflicts = conflicts,
                Reason = $"AI-assisted (no deterministic rule matched): '{name}'. {ai.Explanation}",
                AiSuggestion = ai,
            };
        }
        return new ClassificationResult
        {
            Status = ClassificationStatus.NeedsReview,
            Method = AssignmentMethod.None,
            Certainty = Certainty.Low,
            Tier = ClassificationTier.Ai,
            Evidence = used,
            CandidateProjectIds = [projectId],
            Conflicts = conflicts,
            NeedsReview = true,
            Reason = $"No deterministic rule matched. AI suggests '{name}': {ai.Explanation}",
            SuggestedNextAction = "Confirm the AI suggestion or choose another project.",
            AiSuggestion = ai,
        };
    }

    private static List<ParticipantFact> Participants(MeetingFacts facts, ClassificationSettings settings)
    {
        var owner = TextNormalizer.Email(facts.OwnerEmail);
        var list = facts.Attendees.ToList();
        if (settings.UseCalendarInvitees)
        {
            foreach (var email in facts.InviteeEmails)
            {
                if (!list.Any(a => TextNormalizer.Email(a.Email) == TextNormalizer.Email(email)))
                    list.Add(new ParticipantFact(null, email));
            }
        }
        if (settings.IgnoreOwner && owner.Length > 0)
            list = list.Where(a => TextNormalizer.Email(a.Email) != owner).ToList();
        return list;
    }

    private static List<Evidence> Collect(
        ClassificationTier tier,
        MeetingFacts facts,
        List<ParticipantFact> participants,
        List<ProjectProfile> projects,
        IReadOnlyList<ClientProfile> clients,
        ClassificationSettings settings,
        AiSuggestion? ai)
    {
        var result = new List<Evidence>();
        var emails = participants.Select(p => TextNormalizer.Email(p.Email)).Where(e => e.Length > 0).Distinct().ToList();
        var names = participants.Select(p => (Raw: p.Name, Norm: TextNormalizer.Name(p.Name))).Where(n => n.Norm.Length > 0).ToList();
        var ownerName = settings.IgnoreOwner ? TextNormalizer.Name(facts.OwnerName) : "";
        var speakers = facts.IdentifiedSpeakers
            .Where(kv => kv.Value >= settings.MinimumSpeakerSegments)
            .Select(kv => (Raw: kv.Key, Norm: TextNormalizer.Name(kv.Key), Count: kv.Value))
            .Where(s => s.Norm.Length > 0 && s.Norm != ownerName)
            .ToList();

        IEnumerable<(RuleSpec Rule, string Matched)> Match(IEnumerable<RuleSpec> rules, bool forClient)
        {
            foreach (var r in rules)
            {
                switch (r.Kind)
                {
                    case RuleKind.Email when tier == ClassificationTier.ParticipantEmail || forClient:
                        foreach (var e in emails.Where(e => e == r.NormalizedValue)) yield return (r, e);
                        break;
                    case RuleKind.EmailDomain when tier == ClassificationTier.ParticipantIdentity || forClient:
                        foreach (var e in emails.Where(e => e.EndsWith(r.NormalizedValue, StringComparison.Ordinal))) yield return (r, e);
                        break;
                    case RuleKind.ParticipantName when tier == ClassificationTier.ParticipantIdentity || forClient:
                        foreach (var n in names.Where(n => n.Norm == r.NormalizedValue)) yield return (r, $"attendee {n.Raw}");
                        break;
                }
                if (r.Kind is RuleKind.SpeakerName or RuleKind.ParticipantName && (tier == ClassificationTier.SpeakerIdentity || forClient))
                {
                    foreach (var s in speakers.Where(s => s.Norm == r.NormalizedValue))
                        yield return (r, $"speaker {s.Raw} ({s.Count} segments)");
                }
            }
        }

        switch (tier)
        {
            case ClassificationTier.ParticipantEmail:
            case ClassificationTier.ParticipantIdentity:
            case ClassificationTier.SpeakerIdentity:
                foreach (var p in projects)
                    foreach (var (rule, matched) in Match(p.Rules, forClient: false))
                        result.Add(new Evidence(tier, p.Id, p.Name, rule.Kind, rule.Value, matched, rule.Id));
                break;

            case ClassificationTier.Client:
                foreach (var c in clients)
                {
                    var hits = Match(c.Rules, forClient: true).ToList();
                    if (hits.Count == 0) continue;
                    foreach (var p in projects.Where(p => p.ClientId == c.Id))
                        foreach (var (rule, matched) in hits)
                            result.Add(new Evidence(tier, p.Id, p.Name, rule.Kind, rule.Value, matched, rule.Id, c.Id, c.Name));
                }
                break;

            case ClassificationTier.Metadata:
                var folderNames = facts.FolderNames.Select(TextNormalizer.Name).ToList();
                var folderIds = facts.FolderIds.Select(TextNormalizer.Name).ToList();
                foreach (var p in projects)
                    foreach (var r in p.Rules.Where(r => r.Kind == RuleKind.GranolaFolder))
                    {
                        var idx = folderNames.IndexOf(r.NormalizedValue);
                        if (idx < 0) idx = folderIds.IndexOf(r.NormalizedValue);
                        if (idx >= 0)
                            result.Add(new Evidence(tier, p.Id, p.Name, r.Kind, r.Value, $"Granola folder {facts.FolderNames[idx]}", r.Id));
                    }
                break;

            case ClassificationTier.Keyword:
                foreach (var p in projects)
                    foreach (var r in p.Rules.Where(r => r.Kind is RuleKind.TitleKeyword or RuleKind.Keyword))
                    {
                        if (KeywordMatcher.Contains(facts.Title, r.NormalizedValue))
                            result.Add(new Evidence(tier, p.Id, p.Name, r.Kind, r.Value, $"title \"{facts.Title}\"", r.Id));
                        else if (r.Kind == RuleKind.Keyword && KeywordMatcher.Contains(facts.SummaryText, r.NormalizedValue))
                            result.Add(new Evidence(tier, p.Id, p.Name, r.Kind, r.Value, "summary text", r.Id));
                    }
                break;

            case ClassificationTier.Ai:
                if (ai?.ProjectId is string id && Guid.TryParse(id, out var gid) && projects.FirstOrDefault(p => p.Id == gid) is { } proj)
                    result.Add(new Evidence(tier, proj.Id, proj.Name, null, "ai", ai.Explanation, null));
                break;
        }
        return result;
    }

    private static Certainty CertaintyOf(Evidence e) => e.Tier switch
    {
        ClassificationTier.ParticipantEmail => Certainty.Certain,
        ClassificationTier.ParticipantIdentity => e.RuleKind == RuleKind.EmailDomain ? Certainty.Medium : Certainty.High,
        ClassificationTier.SpeakerIdentity => Certainty.High,
        ClassificationTier.Metadata => Certainty.High,
        ClassificationTier.Client => Certainty.Medium,
        ClassificationTier.Keyword => Certainty.Medium,
        _ => Certainty.Low,
    };

    public static string TierLabel(ClassificationTier tier) => tier switch
    {
        ClassificationTier.ParticipantEmail => "Participant email match",
        ClassificationTier.ParticipantIdentity => "Participant identity match",
        ClassificationTier.SpeakerIdentity => "Verified speaker match",
        ClassificationTier.Client => "Client mapping",
        ClassificationTier.Metadata => "Granola folder match",
        ClassificationTier.Keyword => "Keyword match",
        ClassificationTier.Ai => "AI suggestion",
        _ => tier.ToString(),
    };

    private static string NameOf(IReadOnlyList<ProjectProfile> projects, Guid id) =>
        projects.FirstOrDefault(p => p.Id == id)?.Name ?? id.ToString();
}

public static class KeywordMatcher
{
    /// <summary>Case-insensitive match on whole words/phrases. Works for non-Latin scripts (letters and marks count as word characters).</summary>
    public static bool Contains(string? haystack, string normalizedKeyword)
    {
        if (string.IsNullOrEmpty(haystack) || normalizedKeyword.Length == 0) return false;
        var text = TextNormalizer.Name(haystack);
        var start = 0;
        while (true)
        {
            var idx = text.IndexOf(normalizedKeyword, start, StringComparison.Ordinal);
            if (idx < 0) return false;
            var before = idx == 0 || !IsWordChar(text[idx - 1]);
            var end = idx + normalizedKeyword.Length;
            var after = end >= text.Length || !IsWordChar(text[end]);
            if (before && after) return true;
            start = idx + 1;
        }
    }

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark;
}
