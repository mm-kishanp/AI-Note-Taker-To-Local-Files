using GranolaPI.Application.Classification;
using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Projects;
using GranolaPI.Application.Synchronization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GranolaPI.Application;

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
