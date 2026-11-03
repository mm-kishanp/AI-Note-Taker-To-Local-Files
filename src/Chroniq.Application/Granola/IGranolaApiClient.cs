using Chroniq.Domain.Enums;

namespace Chroniq.Application.Granola;

public sealed record NoteListQuery(
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    string? Cursor = null,
    int PageSize = 30);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, bool HasMore, string? Cursor);

public sealed record GranolaConnectionResult(
    bool Success,
    ErrorCategory ErrorCategory,
    string Message,
    string? CredentialSource);

/// <summary>Result of transcript retrieval. Failure is never represented as an empty transcript.</summary>
public sealed record GranolaTranscriptResult(
    bool Success,
    IReadOnlyList<GranolaTranscriptSegment> Segments,
    ErrorCategory ErrorCategory = ErrorCategory.None,
    string? Error = null);

public interface IGranolaApiClient
{
    Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken cancellationToken);

    Task<PagedResult<GranolaNoteSummary>> ListNotesAsync(NoteListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the note, or null when it no longer exists / is inaccessible (404).
    /// With <paramref name="includeTranscript"/>, a transcript too large to inline (413) is
    /// fetched transparently through the paged transcript endpoint.
    /// </summary>
    Task<GranolaNoteFetch?> GetNoteAsync(string noteId, bool includeTranscript, CancellationToken cancellationToken);

    Task<GranolaTranscriptResult> GetTranscriptAsync(string noteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GranolaFolder>> ListFoldersAsync(CancellationToken cancellationToken);
}

/// <summary>A fetched note plus the outcome of transcript retrieval, which can fail independently.</summary>
public sealed record GranolaNoteFetch(GranolaNote Note, GranolaTranscriptResult? Transcript);

public class GranolaApiException : Exception
{
    public GranolaApiException(ErrorCategory category, int? statusCode, string message, string? apiCode = null, Exception? inner = null)
        : base(message, inner)
    {
        Category = category;
        StatusCode = statusCode;
        ApiCode = apiCode;
    }

    public ErrorCategory Category { get; }
    public int? StatusCode { get; }
    public string? ApiCode { get; }

    public bool IsRetryable => Category is ErrorCategory.RateLimited or ErrorCategory.TransientApi;
}
