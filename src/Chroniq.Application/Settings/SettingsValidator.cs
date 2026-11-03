using Chroniq.Application.DTOs;
using Chroniq.Application.Export;
using Chroniq.Domain.Enums;

namespace Chroniq.Application.Settings;

public static class SettingsValidator
{
    private static readonly string[] LogLevels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"];

    public static List<ValidationErrorDto> Validate(ExportSettings? e, SyncSettings? s, ClassificationSettings? c, LoggingSettings? l)
    {
        var errors = new List<ValidationErrorDto>();
        if (e is not null)
            errors.AddRange(ExportLayout.Validate(e).Select(m => new ValidationErrorDto("export", m)));
        if (s is not null)
        {
            if (s.IntervalMinutes is < 5 or > 1440) errors.Add(new("sync.intervalMinutes", "Interval must be between 5 and 1440 minutes."));
            if (s.MaxConcurrency is < 1 or > 8) errors.Add(new("sync.maxConcurrency", "Concurrency must be between 1 and 8 (Granola allows 5 requests/second)."));
            if (s.MaxRequestAttempts is < 1 or > 10) errors.Add(new("sync.maxRequestAttempts", "Request attempts must be between 1 and 10."));
            if (s.MaxMeetingRetries is < 0 or > 50) errors.Add(new("sync.maxMeetingRetries", "Meeting retries must be between 0 and 50."));
            if (s.IncrementalOverlapMinutes is < 0 or > 1440) errors.Add(new("sync.incrementalOverlapMinutes", "Overlap must be between 0 and 1440 minutes."));
            if (s.FirefliesLookbackDays is < 1 or > 60) errors.Add(new("sync.firefliesLookbackDays", "Fireflies look-back must be between 1 and 60 days."));
        }
        if (c is not null)
        {
            if (c.TierOrder.Count == 0) errors.Add(new("classification.tierOrder", "At least one classification tier is required."));
            if (c.TierOrder.Distinct().Count() != c.TierOrder.Count) errors.Add(new("classification.tierOrder", "Each tier may appear only once."));
            if (c.TierOrder.Any(t => !Enum.IsDefined(t))) errors.Add(new("classification.tierOrder", "Unknown classification tier."));
            if (c.MinimumSpeakerSegments is < 1 or > 1000) errors.Add(new("classification.minimumSpeakerSegments", "Must be between 1 and 1000."));
            if (c.Ai.Enabled && string.IsNullOrWhiteSpace(c.Ai.Model)) errors.Add(new("classification.ai.model", "Model is required when AI is enabled."));
            if (c.Ai.Enabled && c.Ai.Provider != "anthropic") errors.Add(new("classification.ai.provider", "Only the 'anthropic' provider is currently implemented."));
        }
        if (l is not null)
        {
            if (!LogLevels.Contains(l.MinimumLevel)) errors.Add(new("logging.minimumLevel", $"Log level must be one of {string.Join(", ", LogLevels)}."));
            if (string.IsNullOrWhiteSpace(l.LogDirectory)) errors.Add(new("logging.logDirectory", "Log directory is required."));
            if (l.RetentionDays is < 1 or > 365) errors.Add(new("logging.retentionDays", "Retention must be between 1 and 365 days."));
        }
        return errors;
    }

    /// <summary>Fills missing values from defaults so older stored settings keep working after upgrades.</summary>
    public static ClassificationSettings Normalize(ClassificationSettings c)
    {
        var order = c.TierOrder.Distinct().Where(Enum.IsDefined).ToList();
        foreach (var t in new ClassificationSettings().TierOrder.Where(t => !order.Contains(t))) order.Add(t);
        return c with { TierOrder = order, DisabledTiers = c.DisabledTiers.Distinct().ToList(), Ai = c.Ai ?? new AiSettings() };
    }
}
