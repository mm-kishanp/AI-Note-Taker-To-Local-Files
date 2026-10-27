using GranolaPI.Application.Export;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GranolaPI.Application.Classification;

public sealed record ClassificationContext(
    IReadOnlyList<ProjectProfile> Projects,
    IReadOnlyList<ClientProfile> Clients,
    IReadOnlyDictionary<Guid, ProjectInfo> ProjectInfos,
    ClassificationSettings Settings);

public sealed record AiClassificationInput(
    string Title,
    IReadOnlyList<ParticipantFact> Participants,
    IReadOnlyList<string> IdentifiedSpeakers,
    IReadOnlyList<string> GranolaFolders,
    string? Summary,
    IReadOnlyList<AiProjectOption> Projects);

public sealed record AiProjectOption(string Id, string Name, string? Description, string? Client);

/// <summary>Optional AI-assisted classification. The application works fully without it.</summary>
public interface IAiClassifier
{
    bool IsConfigured(AiSettings settings);
    Task<AiSuggestion?> SuggestAsync(AiClassificationInput input, AiSettings settings, CancellationToken ct);
}

public sealed class NullAiClassifier : IAiClassifier
{
    public bool IsConfigured(AiSettings settings) => false;
    public Task<AiSuggestion?> SuggestAsync(AiClassificationInput input, AiSettings settings, CancellationToken ct) =>
        Task.FromResult<AiSuggestion?>(null);
}

public sealed class ClassificationService(IAppDbContext db, ISettingsService settings, IAiClassifier ai, ILogger<ClassificationService> logger)
{
    public async Task<ClassificationContext> LoadContextAsync(CancellationToken ct)
    {
        var cfg = await settings.GetSectionAsync<ClassificationSettings>(SettingKeys.Classification, ct);
        var projects = await db.Projects.AsNoTracking().Include(p => p.Client).Include(p => p.Rules).ToListAsync(ct);
        var clients = await db.Clients.AsNoTracking().Include(c => c.Rules).ToListAsync(ct);
        return new ClassificationContext(
            projects.Select(ToProfile).ToList(),
            clients.Select(c => new ClientProfile(c.Id, c.Name, c.Rules.Where(r => r.Enabled).Select(ToSpec).ToList())).ToList(),
            projects.ToDictionary(p => p.Id, ProjectInfo.From),
            cfg);
    }

    public static ProjectProfile ToProfile(Project p) =>
        new(p.Id, p.Name, p.ClientId, p.DefaultClassificationPriority, p.Status == ProjectStatus.Active,
            p.Rules.Where(r => r.Enabled).Select(ToSpec).ToList(), p.Description);

    public static RuleSpec ToSpec(ClassificationRule r) => new(r.Id, r.Kind, r.Value, r.NormalizedValue);

    public async Task<ClassificationResult> ClassifyAsync(Meeting meeting, ClassificationContext ctx, CancellationToken ct)
    {
        var facts = meeting.ToFacts();
        var manual = new ManualDecision(meeting.ManualAssignment, meeting.ManualProjectId);
        var result = ClassificationEngine.Classify(facts, ctx.Projects, ctx.Clients, ctx.Settings, manual);

        var aiTierActive = ctx.Settings.Ai.Enabled && !ctx.Settings.DisabledTiers.Contains(ClassificationTier.Ai) && ai.IsConfigured(ctx.Settings.Ai);
        if (aiTierActive && result.Status == ClassificationStatus.Unassigned && meeting.ManualAssignment == ManualAssignmentKind.None)
        {
            try
            {
                var suggestion = await ai.SuggestAsync(BuildAiInput(meeting, facts, ctx), ctx.Settings.Ai, ct);
                if (suggestion is not null)
                    result = ClassificationEngine.Classify(facts, ctx.Projects, ctx.Clients, ctx.Settings, manual, suggestion);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // AI is optional: a failure never blocks deterministic classification.
                logger.LogWarning("AI classification failed for {NoteId}: {Message}", meeting.GranolaNoteId, ex.Message);
            }
        }
        return result;
    }

    private static AiClassificationInput BuildAiInput(Meeting m, MeetingFacts facts, ClassificationContext ctx) =>
        new(
            facts.Title,
            facts.Attendees.Concat(facts.InviteeEmails.Select(e => new ParticipantFact(null, e))).ToList(),
            facts.IdentifiedSpeakers.Keys.ToList(),
            facts.FolderNames,
            ctx.Settings.Ai.IncludeSummary ? Truncate(m.Summary, 4000) : null,
            ctx.Projects.Where(p => p.Active)
                .Select(p => new AiProjectOption(p.Id.ToString(), p.Name, p.Description, ctx.ProjectInfos.GetValueOrDefault(p.Id)?.ClientName))
                .ToList());

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];

    /// <summary>Copies a classification result onto the meeting, including multi-project assignments.</summary>
    public static void Apply(Meeting m, ClassificationResult r)
    {
        m.ClassificationStatus = r.Status;
        m.AssignmentMethod = r.Method;
        m.Certainty = r.Certainty;
        m.AssignedProjectId = r.PrimaryProjectId;
        m.NeedsReview = r.NeedsReview;
        m.ClassificationReason = r.Reason;
        m.ClassificationDetailJson = MeetingJson.Serialize(r);

        var wanted = r.ProjectIds.ToList();
        m.ProjectAssignments.RemoveAll(a => !wanted.Contains(a.ProjectId));
        for (var i = 0; i < wanted.Count; i++)
        {
            var existing = m.ProjectAssignments.FirstOrDefault(a => a.ProjectId == wanted[i]);
            if (existing is null)
            {
                m.ProjectAssignments.Add(new MeetingProjectAssignment { MeetingId = m.Id, ProjectId = wanted[i], IsPrimary = i == 0, Method = r.Method });
            }
            else
            {
                existing.IsPrimary = i == 0;
                existing.Method = r.Method;
            }
        }
    }
}
