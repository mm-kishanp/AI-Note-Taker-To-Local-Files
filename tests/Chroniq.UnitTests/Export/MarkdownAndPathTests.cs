using Chroniq.Application.Export;
using Chroniq.Application.Granola;
using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;

namespace Chroniq.UnitTests.Export;

public class MarkdownRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static MeetingDocument Doc(string title = "Client Requirement Discussion", IReadOnlyList<GranolaTranscriptSegment>? transcript = null) => new()
    {
        GranolaNoteId = "not_aaaaaaaaaaaaaa",
        Title = title,
        MeetingDate = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
        CreatedAt = Now,
        UpdatedAt = Now,
        OwnerName = "Kishan Parmar",
        Attendees = [new("Alice", "alice@a.com")],
        SummaryMarkdown = "# Overview\nWe agreed on scope.\n## Next Steps\n- Send estimate",
        NotesMarkdown = "my own note",
        Transcript = transcript ?? [Fixtures.Seg(null, "Hello", 0, "me"), Fixtures.Seg("Alice", "Hi", 12), Fixtures.Seg("Alice", "Second line", 15)],
        SourceUrl = "https://notes.granola.ai/d/x",
        ProjectName = "Motor Dealer App",
        ProjectSlug = "MotorDealerApp",
        ClientName = "Example Client",
        ClassificationStatus = ClassificationStatus.Assigned,
        Location = ExportLocation.Project,
    };

    [Fact]
    public void Notes_contain_front_matter_and_sections_without_fabrication()
    {
        var md = MarkdownRenderer.RenderNotes(Doc(), Now);
        Assert.StartsWith("---\nsource: granola\ncontent_type: notes\n", md);
        Assert.Contains("granola_note_id: \"not_aaaaaaaaaaaaaa\"", md);
        Assert.Contains("project: \"Motor Dealer App\"", md);
        Assert.Contains("## Meeting Notes", md);
        Assert.Contains("## Summary", md);
        Assert.Contains("### Next Steps", md); // Granola headings nested under our section
        Assert.DoesNotContain("## Action Items", md); // never invented
        Assert.DoesNotContain("## Open Questions", md);
        Assert.True(MarkdownRenderer.IsUnmodified(md));
    }

    [Fact]
    public void Transcript_groups_consecutive_segments_and_preserves_order_and_timestamps()
    {
        var md = MarkdownRenderer.RenderTranscript(Doc(), Now);
        Assert.Contains("**[00:00:00] Kishan Parmar (note owner):**\n\nHello", md);
        Assert.Contains("**[00:00:12] Alice:**\n\nHi Second line", md);
        Assert.True(md.IndexOf("Hello", StringComparison.Ordinal) < md.IndexOf("Second line", StringComparison.Ordinal));
        Assert.Contains("content_type: transcript", md);
    }

    [Fact]
    public void Transcript_without_timestamps_does_not_invent_them()
    {
        var seg = new GranolaTranscriptSegment { Speaker = new GranolaSpeaker { Source = "speaker", DiarizationLabel = "Speaker B" }, Text = "No time" };
        var md = MarkdownRenderer.RenderTranscript(Doc(transcript: [seg]), Now);
        Assert.Contains("**Speaker B:**", md);
        Assert.DoesNotContain("[00:", md);
    }

    [Theory]
    [InlineData("speaker", null, null, "Unknown speaker (system audio)")]
    [InlineData("microphone", null, null, "Unknown speaker (microphone)")]
    [InlineData("speaker", "them", null, "Other participant")]
    [InlineData("speaker", "them", "Speaker A", "Speaker A")]
    [InlineData("future-source", null, null, "Unknown speaker")]
    public void Speaker_labels_use_source_labels_or_neutral_fallbacks(string source, string? attribution, string? diarization, string expected) =>
        Assert.Equal(expected, MarkdownRenderer.SpeakerLabel(new GranolaSpeaker { Source = source, Attribution = attribution, DiarizationLabel = diarization }, "Owner"));

    [Fact]
    public void Unicode_gujarati_hindi_and_emoji_round_trip()
    {
        var segs = new[] { Fixtures.Seg("રવિ", "આ પ્રોજેક્ટ સરસ છે 🎉", 0), Fixtures.Seg("अनीता", "हाँ, ठीक है।", 3) };
        var md = MarkdownRenderer.RenderTranscript(Doc("ગુજરાતી મીટિંગ — हिंदी", segs), Now);
        Assert.Contains("આ પ્રોજેક્ટ સરસ છે 🎉", md);
        Assert.Contains("हाँ, ठीक है।", md);
        Assert.Contains("title: \"ગુજરાતી મીટિંગ — हिंदी\"", md);
        Assert.True(MarkdownRenderer.IsUnmodified(md));
    }

    [Fact]
    public void Html_and_markdown_block_markers_in_speech_are_neutralised_not_rewritten()
    {
        var segs = new[] { Fixtures.Seg("A", "<script>alert(1)</script>\n# not a heading\n3. not a list", 0) };
        var md = MarkdownRenderer.RenderTranscript(Doc(transcript: segs), Now);
        Assert.Contains("\\<script\\>alert(1)\\</script\\>", md);
        Assert.Contains("\\# not a heading", md);
        Assert.Contains("3\\. not a list", md);
        Assert.DoesNotContain("<script>", md);
    }

    [Fact]
    public void Front_matter_values_are_escaped()
    {
        var md = MarkdownRenderer.RenderNotes(Doc("Quote \" and newline\nsplit: yes"), Now);
        var fm = MarkdownRenderer.ReadFrontMatter(md)!;
        Assert.Equal("Quote \" and newline\nsplit: yes", fm["title"]);
    }

    [Fact]
    public void Hash_ignores_generated_at_and_line_endings_but_detects_edits()
    {
        var a = MarkdownRenderer.RenderNotes(Doc(), Now);
        var b = MarkdownRenderer.RenderNotes(Doc(), Now.AddHours(5));
        Assert.Equal(MarkdownRenderer.ComputeHash(a), MarkdownRenderer.ComputeHash(b));
        Assert.True(MarkdownRenderer.IsUnmodified(a.Replace("\n", "\r\n")));
        Assert.False(MarkdownRenderer.IsUnmodified(a + "\nmy edit"));
    }

    [Fact]
    public void Large_transcript_is_not_truncated()
    {
        var segs = Enumerable.Range(0, 20_000).Select(i => Fixtures.Seg(i % 2 == 0 ? "A" : "B", $"segment {i}", i)).ToList();
        var md = MarkdownRenderer.RenderTranscript(Doc(transcript: segs), Now);
        Assert.Contains("segment 0", md);
        Assert.Contains("segment 19999", md);
        Assert.Contains("transcript_segments: 20000", md);
    }

    [Fact]
    public void Missing_metadata_omits_sections()
    {
        var doc = Doc() with { SummaryMarkdown = null, NotesMarkdown = null, Attendees = [], SourceUrl = null, ClientName = null };
        var md = MarkdownRenderer.RenderNotes(doc, Now);
        Assert.Contains("_Granola returned no notes or summary for this meeting._", md);
        Assert.DoesNotContain("**Participants:**", md);
        Assert.DoesNotContain("source_url", md);
        Assert.DoesNotContain("client:", md);
    }
}

