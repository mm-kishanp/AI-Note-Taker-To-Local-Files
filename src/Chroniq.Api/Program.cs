using System.Text.Json.Serialization;
using Chroniq.Api.Controllers;
using Chroniq.Api.Middleware;
using Chroniq.Application;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Settings;
using Chroniq.Infrastructure;
using Chroniq.Infrastructure.Logging;
using Chroniq.Infrastructure.Persistence;
using Chroniq.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Base directory for relative paths (database, logs, export root). Defaults to the content root.
var baseDir = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["App:BaseDirectory"] ?? "."));
var dataDir = Path.GetFullPath(Path.Combine(baseDir, builder.Configuration["App:DataDirectory"] ?? "data"));
Directory.CreateDirectory(dataDir);
// Installs from before the rename to CHRONIQ keep using their existing granola-pi.db.
var dbPath = Path.Combine(dataDir, "chroniq.db");
var legacyDbPath = Path.Combine(dataDir, "granola-pi.db");
if (!File.Exists(dbPath) && File.Exists(legacyDbPath)) dbPath = legacyDbPath;
var connectionString = builder.Configuration.GetConnectionString("Default") ?? $"Data Source={dbPath}";

var logState = new LogSettingsState { Directory = Path.Combine(dataDir, "logs") };
builder.Services.AddSingleton(logState);
builder.Logging.AddProvider(new FileLoggerProvider(logState));

builder.Services.AddSingleton<IAppPaths>(new AppPaths(baseDir));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, connectionString);
if (builder.Configuration.GetValue("Worker:Enabled", true)) builder.Services.AddSynchronizationWorker();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
// Same enum-as-string contract for the OpenAPI document and minimal endpoints.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173"])
    .AllowAnyMethod()
    .WithHeaders("Content-Type", RequestProtectionMiddleware.CsrfHeader, RequestProtectionMiddleware.LegacyCsrfHeader, "X-Correlation-ID", "Authorization")
    .WithExposedHeaders("X-Correlation-ID")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    // Installs from before the rename to CHRONIQ that never saved export settings keep exporting to the
    // old default folder instead of silently switching to an empty ChroniqKnowledge folder.
    const string legacyRoot = "./GranolaProjectKnowledge";
    if (!await db.ApplicationSettings.AnyAsync(s => s.Key == SettingKeys.Export)
        && Directory.Exists(Path.Combine(baseDir, legacyRoot))
        && !Directory.Exists(Path.Combine(baseDir, new ExportSettings().RootDirectory)))
    {
        await settings.SaveSectionAsync(SettingKeys.Export, new ExportSettings { RootDirectory = legacyRoot });
    }
    var logging = await settings.GetSectionAsync<LoggingSettings>(SettingKeys.Logging);
    SettingsController.ApplyLogging(logState, logging, baseDir);
}

app.UseExceptionHandler();
app.UseMiddleware<RequestProtectionMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "CHRONIQ API"));
}

// Production: the built React app is served from wwwroot (see scripts/build.ps1).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
// Unknown API paths are a 404, never the SPA page (which the UI would misread as data).
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found",
    detail: "This API endpoint does not exist."));
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("CHRONIQ started. Base directory: {BaseDir}", baseDir);
await app.RunAsync();

public partial class Program;
