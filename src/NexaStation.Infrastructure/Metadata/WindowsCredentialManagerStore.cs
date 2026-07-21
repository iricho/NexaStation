using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;
using Microsoft.Data.Sqlite;
using NexaStation.Infrastructure.Persistence;

namespace NexaStation.Infrastructure.Metadata;

public sealed class WindowsCredentialManagerStore(ISqliteConnectionFactory? connections = null) : IProviderCredentialStore
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;

    public async Task SaveAsync(ProviderCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential.ProviderId);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] secret = Encoding.Unicode.GetBytes(credential.Secret);
        if (secret.Length > 5120) throw new ArgumentOutOfRangeException(nameof(credential), "The provider credential is too large for Windows Credential Manager.");
        nint blob = Marshal.AllocCoTaskMem(secret.Length);
        try
        {
            CREDENTIAL native = new()
            {
                Type = GenericCredential,
                TargetName = Target(credential.ProviderId),
                CredentialBlobSize = (uint)secret.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = credential.AccountName ?? string.Empty,
            };
            Marshal.Copy(secret, 0, native.CredentialBlob, secret.Length);
            if (!CredWrite(ref native, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not protect the provider credential.");
            if (connections is not null)
            {
                await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO ProviderCredentialReferences(ProviderId,CredentialStoreReference,DisplayAccountName,ValidationState,UpdatedAtUtc)
                    VALUES($provider,$reference,$account,$state,$now)
                    ON CONFLICT(ProviderId) DO UPDATE SET
                        CredentialStoreReference=excluded.CredentialStoreReference,
                        DisplayAccountName=excluded.DisplayAccountName,
                        ValidationState=excluded.ValidationState,
                        UpdatedAtUtc=excluded.UpdatedAtUtc;
                    """;
                command.Parameters.AddWithValue("$provider", credential.ProviderId);
                command.Parameters.AddWithValue("$reference", Target(credential.ProviderId));
                command.Parameters.AddWithValue("$account", (object?)credential.AccountName ?? DBNull.Value);
                command.Parameters.AddWithValue("$state", (int)ProviderValidationState.Unknown);
                command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blob);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public async Task<ProviderCredential?> GetAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        cancellationToken.ThrowIfCancellationRequested();
        if (connections is not null)
        {
            await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT CredentialStoreReference FROM ProviderCredentialReferences WHERE ProviderId=$provider;";
            command.Parameters.AddWithValue("$provider", providerId);
            string? reference = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (!string.Equals(reference, Target(providerId), StringComparison.Ordinal)) return null;
        }
        if (!CredRead(Target(providerId), GenericCredential, 0, out nint pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error, "Windows could not read the protected provider credential.");
        }
        try
        {
            CREDENTIAL native = Marshal.PtrToStructure<CREDENTIAL>(pointer);
            byte[] bytes = new byte[native.CredentialBlobSize];
            if (bytes.Length > 0) Marshal.Copy(native.CredentialBlob, bytes, 0, bytes.Length);
            try { return new(providerId, Encoding.Unicode.GetString(bytes), native.UserName); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally { CredFree(pointer); }
    }

    public async Task DeleteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!CredDelete(Target(providerId), GenericCredential, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not remove the protected provider credential.");
        if (connections is not null)
        {
            await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ProviderCredentialReferences WHERE ProviderId=$provider;";
            command.Parameters.AddWithValue("$provider", providerId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Target(string providerId) => $"NexaStation.Metadata.{new string(providerId.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray())}";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags; public uint Type; public string TargetName; public string? Comment; public long LastWritten;
        public uint CredentialBlobSize; public nint CredentialBlob; public uint Persist; public uint AttributeCount;
        public nint Attributes; public string? TargetAlias; public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredRead(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(nint buffer);
}