public class PathTemplateTests
{
    [Theory]
    [InlineData("Client: Requirement Discussion?", "Client-Requirement-Discussion")]
    [InlineData("  ", "Untitled")]
    [InlineData("a/b\\c", "a-b-c")]
    [InlineData("ગુજરાતી મીટિંગ", "ગુજરાતી-મીટિંગ")]
    [InlineData("v2.0 release", "v2-0-release")]
    public void Titles_become_safe_file_names(string title, string expected) => Assert.Equal(expected, FileNames.TitleForFileName(title));

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("nul.txt", "_nul.txt")]
    [InlineData("trailing. ", "trailing")]
    [InlineData("..", "_")]
    [InlineData("a<b>c", "a b c")]
    public void Segments_are_sanitised_for_windows(string input, string expected) => Assert.Equal(expected, FileNames.SanitizeSegment(input));

    [Fact]
    public void Default_filename_matches_spec()
    {
        var ctx = new TemplateContext("Motor", "MotorDealerApp", null, new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), "not_x", "Client Requirement Discussion");
        Assert.Equal("2026-10-26-Client-Requirement-Discussion", PathTemplates.ExpandFileName("{MeetingDate}-{MeetingTitle}", ctx));
    }

    [Fact]
    public void Variable_values_cannot_traverse_directories()
    {
        var ctx = new TemplateContext("../../etc", "..", "..\\..", DateTimeOffset.UtcNow, "id", "t");
        var segments = PathTemplates.ExpandDirectory("Projects/{ProjectSlug}/{ProjectName}/{ClientName}", ctx);
        Assert.DoesNotContain("..", segments);
        Assert.All(segments, s => Assert.DoesNotContain('/', s));
    }

    [Theory]
    [InlineData("../outside", false)]
    [InlineData("C:/abs", false)]
    [InlineData("/abs", false)]
    [InlineData("Projects/{Unknown}", false)]
    [InlineData("Projects//x", false)]
    [InlineData("Projects/{ProjectSlug}", true)]
    [InlineData("{Year}/{Month}", true)]
    public void Templates_are_validated(string template, bool valid) =>
        Assert.Equal(valid, PathTemplates.Validate(template, "t").Count == 0);

    [Fact]
    public void Layout_rejects_settings_without_project_variable()
    {
        var errors = ExportLayout.Validate(new ExportSettings { ProjectDirectoryTemplate = "Projects" });
        Assert.Contains(errors, e => e.Contains("{ProjectSlug}"));
    }

    [Fact]
    public void Safe_path_refuses_escape()
    {
        var root = Path.Combine(Path.GetTempPath(), "gpi-root");
        Assert.Throws<InvalidOperationException>(() => SafePath.Combine(root, "../x.md"));
        Assert.Throws<InvalidOperationException>(() => SafePath.Combine(root, Path.GetTempPath()));
        Assert.EndsWith("a" + Path.DirectorySeparatorChar + "b.md", SafePath.Combine(root, "a/b.md"));
    }

    [Fact]
    public void Separate_and_relative_roots_resolve_independently()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "gpi-base");
        var layout = new ExportLayout(new ExportSettings
        {
            RootDirectory = "./Knowledge",
            NotesRootDirectory = Path.Combine(Path.GetTempPath(), "MeetingNotes"),
            TranscriptsRootDirectory = "../MeetingTranscripts",
            NotesDirectoryTemplate = "{ProjectDirectory}",
            TranscriptsDirectoryTemplate = "{ProjectDirectory}",
        }, baseDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "Knowledge")), layout.Root);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MeetingNotes")), layout.NotesRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "..", "MeetingTranscripts")), layout.TranscriptsRoot);
        var p = new ProjectInfo(Guid.NewGuid(), "Motor", "MotorDealerApp", null, null, null);
        Assert.Equal("Projects/MotorDealerApp", layout.RelativeDirectory(ExportKind.Notes, ExportLocation.Project, p, DateTimeOffset.UtcNow, "id", "t"));
        Assert.False(layout.SharedRoot);
    }
}
