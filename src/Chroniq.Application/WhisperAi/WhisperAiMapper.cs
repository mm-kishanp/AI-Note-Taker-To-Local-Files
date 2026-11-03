using Chroniq.Application.Granola;
using Chroniq.Domain.Enums;

namespace Chroniq.Application.WhisperAi;

/// <summary>
/// Converts a WhisperAI transcript into the normalized note shape the rest of the pipeline works with
/// (ingestion, classification, Markdown export). WhisperAI returns text only: there is no title, owner,
/// attendee list or AI summary, and speakers are anonymous diarization labels.
/// </summary>
public static class WhisperAiMapper
{
    public const string DefaultTitle = "WhisperAI transcript";

    /// <param name="created">
    /// When the transcript job was created. <c>GET /v1/transcript/{id}</c> does not return it, so the caller
    /// passes the value from the list endpoint (or the one stored on an earlier sync).
    /// </param>
    public static GranolaNoteFetch ToFetch(WhisperAiTranscript t, DateTimeOffset? created)
    {
        var note = new GranolaNote
        {
            Id = t.Id,
            Title = Title(t.AudioUrl),
            // WhisperAI has no "updated at"; a completed transcript does not change.
            CreatedAt = created ?? DateTimeOffset.UnixEpoch,
            UpdatedAt = created ?? DateTimeOffset.UnixEpoch,
            // audio_url is not exposed as a link: pre-signed URLs grant access to the recording.
            WebUrl = null,
        };

        if (!string.Equals(t.Status, WhisperAiStatus.Completed, StringComparison.OrdinalIgnoreCase))
        {
            var reason = t.ErrorCode ?? t.Error ?? $"status '{t.Status ?? "unknown"}'";
            return new GranolaNoteFetch(note, new GranolaTranscriptResult(false, [], ErrorCategory.TranscriptUnavailable, $"WhisperAI transcript is not completed ({reason})."));
        }

        var turns = t.Utterances is { Count: > 0 } u ? u : t.Sentences ?? [];
        var segments = turns
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .OrderBy(x => x.Start ?? double.MaxValue)
            .Select(x => new GranolaTranscriptSegment
            {
                Speaker = Speaker(x.Speaker),
                Text = x.Text!.Trim(),
                StartTime = created is { } st && x.Start is { } off ? st.AddMilliseconds(off) : null,
                EndTime = created is { } st2 && x.End is { } off2 ? st2.AddMilliseconds(off2) : null,
            })
            .ToList();
        // Without speaker turns or sentences the plain text is still the transcript.
        if (segments.Count == 0 && !string.IsNullOrWhiteSpace(t.Text))
            segments.Add(new GranolaTranscriptSegment { Text = t.Text.Trim() });

        return new GranolaNoteFetch(note, new GranolaTranscriptResult(true, segments));
    }

    /// <summary>
    /// WhisperAI labels speakers "A", "B"... Those are kept as anonymous diarization labels so they are
    /// never treated as a person's identity during classification.
    /// </summary>
    public static GranolaSpeaker? Speaker(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var trimmed = label.Trim();
        return new GranolaSpeaker
        {
            DiarizationLabel = trimmed.StartsWith("speaker", StringComparison.OrdinalIgnoreCase) ? trimmed : $"Speaker {trimmed}",
        };
    }

    /// <summary>The audio file's name (without extension or query string), or a generic title for uploaded files.</summary>
    public static string Title(string? audioUrl)
    {
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri)
            || uri.Host.EndsWith("whisperai.com", StringComparison.OrdinalIgnoreCase))
            return DefaultTitle;
        var name = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/')));
        return string.IsNullOrWhiteSpace(name) ? DefaultTitle : name.Trim();
    }
}
