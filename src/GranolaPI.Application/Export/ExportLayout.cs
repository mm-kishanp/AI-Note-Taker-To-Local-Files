using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Export;

public sealed record ProjectInfo(
    Guid Id,
    string Name,
    string Slug,
    string? ClientName,
    string? NotesFolderPath,
    string? TranscriptFolderPath,
    string? Description = null,
    ProjectStatus Status = ProjectStatus.Active)
{
    public static ProjectInfo From(Project p) =>
        new(p.Id, p.Name, p.Slug, p.Client?.Name, p.NotesFolderPath, p.TranscriptFolderPath, p.Description, p.Status);
}

/// <summary>Resolves configured roots and directory templates into concrete, root-guarded locations.</summary>
public sealed class ExportLayout
{
    private readonly ExportSettings _s;

    public ExportLayout(ExportSettings settings, string baseDirectory)
    {
        _s = settings;
        Root = SafePath.NormalizeRoot(settings.RootDirectory, baseDirectory);
        NotesRoot = string.IsNullOrWhiteSpace(settings.NotesRootDirectory) ? Root : SafePath.NormalizeRoot(settings.NotesRootDirectory, baseDirectory);
        TranscriptsRoot = string.IsNullOrWhiteSpace(settings.TranscriptsRootDirectory) ? Root : SafePath.NormalizeRoot(settings.TranscriptsRootDirectory, baseDirectory);
    }

    public string Root { get; }
    public string NotesRoot { get; }
    public string TranscriptsRoot { get; }
    public ExportSettings Settings => _s;

    public bool SharedRoot => string.Equals(NotesRoot, TranscriptsRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public string RootFor(ExportKind kind) => kind == ExportKind.Notes ? NotesRoot : TranscriptsRoot;

    public string AbsolutePath(ExportKind kind, string relativePath) => SafePath.Combine(RootFor(kind), relativePath);

    /// <summary>Project directory relative to the notes root, e.g. "Projects/MotorDealerApp".</summary>
    public string ProjectDirectory(ProjectInfo p, DateTimeOffset date, string meetingId = "", string title = "") =>
        string.Join('/', PathTemplates.ExpandDirectory(_s.ProjectDirectoryTemplate, Ctx(p, date, meetingId, title, null)));

    public string RelativeDirectory(ExportKind kind, ExportLocation location, ProjectInfo? project, DateTimeOffset date, string meetingId, string title)
    {
        List<string> segments;
        switch (location)
        {
            case ExportLocation.Project when project is not null:
                var overridePath = kind == ExportKind.Notes ? project.NotesFolderPath : project.TranscriptFolderPath;
                if (!string.IsNullOrWhiteSpace(overridePath))
                {
                    segments = PathTemplates.ExpandDirectory(overridePath, Ctx(project, date, meetingId, title, null));
                    break;
                }
                var projectDir = ProjectDirectory(project, date, meetingId, title);
                var template = kind == ExportKind.Notes ? _s.NotesDirectoryTemplate : _s.TranscriptsDirectoryTemplate;
                segments = PathTemplates.ExpandDirectory(template, Ctx(project, date, meetingId, title, projectDir));
                break;
            case ExportLocation.Shared:
                segments = PathTemplates.ExpandDirectory(kind == ExportKind.Notes ? _s.SharedNotesDirectory : _s.SharedTranscriptsDirectory, Ctx(null, date, meetingId, title, null));
                break;
            default:
                segments = PathTemplates.ExpandDirectory(kind == ExportKind.Notes ? _s.UnassignedNotesDirectory : _s.UnassignedTranscriptsDirectory, Ctx(null, date, meetingId, title, null));
                break;
        }
        var rel = string.Join('/', segments);
        SafePath.Combine(RootFor(kind), rel.Length == 0 ? "." : rel); // throws if it escapes the root
        return rel;
    }

    public string FileStem(ProjectInfo? project, DateTimeOffset date, string meetingId, string title) =>
        PathTemplates.ExpandFileName(_s.FilenameTemplate, Ctx(project, date, meetingId, title, null));

    /// <summary>Folder that holds ReviewQueue.md (parent of the unassigned notes directory).</summary>
    public string ReviewQueuePath()
    {
        var segments = PathTemplates.ExpandDirectory(_s.UnassignedNotesDirectory, Ctx(null, DateTimeOffset.UtcNow, "", "", null));
        var parent = segments.Count > 1 ? string.Join('/', segments.Take(segments.Count - 1)) : string.Join('/', segments);
        return SafePath.Combine(NotesRoot, Path.Combine(parent, "ReviewQueue.md"));
    }

    private static TemplateContext Ctx(ProjectInfo? p, DateTimeOffset date, string meetingId, string title, string? projectDir) =>
        new(p?.Name, p?.Slug, p?.ClientName, date, meetingId, title, projectDir);

    public static IReadOnlyList<string> Validate(ExportSettings s)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.RootDirectory)) errors.Add("Root directory is required.");
        errors.AddRange(PathTemplates.Validate(s.ProjectDirectoryTemplate, "Project directory template"));
        errors.AddRange(PathTemplates.Validate(s.NotesDirectoryTemplate, "Notes directory template"));
        errors.AddRange(PathTemplates.Validate(s.TranscriptsDirectoryTemplate, "Transcripts directory template"));
        errors.AddRange(PathTemplates.Validate(s.UnassignedNotesDirectory, "Unassigned notes directory", allowProjectVars: false));
        errors.AddRange(PathTemplates.Validate(s.UnassignedTranscriptsDirectory, "Unassigned transcripts directory", allowProjectVars: false));
        errors.AddRange(PathTemplates.Validate(s.SharedNotesDirectory, "Shared notes directory", allowProjectVars: false));
        errors.AddRange(PathTemplates.Validate(s.SharedTranscriptsDirectory, "Shared transcripts directory", allowProjectVars: false));
        errors.AddRange(PathTemplates.Validate(s.FilenameTemplate, "Filename template", isFileName: true));
        if (s.ProjectDirectoryTemplate.Contains("{ProjectDirectory}")) errors.Add("Project directory template cannot reference {ProjectDirectory}.");
        if (!s.ProjectDirectoryTemplate.Contains("{ProjectSlug}") && !s.ProjectDirectoryTemplate.Contains("{ProjectName}"))
            errors.Add("Project directory template must contain {ProjectSlug} or {ProjectName} so projects get separate folders.");
        if (!s.ExportNotes && !s.ExportTranscripts) errors.Add("At least one of notes or transcripts must be exported.");
        return errors;
    }
}
