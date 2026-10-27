using GranolaPI.Application.Classification;
using GranolaPI.Application.DTOs;
using GranolaPI.Application.Export;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using GranolaPI.Domain.Rules;
using GranolaPI.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GranolaPI.Application.Projects;

public sealed class ProjectService(IAppDbContext db, ISettingsService settings, IAppPaths paths, IClock clock)
{
    // ---------- Projects ----------

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(string? search, bool includeArchived, CancellationToken ct)
    {
        var q = db.Projects.AsNoTracking().Include(p => p.Client).Include(p => p.Rules).AsQueryable();
        if (!includeArchived) q = q.Where(p => p.Status == ProjectStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(s) || p.Slug.ToLower().Contains(s) || (p.Client != null && p.Client.Name.ToLower().Contains(s)));
        }
        var projects = await q.OrderBy(p => p.Name).ToListAsync(ct);
        var stats = await MeetingStats(ct);
        var layout = await LayoutAsync(ct);
        return projects.Select(p => ToDto(p, stats, layout)).ToList();
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Projects.AsNoTracking().Include(x => x.Client).Include(x => x.Rules).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Project not found.");
        return ToDto(p, await MeetingStats(ct), await LayoutAsync(ct));
    }

    public async Task<ProjectDto> CreateAsync(ProjectInput input, CancellationToken ct)
    {
        var project = new Project();
        await ApplyAsync(project, input, isNew: true, ct);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return await GetAsync(project.Id, ct);
    }

    /// <summary>
    /// Updates a project. The ID never changes, so renaming keeps history. Changing the slug or folder
    /// overrides only affects meetings exported from now on; existing files are not moved.
    /// </summary>
    public async Task<ProjectDto> UpdateAsync(Guid id, ProjectInput input, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Rules).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project not found.");
        await ApplyAsync(project, input, isNew: false, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProjectDto> SetStatusAsync(Guid id, ProjectStatus status, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Project not found.");
        project.Status = status;
        project.UpdatedAt = clock.UtcNow.UtcDateTime;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<RuleDto> AddRuleAsync(Guid projectId, RuleInput input, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Rules).FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Project not found.");
        var errors = ValidateRule(input, "rule");
        if (errors.Count > 0) throw new ValidationException(errors);
        var normalized = TextNormalizer.ForRule(input.Kind, input.Value);
        var existing = project.Rules.FirstOrDefault(r => r.Kind == input.Kind && r.NormalizedValue == normalized);
        if (existing is not null) return new RuleDto(existing.Id, existing.Kind, existing.Value, existing.Enabled);
        var rule = new ClassificationRule { ProjectId = project.Id, Kind = input.Kind, Value = input.Value.Trim(), NormalizedValue = normalized, Enabled = input.Enabled };
        db.ClassificationRules.Add(rule);
        project.UpdatedAt = clock.UtcNow.UtcDateTime;
        await db.SaveChangesAsync(ct);
        return new RuleDto(rule.Id, rule.Kind, rule.Value, rule.Enabled);
    }

    private async Task ApplyAsync(Project project, ProjectInput input, bool isNew, CancellationToken ct)
    {
        var errors = new List<ValidationErrorDto>();
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0) errors.Add(new("name", "Name is required."));
        if (name.Length > 200) errors.Add(new("name", "Name is too long."));
        var slug = string.IsNullOrWhiteSpace(input.Slug) ? (isNew ? Slug.FromName(name) : project.Slug) : input.Slug.Trim();
        if (!Slug.IsValid(slug)) errors.Add(new("slug", "Slug may only contain letters, digits, '-' and '_' (max 64)."));
        else if (await db.Projects.AnyAsync(p => p.Id != project.Id && p.Slug.ToLower() == slug.ToLower(), ct))
        {
            if (isNew && string.IsNullOrWhiteSpace(input.Slug)) slug = await UniqueSlug(slug, ct);
            else errors.Add(new("slug", $"Another project already uses the slug '{slug}'."));
        }
        if (input.ClientId is { } cid && !await db.Clients.AnyAsync(c => c.Id == cid, ct)) errors.Add(new("clientId", "Client not found."));
        foreach (var (path, field) in new[] { (input.NotesFolderPath, "notesFolderPath"), (input.TranscriptFolderPath, "transcriptFolderPath") })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            foreach (var e in PathTemplates.Validate(path, field)) errors.Add(new(field, e));
            if (path.Contains("{ProjectDirectory}")) errors.Add(new(field, "Folder overrides cannot use {ProjectDirectory}."));
        }
        for (var i = 0; i < (input.Rules?.Count ?? 0); i++) errors.AddRange(ValidateRule(input.Rules![i], $"rules[{i}]"));
        if (errors.Count > 0) throw new ValidationException(errors);

        project.Name = name;
        project.Slug = slug;
        project.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        project.ClientId = input.ClientId;
        project.Status = input.Status;
        project.NotesFolderPath = string.IsNullOrWhiteSpace(input.NotesFolderPath) ? null : input.NotesFolderPath.Trim();
        project.TranscriptFolderPath = string.IsNullOrWhiteSpace(input.TranscriptFolderPath) ? null : input.TranscriptFolderPath.Trim();
        project.DefaultClassificationPriority = input.DefaultClassificationPriority;
        project.UpdatedAt = clock.UtcNow.UtcDateTime;
        SyncRules(project.Rules, input.Rules ?? [], r => r.ProjectId = project.Id);
    }

    private void SyncRules(List<ClassificationRule> existing, IReadOnlyList<RuleInput> wanted, Action<ClassificationRule> attach)
    {
        var desired = wanted
            .Select(r => (r.Kind, Value: r.Value.Trim(), Norm: TextNormalizer.ForRule(r.Kind, r.Value), r.Enabled))
            .GroupBy(r => (r.Kind, r.Norm)).Select(g => g.First()).ToList();
        foreach (var rule in existing.ToList())
        {
            var match = desired.FirstOrDefault(d => d.Kind == rule.Kind && d.Norm == rule.NormalizedValue);
            if (match == default)
            {
                existing.Remove(rule);
                db.ClassificationRules.Remove(rule);
            }
            else
            {
                rule.Value = match.Value;
                rule.Enabled = match.Enabled;
            }
        }
        foreach (var d in desired.Where(d => !existing.Any(r => r.Kind == d.Kind && r.NormalizedValue == d.Norm)))
        {
            var rule = new ClassificationRule { Kind = d.Kind, Value = d.Value, NormalizedValue = d.Norm, Enabled = d.Enabled };
            attach(rule);
            existing.Add(rule);
            db.ClassificationRules.Add(rule);
        }
    }

    public static List<ValidationErrorDto> ValidateRule(RuleInput r, string field)
    {
        var errors = new List<ValidationErrorDto>();
        if (string.IsNullOrWhiteSpace(r.Value)) errors.Add(new(field, "Rule value is required."));
        else if (r.Value.Length > 320) errors.Add(new(field, "Rule value is too long."));
        else if (r.Kind == RuleKind.Email && !TextNormalizer.IsValidEmail(r.Value)) errors.Add(new(field, $"'{r.Value}' is not a valid email address."));
        else if (r.Kind == RuleKind.EmailDomain && !TextNormalizer.Domain(r.Value).Contains('.')) errors.Add(new(field, $"'{r.Value}' is not a valid domain (e.g. @acme.com)."));
        return errors;
    }

    private async Task<string> UniqueSlug(string slug, CancellationToken ct)
    {
        for (var i = 2; ; i++)
        {
            var candidate = $"{slug}{i}";
            if (!await db.Projects.AnyAsync(p => p.Slug.ToLower() == candidate.ToLower(), ct)) return candidate;
        }
    }

    private async Task<Dictionary<Guid, (int Count, DateTime? Last)>> MeetingStats(CancellationToken ct)
    {
        var rows = await db.MeetingProjectAssignments.AsNoTracking()
            .Join(db.Meetings, a => a.MeetingId, m => m.Id, (a, m) => new { a.ProjectId, Date = m.MeetingStartTime ?? m.CreatedAt })
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count(), Last = g.Max(x => x.Date) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => (r.Count, (DateTime?)r.Last));
    }

    private ProjectDto ToDto(Project p, Dictionary<Guid, (int Count, DateTime? Last)> stats, ExportLayout layout)
    {
        var info = ProjectInfo.From(p);
        string Dir(ExportKind k)
        {
            try { return layout.AbsolutePath(k, layout.RelativeDirectory(k, ExportLocation.Project, info, clock.UtcNow, "MeetingId", "Meeting Title")); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return $"(invalid: {ex.Message})"; }
        }
        var s = stats.GetValueOrDefault(p.Id);
        return new ProjectDto(
            p.Id, p.Name, p.Slug, p.Description, p.ClientId, p.Client?.Name, p.Status,
            p.NotesFolderPath, p.TranscriptFolderPath, p.DefaultClassificationPriority,
            p.Rules.OrderBy(r => r.Kind).ThenBy(r => r.Value).Select(r => new RuleDto(r.Id, r.Kind, r.Value, r.Enabled)).ToList(),
            s.Count,
            s.Last is { } last ? new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc)) : null,
            Dir(ExportKind.Notes), Dir(ExportKind.Transcript),
            new DateTimeOffset(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(p.UpdatedAt, DateTimeKind.Utc)));
    }

    // ---------- Mapping preview ----------

    /// <summary>
    /// Evaluates the given rules (or the saved ones) for a project against existing meetings without
    /// saving anything. Manual assignments are reported but never changed.
    /// </summary>
    public async Task<TestMappingResultDto> TestMappingAsync(Guid projectId, TestMappingRequest req, ClassificationContext ctx, CancellationToken ct)
    {
        var target = ctx.Projects.FirstOrDefault(p => p.Id == projectId) ?? throw new NotFoundException("Project not found.");
        if (req.Rules is not null)
        {
            var errors = req.Rules.SelectMany((r, i) => ValidateRule(r, $"rules[{i}]")).ToList();
            if (errors.Count > 0) throw new ValidationException(errors);
            var rules = req.Rules.Where(r => r.Enabled)
                .Select(r => new RuleSpec(Guid.NewGuid(), r.Kind, r.Value.Trim(), TextNormalizer.ForRule(r.Kind, r.Value))).ToList();
            target = target with { Rules = rules, Active = true };
        }
        var projects = ctx.Projects.Select(p => p.Id == projectId ? target : p).ToList();
        var names = projects.ToDictionary(p => p.Id, p => p.Name);
        var limit = Math.Clamp(req.Limit, 1, 5000);
        var meetings = await db.Meetings.AsNoTracking().Where(m => m.SourceStatus == SourceStatus.Active)
            .OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt).Take(limit).ToListAsync(ct);

        var items = new List<TestMappingItemDto>();
        foreach (var m in meetings)
        {
            var result = ClassificationEngine.Classify(m.ToFacts(), projects, ctx.Clients, ctx.Settings, new ManualDecision(m.ManualAssignment, m.ManualProjectId));
            var matchesTarget = result.ProjectIds.Contains(projectId) || result.CandidateProjectIds.Contains(projectId);
            var changes = result.Status != m.ClassificationStatus || result.PrimaryProjectId != m.AssignedProjectId;
            if (!matchesTarget && !(changes && m.AssignedProjectId == projectId)) continue;
            items.Add(new TestMappingItemDto(
                m.Id, m.Title, m.MeetingDate(),
                m.ClassificationStatus, m.AssignedProjectId is { } cur ? names.GetValueOrDefault(cur) : null,
                result.Status, result.PrimaryProjectId is { } np ? names.GetValueOrDefault(np) : null,
                changes, m.ManualAssignment != ManualAssignmentKind.None, result.Reason));
        }
        return new TestMappingResultDto(meetings.Count, items.Count(i => i.ProposedProject == target.Name || i.ProposedStatus == ClassificationStatus.NeedsReview), items.Count(i => i.Changes), items);
    }

    // ---------- Clients ----------

    public async Task<IReadOnlyList<ClientDto>> ListClientsAsync(CancellationToken ct)
    {
        var clients = await db.Clients.AsNoTracking().Include(c => c.Rules).Include(c => c.Projects).OrderBy(c => c.Name).ToListAsync(ct);
        return clients.Select(ToDto).ToList();
    }

    public async Task<ClientDto> SaveClientAsync(Guid? id, ClientInput input, CancellationToken ct)
    {
        var errors = new List<ValidationErrorDto>();
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0) errors.Add(new("name", "Name is required."));
        if (await db.Clients.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct)) errors.Add(new("name", "A client with this name already exists."));
        for (var i = 0; i < (input.Rules?.Count ?? 0); i++) errors.AddRange(ValidateRule(input.Rules![i], $"rules[{i}]"));
        if (errors.Count > 0) throw new ValidationException(errors);

        Client client;
        if (id is { } existingId)
        {
            client = await db.Clients.Include(c => c.Rules).FirstOrDefaultAsync(c => c.Id == existingId, ct) ?? throw new NotFoundException("Client not found.");
        }
        else
        {
            client = new Client();
            db.Clients.Add(client);
        }
        client.Name = name;
        client.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        client.UpdatedAt = clock.UtcNow.UtcDateTime;
        SyncRules(client.Rules, input.Rules ?? [], r => r.ClientId = client.Id);
        await db.SaveChangesAsync(ct);
        var saved = await db.Clients.AsNoTracking().Include(c => c.Rules).Include(c => c.Projects).FirstAsync(c => c.Id == client.Id, ct);
        return ToDto(saved);
    }

    public async Task DeleteClientAsync(Guid id, CancellationToken ct)
    {
        var client = await db.Clients.Include(c => c.Rules).Include(c => c.Projects).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client not found.");
        foreach (var p in client.Projects) p.ClientId = null;
        db.ClassificationRules.RemoveRange(client.Rules);
        db.Clients.Remove(client);
        await db.SaveChangesAsync(ct);
    }

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Name, c.Description,
        c.Rules.OrderBy(r => r.Kind).ThenBy(r => r.Value).Select(r => new RuleDto(r.Id, r.Kind, r.Value, r.Enabled)).ToList(),
        c.Projects.OrderBy(p => p.Name).Select(p => new ProjectRefDto(p.Id, p.Name, p.Slug)).ToList());

    // ---------- Import / export ----------

    public async Task<ProjectConfigExport> ExportConfigAsync(CancellationToken ct)
    {
        var clients = await db.Clients.AsNoTracking().Include(c => c.Rules).OrderBy(c => c.Name).ToListAsync(ct);
        var projects = await db.Projects.AsNoTracking().Include(p => p.Rules).Include(p => p.Client).OrderBy(p => p.Name).ToListAsync(ct);
        return new ProjectConfigExport(1,
            clients.Select(c => new ClientConfig(c.Name, c.Description, c.Rules.Select(r => new RuleInput(r.Kind, r.Value, r.Enabled)).ToList())).ToList(),
            projects.Select(p => new ProjectConfig(p.Id.ToString(), p.Name, p.Slug, p.Description, p.Client?.Name, p.Status,
                p.NotesFolderPath, p.TranscriptFolderPath, p.DefaultClassificationPriority,
                p.Rules.Select(r => new RuleInput(r.Kind, r.Value, r.Enabled)).ToList())).ToList());
    }

    /// <summary>Imports configuration. Projects are matched by ID, then slug; nothing is deleted.</summary>
    public async Task<ImportResultDto> ImportConfigAsync(ProjectConfigExport config, CancellationToken ct)
    {
        var warnings = new List<string>();
        int cCreated = 0, cUpdated = 0, pCreated = 0, pUpdated = 0;
        var clientIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in config.Clients ?? [])
        {
            var existing = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Name.ToLower() == c.Name.ToLower(), ct);
            var saved = await SaveClientAsync(existing?.Id, new ClientInput(c.Name, c.Description, c.Rules ?? []), ct);
            clientIds[c.Name] = saved.Id;
            if (existing is null) cCreated++; else cUpdated++;
        }
        foreach (var p in config.Projects ?? [])
        {
            Guid? clientId = null;
            if (!string.IsNullOrWhiteSpace(p.ClientName))
            {
                if (clientIds.TryGetValue(p.ClientName, out var cid)) clientId = cid;
                else
                {
                    var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Name.ToLower() == p.ClientName.ToLower(), ct);
                    if (c is null) warnings.Add($"Project '{p.Name}': client '{p.ClientName}' not found; imported without client.");
                    clientId = c?.Id;
                }
            }
            var input = new ProjectInput(p.Name, p.Slug, p.Description, clientId, p.Status, p.NotesFolder, p.TranscriptsFolder, p.Priority, p.Rules ?? []);
            Project? existing = null;
            if (Guid.TryParse(p.Id, out var id)) existing = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            existing ??= await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Slug.ToLower() == p.Slug.ToLower(), ct);
            try
            {
                if (existing is null)
                {
                    var project = new Project();
                    if (Guid.TryParse(p.Id, out var newId)) project.Id = newId;
                    await ApplyAsync(project, input, isNew: true, ct);
                    db.Projects.Add(project);
                    await db.SaveChangesAsync(ct);
                    pCreated++;
                }
                else
                {
                    await UpdateAsync(existing.Id, input, ct);
                    pUpdated++;
                }
            }
            catch (ValidationException ex)
            {
                warnings.Add($"Project '{p.Name}' skipped: {ex.Message}");
            }
        }
        return new ImportResultDto(cCreated, cUpdated, pCreated, pUpdated, warnings);
    }

    private async Task<ExportLayout> LayoutAsync(CancellationToken ct) =>
        new(await settings.GetSectionAsync<ExportSettings>(SettingKeys.Export, ct), paths.BaseDirectory);
}
