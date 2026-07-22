using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Display.Calibration;
using NexaStation.Display.Monitors;
using NexaStation.Input.Mapping;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;
using NexaStation.TextInput.Layouts;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqlitePhase10Store(ISqliteConnectionFactory factory) :
    IInputProfileStore,
    IControllerPreferenceStore,
    ITextInputSettingsStore,
    IDisplayPreferenceStore,
    IDisplayCalibrationStore,
    IFrontendProfileStore,
    IGuestActivityStore,
    IPinCredentialStore,
    IContentRestrictionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    public async Task<IReadOnlyList<InputProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,Name,DeviceScope,DeviceMatchJson,GlyphSetId,Enabled,BuiltIn,CreatedAtUtc,UpdatedAtUtc FROM InputProfiles ORDER BY BuiltIn DESC,Name;";
        List<InputProfile> result = []; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(reader.GetString(0), reader.GetString(1), (InputDeviceScope)reader.GetInt32(2), NullableString(reader, 3), NullableString(reader, 4), reader.GetBoolean(5), reader.GetBoolean(6), At(reader.GetString(7)), At(reader.GetString(8))));
        return result;
    }

    public async Task<IReadOnlyList<InputBinding>> GetBindingsAsync(string profileId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,InputProfileId,InputAction,PhysicalInputJson,Context,SortOrder,CreatedAtUtc,UpdatedAtUtc FROM InputBindings WHERE InputProfileId=$profile ORDER BY SortOrder;"; command.Parameters.AddWithValue("$profile", profileId);
        List<InputBinding> result = []; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(reader.GetString(0), reader.GetString(1), (Core.InputAction)reader.GetInt32(2), JsonSerializer.Deserialize<PhysicalInput>(reader.GetString(3), JsonOptions) ?? throw new InvalidDataException("An input binding contains invalid physical input JSON."), (InputContext)reader.GetInt32(4), reader.GetInt32(5), At(reader.GetString(6)), At(reader.GetString(7))));
        return result.Count == 0 && string.Equals(profileId, DefaultInputBindings.BuiltInProfileId, StringComparison.Ordinal) ? DefaultInputBindings.Create() : result;
    }

    public async Task SaveProfileAsync(InputProfile profile, IReadOnlyList<InputBinding> bindings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(bindings);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = "INSERT INTO InputProfiles(Id,Name,DeviceScope,DeviceMatchJson,GlyphSetId,Enabled,BuiltIn,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$name,$scope,$match,$glyph,$enabled,$builtIn,$created,$updated) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,DeviceScope=excluded.DeviceScope,DeviceMatchJson=excluded.DeviceMatchJson,GlyphSetId=excluded.GlyphSetId,Enabled=excluded.Enabled,UpdatedAtUtc=excluded.UpdatedAtUtc;";
            command.Parameters.AddWithValue("$id", profile.Id); command.Parameters.AddWithValue("$name", profile.Name); command.Parameters.AddWithValue("$scope", (int)profile.DeviceScope); command.Parameters.AddWithValue("$match", Db(profile.DeviceMatchJson)); command.Parameters.AddWithValue("$glyph", Db(profile.GlyphSetId)); command.Parameters.AddWithValue("$enabled", profile.Enabled); command.Parameters.AddWithValue("$builtIn", profile.BuiltIn); command.Parameters.AddWithValue("$created", Iso(profile.CreatedAtUtc)); command.Parameters.AddWithValue("$updated", Iso(profile.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            command.Parameters.Clear(); command.CommandText = "DELETE FROM InputBindings WHERE InputProfileId=$profile;"; command.Parameters.AddWithValue("$profile", profile.Id); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (InputBinding binding in bindings)
        {
            await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT INTO InputBindings(Id,InputProfileId,InputAction,PhysicalInputJson,Context,SortOrder,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$profile,$action,$physical,$context,$sort,$created,$updated);";
            command.Parameters.AddWithValue("$id", binding.Id); command.Parameters.AddWithValue("$profile", profile.Id); command.Parameters.AddWithValue("$action", (int)binding.Action); command.Parameters.AddWithValue("$physical", JsonSerializer.Serialize(binding.PhysicalInput, JsonOptions)); command.Parameters.AddWithValue("$context", (int)binding.Context); command.Parameters.AddWithValue("$sort", binding.SortOrder); command.Parameters.AddWithValue("$created", Iso(binding.CreatedAtUtc)); command.Parameters.AddWithValue("$updated", Iso(binding.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetAsync(string? profileId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = profileId is null ? "DELETE FROM InputProfiles WHERE BuiltIn=0; DELETE FROM InputBindings WHERE InputProfileId='builtin.default';" : "DELETE FROM InputProfiles WHERE Id=$id AND BuiltIn=0; DELETE FROM InputBindings WHERE InputProfileId=$id;";
        if (profileId is not null) command.Parameters.AddWithValue("$id", profileId); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task<ControllerPreference?> IControllerPreferenceStore.GetAsync(string deviceMatchKey, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT DeviceMatchKey,PreferredGlyphSetId,ConfirmConvention,Deadzone,RepeatDelayMilliseconds,RepeatRateMilliseconds,AnalogNavigationEnabled,DpadNavigationEnabled,WrapNavigationEnabled,UpdatedAtUtc FROM ControllerPreferences WHERE DeviceMatchKey=$key;"; command.Parameters.AddWithValue("$key", deviceMatchKey); await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(reader.GetString(0), NullableString(reader, 1), reader.GetInt32(2), reader.GetDouble(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8), At(reader.GetString(9)));
    }

    public async Task SaveAsync(ControllerPreference preference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO ControllerPreferences(DeviceMatchKey,PreferredGlyphSetId,ConfirmConvention,Deadzone,RepeatDelayMilliseconds,RepeatRateMilliseconds,AnalogNavigationEnabled,DpadNavigationEnabled,WrapNavigationEnabled,UpdatedAtUtc) VALUES($key,$glyph,$confirm,$deadzone,$delay,$rate,$analog,$dpad,$wrap,$updated) ON CONFLICT(DeviceMatchKey) DO UPDATE SET PreferredGlyphSetId=excluded.PreferredGlyphSetId,ConfirmConvention=excluded.ConfirmConvention,Deadzone=excluded.Deadzone,RepeatDelayMilliseconds=excluded.RepeatDelayMilliseconds,RepeatRateMilliseconds=excluded.RepeatRateMilliseconds,AnalogNavigationEnabled=excluded.AnalogNavigationEnabled,DpadNavigationEnabled=excluded.DpadNavigationEnabled,WrapNavigationEnabled=excluded.WrapNavigationEnabled,UpdatedAtUtc=excluded.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$key", preference.DeviceMatchKey); command.Parameters.AddWithValue("$glyph", Db(preference.PreferredGlyphSetId)); command.Parameters.AddWithValue("$confirm", preference.ConfirmConvention); command.Parameters.AddWithValue("$deadzone", preference.Deadzone); command.Parameters.AddWithValue("$delay", preference.RepeatDelayMilliseconds); command.Parameters.AddWithValue("$rate", preference.RepeatRateMilliseconds); command.Parameters.AddWithValue("$analog", preference.AnalogNavigationEnabled); command.Parameters.AddWithValue("$dpad", preference.DpadNavigationEnabled); command.Parameters.AddWithValue("$wrap", preference.WrapNavigationEnabled); command.Parameters.AddWithValue("$updated", Iso(preference.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task<TextInputSettings> ITextInputSettingsStore.GetAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT PreferredLayoutId,SuggestionsEnabled,SearchHistoryEnabled,MaximumSuggestions,UpdatedAtUtc FROM TextInputSettings WHERE Id='local';"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetInt32(3), At(reader.GetString(4))) : new();
    }

    public async Task SaveAsync(TextInputSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE TextInputSettings SET PreferredLayoutId=$layout,SuggestionsEnabled=$suggestions,SearchHistoryEnabled=$history,MaximumSuggestions=$maximum,UpdatedAtUtc=$updated WHERE Id='local';"; command.Parameters.AddWithValue("$layout", settings.PreferredLayoutId); command.Parameters.AddWithValue("$suggestions", settings.SuggestionsEnabled); command.Parameters.AddWithValue("$history", settings.SearchHistoryEnabled); command.Parameters.AddWithValue("$maximum", Math.Clamp(settings.MaximumSuggestions, 1, 20)); command.Parameters.AddWithValue("$updated", Iso(settings.UpdatedAtUtc ?? DateTimeOffset.UtcNow)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task<DisplayPreferences> IDisplayPreferenceStore.GetAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT PreferredMode,PreferredDisplayId,StartOnPreferredDisplay,FullscreenMode,InterfaceScale,ReduceEffectsOnBattery FROM DisplayPreferences WHERE Id='local';"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new((PreferredDisplayMode)reader.GetInt32(0), NullableString(reader, 1), reader.GetBoolean(2), (FullscreenMode)reader.GetInt32(3), reader.GetDouble(4), reader.GetBoolean(5)) : new();
    }

    public async Task SaveAsync(DisplayPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE DisplayPreferences SET PreferredMode=$mode,PreferredDisplayId=$display,StartOnPreferredDisplay=$start,FullscreenMode=$fullscreen,InterfaceScale=$scale,ReduceEffectsOnBattery=$battery,UpdatedAtUtc=$updated WHERE Id='local';"; command.Parameters.AddWithValue("$mode", (int)preferences.PreferredMode); command.Parameters.AddWithValue("$display", Db(preferences.PreferredDisplayId)); command.Parameters.AddWithValue("$start", preferences.StartOnPreferredDisplay); command.Parameters.AddWithValue("$fullscreen", (int)preferences.FullscreenMode); command.Parameters.AddWithValue("$scale", preferences.InterfaceScale); command.Parameters.AddWithValue("$battery", preferences.ReduceEffectsOnBattery); command.Parameters.AddWithValue("$updated", Iso(DateTimeOffset.UtcNow)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DisplayCalibration?> GetAsync(string displayId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT DisplayId,LeftInset,TopInset,RightInset,BottomInset,CalibrationWidth,CalibrationHeight,CalibrationScale,ArtworkOverlayDarkness,UpdatedAtUtc FROM DisplayCalibrations WHERE DisplayId=$id;"; command.Parameters.AddWithValue("$id", displayId); await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null; return new(reader.GetString(0), new(reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)), reader.GetInt32(5), reader.GetInt32(6), reader.GetDouble(7), reader.GetDouble(8), At(reader.GetString(9)));
    }

    public async Task SaveAsync(DisplayCalibration calibration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calibration); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO DisplayCalibrations(DisplayId,LeftInset,TopInset,RightInset,BottomInset,CalibrationWidth,CalibrationHeight,CalibrationScale,ArtworkOverlayDarkness,UpdatedAtUtc) VALUES($id,$left,$top,$right,$bottom,$width,$height,$scale,$overlay,$updated) ON CONFLICT(DisplayId) DO UPDATE SET LeftInset=excluded.LeftInset,TopInset=excluded.TopInset,RightInset=excluded.RightInset,BottomInset=excluded.BottomInset,CalibrationWidth=excluded.CalibrationWidth,CalibrationHeight=excluded.CalibrationHeight,CalibrationScale=excluded.CalibrationScale,ArtworkOverlayDarkness=excluded.ArtworkOverlayDarkness,UpdatedAtUtc=excluded.UpdatedAtUtc;"; command.Parameters.AddWithValue("$id", calibration.DisplayId); command.Parameters.AddWithValue("$left", calibration.SafeArea.Left); command.Parameters.AddWithValue("$top", calibration.SafeArea.Top); command.Parameters.AddWithValue("$right", calibration.SafeArea.Right); command.Parameters.AddWithValue("$bottom", calibration.SafeArea.Bottom); command.Parameters.AddWithValue("$width", calibration.CalibrationWidth); command.Parameters.AddWithValue("$height", calibration.CalibrationHeight); command.Parameters.AddWithValue("$scale", calibration.CalibrationScale); command.Parameters.AddWithValue("$overlay", calibration.ArtworkOverlayDarkness); command.Parameters.AddWithValue("$updated", Iso(calibration.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FrontendProfile> GetOwnerAsync(CancellationToken cancellationToken = default) => await GetProfileAsync(FrontendProfileType.Owner, cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("The required local Owner frontend profile is missing.");
    public async Task SaveOwnerAsync(FrontendProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.ProfileType != FrontendProfileType.Owner) throw new ArgumentException("The saved profile must be the local Owner profile.", nameof(profile));
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO FrontendProfiles(Id,Name,ProfileType,AvatarKey,ThemeOverrideId,LayoutOverrideId,IsDefault,PinRequired,Enabled,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$name,0,$avatar,$theme,$layout,1,$pin,$enabled,$created,$updated) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,AvatarKey=excluded.AvatarKey,ThemeOverrideId=excluded.ThemeOverrideId,LayoutOverrideId=excluded.LayoutOverrideId,PinRequired=excluded.PinRequired,Enabled=excluded.Enabled,UpdatedAtUtc=excluded.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name.Trim());
        command.Parameters.AddWithValue("$avatar", Db(profile.AvatarKey));
        command.Parameters.AddWithValue("$theme", Db(profile.ThemeOverrideId));
        command.Parameters.AddWithValue("$layout", Db(profile.LayoutOverrideId));
        command.Parameters.AddWithValue("$pin", profile.PinRequired);
        command.Parameters.AddWithValue("$enabled", profile.Enabled);
        command.Parameters.AddWithValue("$created", Iso(profile.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Iso(profile.UpdatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public Task<FrontendProfile?> GetGuestAsync(CancellationToken cancellationToken = default) => GetProfileAsync(FrontendProfileType.Guest, cancellationToken);
    private async Task<FrontendProfile?> GetProfileAsync(FrontendProfileType type, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,Name,ProfileType,AvatarKey,ThemeOverrideId,LayoutOverrideId,IsDefault,PinRequired,Enabled,CreatedAtUtc,UpdatedAtUtc FROM FrontendProfiles WHERE ProfileType=$type ORDER BY IsDefault DESC LIMIT 1;"; command.Parameters.AddWithValue("$type", (int)type); await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null; return new(reader.GetString(0), reader.GetString(1), (FrontendProfileType)reader.GetInt32(2), NullableString(reader, 3), NullableString(reader, 4), NullableString(reader, 5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8), At(reader.GetString(9)), At(reader.GetString(10)));
    }

    public async Task<GuestModeSettings> GetGuestSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT SettingsJson FROM GuestModeSettings WHERE Id='local';"; string? json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture); return string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<GuestModeSettings>(json, JsonOptions) ?? new();
    }

    public async Task SaveGuestAsync(FrontendProfile? profile, GuestModeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (SqliteCommand command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = "DELETE FROM FrontendProfiles WHERE ProfileType=1;"; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        if (profile is not null) { await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT INTO FrontendProfiles(Id,Name,ProfileType,AvatarKey,ThemeOverrideId,LayoutOverrideId,IsDefault,PinRequired,Enabled,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$name,$type,$avatar,$theme,$layout,$default,$pin,$enabled,$created,$updated);"; command.Parameters.AddWithValue("$id", profile.Id); command.Parameters.AddWithValue("$name", profile.Name); command.Parameters.AddWithValue("$type", (int)profile.ProfileType); command.Parameters.AddWithValue("$avatar", Db(profile.AvatarKey)); command.Parameters.AddWithValue("$theme", Db(profile.ThemeOverrideId)); command.Parameters.AddWithValue("$layout", Db(profile.LayoutOverrideId)); command.Parameters.AddWithValue("$default", profile.IsDefault); command.Parameters.AddWithValue("$pin", profile.PinRequired); command.Parameters.AddWithValue("$enabled", profile.Enabled); command.Parameters.AddWithValue("$created", Iso(profile.CreatedAtUtc)); command.Parameters.AddWithValue("$updated", Iso(profile.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (SqliteCommand command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = "UPDATE GuestModeSettings SET SettingsJson=$json,UpdatedAtUtc=$updated WHERE Id='local';"; command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(settings, JsonOptions)); command.Parameters.AddWithValue("$updated", Iso(DateTimeOffset.UtcNow)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearTemporaryAsync(CancellationToken cancellationToken = default) { await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "DELETE FROM GuestSessionActivity;"; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }

    async Task<PinCredential?> IPinCredentialStore.GetAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Salt,Hash,Iterations,UpdatedAtUtc FROM OwnerPinCredential WHERE Id='owner';"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new((byte[])reader[0], (byte[])reader[1], reader.GetInt32(2), At(reader.GetString(3))) : null;
    }

    public async Task SaveAsync(PinCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential); await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO OwnerPinCredential(Id,Salt,Hash,Iterations,UpdatedAtUtc) VALUES('owner',$salt,$hash,$iterations,$updated) ON CONFLICT(Id) DO UPDATE SET Salt=excluded.Salt,Hash=excluded.Hash,Iterations=excluded.Iterations,UpdatedAtUtc=excluded.UpdatedAtUtc;"; command.Parameters.AddWithValue("$salt", credential.Salt); command.Parameters.AddWithValue("$hash", credential.Hash); command.Parameters.AddWithValue("$iterations", credential.Iterations); command.Parameters.AddWithValue("$updated", Iso(credential.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default) { await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "DELETE FROM OwnerPinCredential WHERE Id='owner';"; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<ContentRestrictionState> GetAsync(string targetId, RestrictedContentKind kind, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT RestrictionState FROM ContentRestrictions WHERE TargetId=$id AND TargetKind=$kind;"; command.Parameters.AddWithValue("$id", targetId); command.Parameters.AddWithValue("$kind", (int)kind); object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); return value is null ? ContentRestrictionState.Allowed : (ContentRestrictionState)Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public async Task SetAsync(string targetId, RestrictedContentKind kind, ContentRestrictionState state, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = state == ContentRestrictionState.Allowed ? "DELETE FROM ContentRestrictions WHERE TargetId=$id AND TargetKind=$kind;" : "INSERT INTO ContentRestrictions(TargetId,TargetKind,RestrictionState,UpdatedAtUtc) VALUES($id,$kind,$state,$updated) ON CONFLICT(TargetId,TargetKind) DO UPDATE SET RestrictionState=excluded.RestrictionState,UpdatedAtUtc=excluded.UpdatedAtUtc;"; command.Parameters.AddWithValue("$id", targetId); command.Parameters.AddWithValue("$kind", (int)kind); if (state != ContentRestrictionState.Allowed) { command.Parameters.AddWithValue("$state", (int)state); command.Parameters.AddWithValue("$updated", Iso(DateTimeOffset.UtcNow)); } await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object Db(string? value) => value is null ? DBNull.Value : value;
    private static string? NullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
