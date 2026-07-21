using System.Security.Cryptography;
using System.Text;

namespace NexaStation.Profiles.Pin;

public sealed record PinCredential(byte[] Salt, byte[] Hash, int Iterations, DateTimeOffset UpdatedAtUtc);
public enum PinVerificationStatus { Succeeded, Incorrect, LockedOut, NotConfigured, InvalidFormat }
public sealed record PinVerificationResult(PinVerificationStatus Status, TimeSpan? RetryAfter = null);

public interface IPinCredentialStore
{
    Task<PinCredential?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PinCredential credential, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IOwnerPinService
{
    Task SetAsync(ReadOnlyMemory<char> pin, CancellationToken cancellationToken = default);
    Task<PinVerificationResult> VerifyAsync(ReadOnlyMemory<char> pin, CancellationToken cancellationToken = default);
    Task ResetThroughLocalRecoveryAsync(bool deliberateRecoveryConfirmed, CancellationToken cancellationToken = default);
}

public sealed class OwnerPinService : IOwnerPinService
{
    private readonly IPinCredentialStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly int _iterations;
    private readonly object _gate = new();
    private int _failedAttempts;
    private DateTimeOffset? _lockedUntil;

    public OwnerPinService(IPinCredentialStore store, TimeProvider? timeProvider = null, int iterations = 210_000)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _iterations = Math.Clamp(iterations, 10_000, 1_000_000);
    }

    public async Task SetAsync(ReadOnlyMemory<char> pin, CancellationToken cancellationToken = default)
    {
        ValidateFormat(pin.Span);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Hash(pin.Span, salt, _iterations);
        await _store.SaveAsync(new(salt, hash, _iterations, _timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        lock (_gate) { _failedAttempts = 0; _lockedUntil = null; }
    }

    public async Task<PinVerificationResult> VerifyAsync(ReadOnlyMemory<char> pin, CancellationToken cancellationToken = default)
    {
        if (!ValidFormat(pin.Span)) return new(PinVerificationStatus.InvalidFormat);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_gate) if (_lockedUntil is { } until && until > now) return new(PinVerificationStatus.LockedOut, until - now);
        PinCredential? credential = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
        if (credential is null) return new(PinVerificationStatus.NotConfigured);
        byte[] candidate = Hash(pin.Span, credential.Salt, credential.Iterations);
        bool valid = CryptographicOperations.FixedTimeEquals(candidate, credential.Hash);
        CryptographicOperations.ZeroMemory(candidate);
        lock (_gate)
        {
            if (valid) { _failedAttempts = 0; _lockedUntil = null; return new(PinVerificationStatus.Succeeded); }
            _failedAttempts++;
            if (_failedAttempts >= 5)
            {
                int penalty = Math.Min(300, 30 * (_failedAttempts - 4));
                _lockedUntil = now.AddSeconds(penalty);
                return new(PinVerificationStatus.LockedOut, TimeSpan.FromSeconds(penalty));
            }
        }
        return new(PinVerificationStatus.Incorrect);
    }

    public async Task ResetThroughLocalRecoveryAsync(bool deliberateRecoveryConfirmed, CancellationToken cancellationToken = default)
    {
        if (!deliberateRecoveryConfirmed) throw new InvalidOperationException("PIN reset requires a deliberate local recovery confirmation.");
        await _store.ClearAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate) { _failedAttempts = 0; _lockedUntil = null; }
    }

    private static bool ValidFormat(ReadOnlySpan<char> pin) => pin.Length is >= 4 and <= 8 && pin.IndexOfAnyExceptInRange('0', '9') < 0;
    private static void ValidateFormat(ReadOnlySpan<char> pin) { if (!ValidFormat(pin)) throw new ArgumentException("PINs must contain four to eight digits.", nameof(pin)); }
    private static byte[] Hash(ReadOnlySpan<char> pin, byte[] salt, int iterations)
    {
        char[] characters = pin.ToArray();
        byte[] bytes = Encoding.UTF8.GetBytes(characters);
        try { return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, iterations, HashAlgorithmName.SHA256, 32); }
        finally { Array.Clear(characters); CryptographicOperations.ZeroMemory(bytes); }
    }
}

public sealed class InMemoryPinCredentialStore : IPinCredentialStore
{
    private PinCredential? _credential;
    public Task<PinCredential?> GetAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_credential is null ? null : _credential with { Salt = _credential.Salt.ToArray(), Hash = _credential.Hash.ToArray() }); }
    public Task SaveAsync(PinCredential credential, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _credential = credential with { Salt = credential.Salt.ToArray(), Hash = credential.Hash.ToArray() }; return Task.CompletedTask; }
    public Task ClearAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); if (_credential is not null) { CryptographicOperations.ZeroMemory(_credential.Salt); CryptographicOperations.ZeroMemory(_credential.Hash); } _credential = null; return Task.CompletedTask; }
}
