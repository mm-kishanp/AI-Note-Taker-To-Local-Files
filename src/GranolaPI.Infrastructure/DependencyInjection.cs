using System.Diagnostics;
using GranolaPI.Application.Classification;
using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Synchronization;
using GranolaPI.Infrastructure.Ai;
using GranolaPI.Infrastructure.Granola;
using GranolaPI.Infrastructure.Logging;
using GranolaPI.Infrastructure.Persistence;
using GranolaPI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GranolaPI.Infrastructure;

public static class DependencyInjection
{
    /// <param name="connectionString">SQLite connection string. Other providers can be plugged in here.</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<ISyncLease, SqlSyncLease>();

        services.TryAddSingleton<ICredentialStore>(_ => OperatingSystem.IsWindows() && configuration.GetValue("Security:UseWindowsCredentialManager", true)
            ? new WindowsCredentialStore()
            : new ReadOnlyCredentialStore());
        services.AddSingleton<ISecretProvider, SecretProvider>();

        services.AddSingleton<GranolaRequestThrottle>();
        services.AddHttpClient<IGranolaApiClient, GranolaApiClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(100);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GranolaProjectIntelligenceSync/1.0");
        });

        services.Replace(ServiceDescriptor.Singleton<IAiClassifier, AnthropicAiClassifier>());
        services.TryAddSingleton<IShellLauncher, ShellLauncher>();
        services.AddSingleton<LogSettingsState>();
        return services;
    }
}

/// <summary>Opens a path with the OS file handler (Explorer / default Markdown editor). Local desktop only.</summary>
public sealed class ShellLauncher : IShellLauncher
{
    public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public void Open(string path)
    {
        // The path is always passed as a single argument (never through a shell), so it cannot inject commands.
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("explorer.exe") { UseShellExecute = false }
            : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
        psi.ArgumentList.Add(path);
        Process.Start(psi)?.Dispose();
    }
}
