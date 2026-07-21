using NexaStation.Domain;

namespace NexaStation.Core;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public interface ILibraryRepository
{
    Task<IReadOnlyList<LibraryItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default);
    Task<LibraryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(LibraryItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(LibraryItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string id, bool favorite, CancellationToken cancellationToken = default);
    Task SetHiddenAsync(string id, bool hidden, CancellationToken cancellationToken = default);
    Task<LibraryItem?> FindDuplicateAsync(LaunchTarget target, CancellationToken cancellationToken = default);
}

public interface IPlaySessionRepository
{
    Task StartSessionAsync(PlaySession session, CancellationToken cancellationToken = default);
    Task CompleteSessionAsync(string sessionId, DateTime endedAtUtc, long durationSeconds, bool exitDetectedNormally, CancellationToken cancellationToken = default);
    Task FailSessionAsync(string sessionId, string failureMessage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlaySession>> GetRecentAsync(int maximumResults, CancellationToken cancellationToken = default);
    Task RepairIncompleteSessionsAsync(DateTime applicationStartedAtUtc, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

public interface IApplicationStateRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

public interface IWindowsShortcutResolver
{
    Task<ShortcutResolutionResult> ResolveAsync(string shortcutPath, CancellationToken cancellationToken = default);
}
