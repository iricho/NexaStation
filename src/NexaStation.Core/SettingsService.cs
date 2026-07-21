using System.Text.Json;

namespace NexaStation.Core;

public sealed class SettingsService(ISettingsRepository repository) : ISettingsService
{
    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        string? stored = await repository.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (stored is null) return defaultValue;
        try { return JsonSerializer.Deserialize<T>(stored) ?? defaultValue; }
        catch (JsonException) { return defaultValue; }
        catch (NotSupportedException) { return defaultValue; }
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        return repository.SetAsync(key, JsonSerializer.Serialize(value), cancellationToken);
    }
}
