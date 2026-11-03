using Chroniq.Application.DTOs;
using Chroniq.Application.Fireflies;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.Settings;
using Chroniq.Application.WhisperAi;
using Chroniq.Domain.Enums;

namespace Chroniq.Application.Granola;

/// <summary>Manages the Granola, Fireflies and WhisperAI API keys. The full key is never returned; only a masked form.</summary>
public sealed class CredentialService(ICredentialStore store, ISecretProvider secrets, IGranolaApiClient api, IFirefliesApiClient fireflies,
    IWhisperAiApiClient whisperAi, ISettingsService settings, IClock clock)
{
    public async Task<CredentialStatusDto> StatusAsync(CancellationToken ct) => await StatusAsync(MeetingSource.Granola, ct);

    public async Task<CredentialStatusDto> StatusAsync(MeetingSource source, CancellationToken ct)
    {
        var secret = secrets.Get(SecretNames.ForSource(source));
        var state = await settings.GetSectionAsync<GranolaConnectionState>(StateKey(source), ct);
        return new CredentialStatusDto(
            secret is not null, secret?.Source, secret is null ? null : Mask(secret.Value),
            store.CanWrite, store.StoreName,
            state.LastCheckSucceeded, state.LastCheckedAt, state.LastSuccessAt, state.LastError, state.LastErrorCategory);
    }

    public Task<CredentialStatusDto> SetAsync(SetCredentialRequest req, CancellationToken ct) => SetAsync(MeetingSource.Granola, req, ct);

    public async Task<CredentialStatusDto> SetAsync(MeetingSource source, SetCredentialRequest req, CancellationToken ct)
    {
        var name = SecretNames.ForSource(source);
        var key = req.ApiKey?.Trim() ?? "";
        if (key.Length < 10 || key.Length > 512 || key.Any(char.IsWhiteSpace))
            throw ValidationException.Single("apiKey", source switch
            {
                MeetingSource.Fireflies => "Enter the API key exactly as shown in Fireflies (Integrations → Fireflies API).",
                MeetingSource.WhisperAi => "Enter the API key exactly as shown in the WhisperAI developer portal (it starts with wai_).",
                _ => "Enter the API key exactly as shown in Granola (Settings → Connectors → API keys).",
            });
        if (!store.CanWrite)
            throw ValidationException.Single("apiKey", $"{store.StoreName} is read-only on this platform. Set the {EnvVar(source)} environment variable instead.");

        var previous = store.Read(name);
        store.Write(name, key);
        if (req.Validate)
        {
            var result = await TestAsync(source, ct);
            if (!result.Success && result.ErrorCategory is ErrorCategory.Authentication)
            {
                // Don't keep a key that the service rejects; restore the previous one.
                if (previous is null) store.Delete(name);
                else store.Write(name, previous);
                throw ValidationException.Single("apiKey", $"{source} rejected this key: {result.Message}");
            }
        }
        return await StatusAsync(source, ct);
    }

    public Task<CredentialStatusDto> DeleteAsync(CancellationToken ct) => DeleteAsync(MeetingSource.Granola, ct);

    public async Task<CredentialStatusDto> DeleteAsync(MeetingSource source, CancellationToken ct)
    {
        if (store.CanWrite) store.Delete(SecretNames.ForSource(source));
        await settings.SaveSectionAsync(StateKey(source), new GranolaConnectionState(), ct);
        return await StatusAsync(source, ct);
    }

    public Task<ConnectionTestDto> TestAsync(CancellationToken ct) => TestAsync(MeetingSource.Granola, ct);

    public async Task<ConnectionTestDto> TestAsync(MeetingSource source, CancellationToken ct)
    {
        var result = source switch
        {
            MeetingSource.Fireflies => await fireflies.TestConnectionAsync(ct),
            MeetingSource.WhisperAi => await whisperAi.TestConnectionAsync(ct),
            _ => await api.TestConnectionAsync(ct),
        };
        var key = StateKey(source);
        var state = await settings.GetSectionAsync<GranolaConnectionState>(key, ct);
        var now = clock.UtcNow;
        await settings.SaveSectionAsync(key, state with
        {
            LastCheckedAt = now,
            LastCheckSucceeded = result.Success,
            LastSuccessAt = result.Success ? now : state.LastSuccessAt,
            LastError = result.Success ? null : result.Message,
            LastErrorCategory = result.ErrorCategory,
        }, ct);
        return new ConnectionTestDto(result.Success, result.ErrorCategory, result.Message, result.CredentialSource);
    }

    public bool IsConfigured(MeetingSource source) => secrets.Get(SecretNames.ForSource(source)) is not null;

    private static string StateKey(MeetingSource source) => source switch
    {
        MeetingSource.Fireflies => SettingKeys.FirefliesConnection,
        MeetingSource.WhisperAi => SettingKeys.WhisperAiConnection,
        _ => SettingKeys.GranolaConnection,
    };

    private static string EnvVar(MeetingSource source) => source switch
    {
        MeetingSource.Fireflies => "FIREFLIES_API_KEY",
        MeetingSource.WhisperAi => "WHISPERAI_API_KEY",
        _ => "GRANOLA_API_KEY",
    };

    public static string Mask(string secret) =>
        secret.Length <= 8 ? new string('•', secret.Length) : $"{secret[..4]}{new string('•', 8)}{secret[^4..]}";
}
