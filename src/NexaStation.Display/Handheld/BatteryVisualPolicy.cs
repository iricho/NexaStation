namespace NexaStation.Display.Handheld;

public enum PowerSourceKind { Unknown, Ac, Battery }
public enum BatteryChargeState { Unknown, Charging, Discharging, Full }
public sealed record HandheldPowerStatus(int? BatteryPercentage, BatteryChargeState ChargeState, PowerSourceKind PowerSource, TimeSpan? EstimatedRemaining, DateTimeOffset UpdatedAtUtc);
public enum VisualQualityPreset { Efficient, Balanced, Quality }
public sealed record BatteryVisualSettings(bool ReduceEffectsOnBattery, VisualQualityPreset SelectedPreset, bool CompactHandheldLayout);
public sealed record EffectiveBatteryVisualState(VisualQualityPreset Preset, bool AnimationsEnabled, bool BlurEnabled, bool GalleryPreloadEnabled, int ArtworkDecodeLimit, int CacheLimit, TimeSpan PollInterval, bool PauseProviderRefresh);

public sealed class BatteryVisualPolicy
{
    public static EffectiveBatteryVisualState Resolve(BatteryVisualSettings settings, HandheldPowerStatus status)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(status);
        bool reduce = status.PowerSource == PowerSourceKind.Battery && (settings.ReduceEffectsOnBattery || settings.CompactHandheldLayout);
        bool low = status.BatteryPercentage is <= 20;
        return reduce
            ? new(VisualQualityPreset.Efficient, false, false, false, low ? 640 : 960, low ? 24 : 40, low ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(2), low)
            : new(settings.SelectedPreset, true, true, true, settings.SelectedPreset == VisualQualityPreset.Quality ? 1920 : 1280, settings.SelectedPreset == VisualQualityPreset.Quality ? 100 : 64, TimeSpan.FromMinutes(1), false);
    }
}
