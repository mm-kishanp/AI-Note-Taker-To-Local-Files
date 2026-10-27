using GranolaPI.Application.DTOs;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Granola;

/// <summary>Manages the Granola API key. The full key is never returned; only a masked form.</summary>
public sealed class CredentialService(ICredentialStore store, ISecretProvider secrets, IGranolaApiClient api, ISettingsService settings, IClock clock)
{
    public async Task<CredentialStatusDto> StatusAsync(CancellationToken ct)
    {
        var secret = secrets.Get(SecretNames.GranolaApiKey);
        var state = await settings.GetSectionAsync<GranolaConnectionState>(SettingKeys.GranolaConnection, ct);
        return new CredentialStatusDto(
            secret is not null, secret?.Source, secret is null ? null : Mask(secret.Value),
            store.CanWrite, store.StoreName,
            state.LastCheckSucceeded, state.LastCheckedAt, state.LastSuccessAt, state.LastError, state.LastErrorCategory);
    }

    public async Task<CredentialStatusDto> SetAsync(SetCredentialRequest req, CancellationToken ct)
    {
        var key = req.ApiKey?.Trim() ?? "";
        if (key.Length < 10 || key.Length > 512 || key.Any(char.IsWhiteSpace))
            throw ValidationException.Single("apiKey", "Enter the API key exactly as shown in Granola (Settings → Connectors → API keys).");
        if (!store.CanWrite)
            throw ValidationException.Single("apiKey", $"{store.StoreName} is read-only on this platform. Set the GRANOLA_API_KEY environment variable instead.");

        var previous = store.Read(SecretNames.GranolaApiKey);
        store.Write(SecretNames.GranolaApiKey, key);
        if (req.Validate)
        {
            var result = await TestAsync(ct);
            if (!result.Success && result.ErrorCategory is ErrorCategory.Authentication)
            {
                // Don't keep a key that Granola rejects; restore the previous one.
                if (previous is null) store.Delete(SecretNames.GranolaApiKey);
                else store.Write(SecretNames.GranolaApiKey, previous);
                throw ValidationException.Single("apiKey", $"Granola rejected this key: {result.Message}");
            }
        }
        return await StatusAsync(ct);
    }

    public async Task<CredentialStatusDto> DeleteAsync(CancellationToken ct)
    {
        if (store.CanWrite) store.Delete(SecretNames.GranolaApiKey);
        await settings.SaveSectionAsync(SettingKeys.GranolaConnection, new GranolaConnectionState(), ct);
        return await StatusAsync(ct);
    }

    public async Task<ConnectionTestDto> TestAsync(CancellationToken ct)
    {
        var result = await api.TestConnectionAsync(ct);
        var state = await settings.GetSectionAsync<GranolaConnectionState>(SettingKeys.GranolaConnection, ct);
        var now = clock.UtcNow;
        await settings.SaveSectionAsync(SettingKeys.GranolaConnection, state with
        {
            LastCheckedAt = now,
            LastCheckSucceeded = result.Success,
            LastSuccessAt = result.Success ? now : state.LastSuccessAt,
            LastError = result.Success ? null : result.Message,
            LastErrorCategory = result.ErrorCategory,
        }, ct);
        return new ConnectionTestDto(result.Success, result.ErrorCategory, result.Message, result.CredentialSource);
    }

    public static string Mask(string secret) =>
        secret.Length <= 8 ? new string('•', secret.Length) : $"{secret[..4]}{new string('•', 8)}{secret[^4..]}";
}
