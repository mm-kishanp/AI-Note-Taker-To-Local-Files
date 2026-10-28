using System.Text.Json.Serialization;
using GranolaPI.Api.Controllers;
using GranolaPI.Api.Middleware;
using GranolaPI.Application;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Settings;
using GranolaPI.Infrastructure;
using GranolaPI.Infrastructure.Logging;
using GranolaPI.Infrastructure.Persistence;
using GranolaPI.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Base directory for relative paths (database, logs, export root). Defaults to the content root.
var baseDir = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["App:BaseDirectory"] ?? "."));
var dataDir = Path.GetFullPath(Path.Combine(baseDir, builder.Configuration["App:DataDirectory"] ?? "data"));
Directory.CreateDirectory(dataDir);
var connectionString = builder.Configuration.GetConnectionString("Default") ?? $"Data Source={Path.Combine(dataDir, "granola-pi.db")}";

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
    .WithHeaders("Content-Type", RequestProtectionMiddleware.CsrfHeader, "X-Correlation-ID", "Authorization")
    .WithExposedHeaders("X-Correlation-ID")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var logging = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetSectionAsync<LoggingSettings>(SettingKeys.Logging);
    SettingsController.ApplyLogging(logState, logging, baseDir);
}

app.UseExceptionHandler();
app.UseMiddleware<RequestProtectionMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Granola Project Intelligence API"));
}

// Production: the built React app is served from wwwroot (see scripts/build.ps1).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
// Unknown API paths are a 404, never the SPA page (which the UI would misread as data).
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found",
    detail: "This API endpoint does not exist."));
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("Granola Project Intelligence Sync started. Base directory: {BaseDir}", baseDir);
await app.RunAsync();

public partial class Program;
