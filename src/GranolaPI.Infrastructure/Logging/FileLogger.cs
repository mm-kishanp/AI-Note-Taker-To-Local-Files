using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GranolaPI.Infrastructure.Logging;

/// <summary>Runtime-adjustable level and directory, updated when logging settings are saved.</summary>
public sealed class LogSettingsState
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public string Directory { get; set; } = "./data/logs";
    public int RetentionDays { get; set; } = 14;
}

/// <summary>
/// Minimal structured file logger: one JSON line per entry, a file per day, old files pruned.
/// Messages are passed through <see cref="SecretRedactor"/> so API keys never reach disk.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly LogSettingsState _state;
    private readonly BlockingCollection<string> _queue = new(10_000);
    private readonly Thread _writer;
    private DateOnly _lastPrune;

    public FileLoggerProvider(LogSettingsState state)
    {
        _state = state;
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "gpi-file-logger" };
        _writer.Start();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal LogSettingsState State => _state;

    internal void Enqueue(string line) => _queue.TryAdd(line);

    private void WriteLoop()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                var dir = Path.GetFullPath(_state.Directory);
                Directory.CreateDirectory(dir);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                File.AppendAllText(Path.Combine(dir, $"granola-pi-{today:yyyyMMdd}.log"), line + Environment.NewLine, Encoding.UTF8);
                if (_lastPrune != today)
                {
                    _lastPrune = today;
                    foreach (var f in Directory.EnumerateFiles(dir, "granola-pi-*.log"))
                        if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-Math.Max(1, _state.RetentionDays))) File.Delete(f);
                }
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(2));
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider.State.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var entry = new Dictionary<string, object?>
            {
                ["ts"] = DateTime.UtcNow.ToString("O"),
                ["level"] = logLevel.ToString(),
                ["category"] = category,
                ["message"] = SecretRedactor.Redact(formatter(state, exception)),
            };
            if (System.Diagnostics.Activity.Current?.TraceId.ToString() is { } trace && trace != "00000000000000000000000000000000")
                entry["traceId"] = trace;
            if (state is IEnumerable<KeyValuePair<string, object?>> props)
                foreach (var (k, v) in props.Where(p => p.Key != "{OriginalFormat}"))
                    entry[k] = v is string s ? SecretRedactor.Redact(s) : v?.ToString();
            if (exception is not null) entry["exception"] = SecretRedactor.Redact(exception.ToString());
            provider.Enqueue(System.Text.Json.JsonSerializer.Serialize(entry));
        }
    }
}

public static partial class SecretRedactor
{
    [GeneratedRegex(@"(grn_[A-Za-z0-9_\-]{6,}|wai_[A-Za-z0-9_\-]{6,}|sk-ant-[A-Za-z0-9_\-]{6,}|Bearer\s+[A-Za-z0-9_\-\.]{10,})")]
    private static partial Regex Secrets();

    public static string Redact(string? text) => string.IsNullOrEmpty(text) ? text ?? "" : Secrets().Replace(text, "[REDACTED]");
}
