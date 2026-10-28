using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using GranolaPI.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GranolaPI.Infrastructure.Security;

/// <summary>Windows Credential Manager (generic credentials, per-user, encrypted by the OS).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore(string prefix = "GranolaProjectIntelligence") : ICredentialStore
{
    public string StoreName => "Windows Credential Manager";
    public bool CanWrite => true;

    private string Target(string name) => $"{prefix}:{name}";

    public string? Read(string name)
    {
        if (!CredRead(Target(name), CredTypeGeneric, 0, out var ptr))
        {
            var err = Marshal.GetLastWin32Error();
            if (err == ErrorNotFound) return null;
            throw new Win32Exception(err, "Could not read from Windows Credential Manager.");
        }
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero) return null;
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public void Write(string name, string secret)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = Target(name),
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName,
                Comment = "Granola Project Intelligence Sync",
            };
            if (!CredWrite(ref cred, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not write to Windows Credential Manager.");
        }
        finally
        {
            // Clear the unmanaged copy of the secret before freeing it.
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Array.Clear(bytes);
        }
    }

    public bool Delete(string name)
    {
        if (CredDelete(Target(name), CredTypeGeneric, 0)) return true;
        var err = Marshal.GetLastWin32Error();
        if (err == ErrorNotFound) return false;
        throw new Win32Exception(err, "Could not delete from Windows Credential Manager.");
    }

    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}

/// <summary>Read-only store for servers/containers: secrets come from environment variables or a secrets manager.</summary>
public sealed class ReadOnlyCredentialStore : ICredentialStore
{
    public string StoreName => "Environment / secrets manager";
    public bool CanWrite => false;
    public string? Read(string name) => null;
    public void Write(string name, string secret) => throw new NotSupportedException("Credentials are read-only on this platform.");
    public bool Delete(string name) => false;
}

/// <summary>In-memory store used by tests.</summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _values = new();
    public string StoreName => "In-memory (testing)";
    public bool CanWrite => true;
    public string? Read(string name) => _values.GetValueOrDefault(name);
    public void Write(string name, string secret) => _values[name] = secret;
    public bool Delete(string name) => _values.Remove(name);
}

/// <summary>
/// Resolves secrets: the OS credential store first, then configuration (environment variables such as
/// GRANOLA_API_KEY, user-secrets or a mounted secrets provider). Never plain-text JSON settings files.
/// </summary>
public sealed class SecretProvider(ICredentialStore store, IConfiguration configuration) : ISecretProvider
{
    private static readonly Dictionary<string, string[]> ConfigKeys = new()
    {
        [SecretNames.GranolaApiKey] = ["GRANOLA_API_KEY", "Secrets:GranolaApiKey"],
        [SecretNames.FirefliesApiKey] = ["FIREFLIES_API_KEY", "Secrets:FirefliesApiKey"],
        [SecretNames.AiApiKey] = ["ANTHROPIC_API_KEY", "Secrets:AiApiKey"],
    };

    public ResolvedSecret? Get(string name)
    {
        try
        {
            var stored = store.Read(name);
            if (!string.IsNullOrWhiteSpace(stored)) return new ResolvedSecret(stored, store.StoreName);
        }
        catch (Win32Exception)
        {
            // Fall back to configuration if the vault is unavailable.
        }
        foreach (var key in ConfigKeys.GetValueOrDefault(name) ?? [])
        {
            var v = configuration[key];
            if (!string.IsNullOrWhiteSpace(v)) return new ResolvedSecret(v.Trim(), key.Contains(':') ? "Configuration secrets" : $"Environment variable {key}");
        }
        return null;
    }
}
