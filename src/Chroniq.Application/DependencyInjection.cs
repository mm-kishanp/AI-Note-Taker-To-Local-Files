using Chroniq.Application.Classification;
using Chroniq.Application.Export;
using Chroniq.Application.Granola;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.Projects;
using Chroniq.Application.Synchronization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chroniq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IAiClassifier, NullAiClassifier>();
        services.AddSingleton<SyncProgress>();
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<SyncCoordinator>();

        services.AddScoped<ClassificationService>();
        services.AddScoped<MeetingExporter>();
        services.AddScoped<ProjectContextService>();
        services.AddScoped<MeetingProcessor>();
        services.AddScoped<MeetingService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SyncHistoryService>();
        services.AddScoped<UnassignedService>();
        services.AddScoped<CredentialService>();
        services.AddScoped<ExportBrowserService>();
        return services;
    }
}
