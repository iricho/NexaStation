namespace NexaStation.Display.Monitors;

public sealed class DisplaySelectionService(IDisplayPlatformAdapter platform, IDisplayPreferenceStore preferences) : IDisplaySelectionService
{
    public Task<IReadOnlyList<DisplayDescriptor>> GetDisplaysAsync(CancellationToken cancellationToken = default) => platform.EnumerateAsync(cancellationToken);

    public async Task<DisplaySelectionResult> SelectAsync(string displayId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayId)) throw new ArgumentException("A stable display identifier is required.", nameof(displayId));
        IReadOnlyList<DisplayDescriptor> displays = await platform.EnumerateAsync(cancellationToken).ConfigureAwait(false);
        DisplayDescriptor? requested = displays.FirstOrDefault(display => display.IsAvailable && string.Equals(display.Id, displayId, StringComparison.Ordinal));
        bool fallback = requested is null;
        DisplayDescriptor? target = requested ?? displays.FirstOrDefault(display => display.IsAvailable && display.IsPrimary) ?? displays.FirstOrDefault(display => display.IsAvailable);
        if (target is null) return new(false, null, fallback, "No available display was reported by Windows.");
        if (!await platform.MoveCurrentWindowAsync(target, cancellationToken).ConfigureAwait(false)) return new(false, target, fallback, "NexaStation could not move to the requested display; the current display remains active.");
        if (!fallback)
        {
            DisplayPreferences current = await preferences.GetAsync(cancellationToken).ConfigureAwait(false);
            await preferences.SaveAsync(current with { PreferredMode = PreferredDisplayMode.SpecificDisplay, PreferredDisplayId = target.Id }, cancellationToken).ConfigureAwait(false);
        }
        return new(true, target, fallback, fallback ? "The preferred display is unavailable; NexaStation used the Windows primary display without discarding the preference." : null);
    }

    public async Task<DisplayDescriptor?> GetPreferredAsync(CancellationToken cancellationToken = default)
    {
        DisplayPreferences choice = await preferences.GetAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DisplayDescriptor> displays = await platform.EnumerateAsync(cancellationToken).ConfigureAwait(false);
        return choice.PreferredMode switch
        {
            PreferredDisplayMode.SpecificDisplay => displays.FirstOrDefault(display => display.IsAvailable && display.Id == choice.PreferredDisplayId),
            PreferredDisplayMode.WindowsPrimary => displays.FirstOrDefault(display => display.IsAvailable && display.IsPrimary),
            _ => displays.FirstOrDefault(display => display.IsAvailable && display.IsCurrent),
        };
    }
}

public sealed class InMemoryDisplayPreferenceStore : IDisplayPreferenceStore
{
    private DisplayPreferences _preferences = new();
    public Task<DisplayPreferences> GetAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_preferences); }
    public Task SaveAsync(DisplayPreferences preferences, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences)); return Task.CompletedTask; }
}

public sealed class EmptyDisplayPlatformAdapter : IDisplayPlatformAdapter
{
    public Task<IReadOnlyList<DisplayDescriptor>> EnumerateAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<DisplayDescriptor>>([]); }
    public Task<bool> MoveCurrentWindowAsync(DisplayDescriptor display, CancellationToken cancellationToken = default) { ArgumentNullException.ThrowIfNull(display); cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(false); }
}
