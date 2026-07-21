using NexaStation.Core;

namespace NexaStation.Input.Mapping;

public enum InputDeviceScope { Global, KeyboardMouse, ControllerFamily, SpecificController }
public enum InputContext { Global, Dashboard, GalleryViewer, MediaPlayer, OnScreenKeyboard, QuickMenu, DesktopControllerMode, Dialog, TextEntry, DisplayCalibration }
public enum PhysicalInputKind { KeyboardKey, MouseButton, ControllerButton, ControllerAxis, ControllerChord }

public sealed record PhysicalInput(PhysicalInputKind Kind, string Code, int Direction = 0)
{
    public string NormalizedCode => Code.Trim().ToUpperInvariant();
}

public sealed record InputProfile(
    string Id,
    string Name,
    InputDeviceScope DeviceScope,
    string? DeviceMatchJson,
    string? GlyphSetId,
    bool Enabled,
    bool BuiltIn,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InputBinding(
    string Id,
    string InputProfileId,
    InputAction Action,
    PhysicalInput PhysicalInput,
    InputContext Context,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public enum MappingConflictType { ExactConflict, ContextConflict, ReservedSafetyConflict, AmbiguousAnalogConflict, UnsupportedInput }
public sealed record MappingConflict(MappingConflictType Type, InputAction Action, InputAction? ExistingAction, string Message, bool Blocking);
public sealed record InputMappingValidationResult(bool IsValid, IReadOnlyList<MappingConflict> Conflicts);

public sealed record ControllerPreference(
    string DeviceMatchKey,
    string? PreferredGlyphSetId,
    int ConfirmConvention,
    double Deadzone,
    int RepeatDelayMilliseconds,
    int RepeatRateMilliseconds,
    bool AnalogNavigationEnabled,
    bool DpadNavigationEnabled,
    bool WrapNavigationEnabled,
    DateTimeOffset UpdatedAtUtc);

public interface IControllerPreferenceStore
{
    Task<ControllerPreference?> GetAsync(string deviceMatchKey, CancellationToken cancellationToken = default);
    Task SaveAsync(ControllerPreference preference, CancellationToken cancellationToken = default);
}

public interface IInputProfileStore
{
    Task<IReadOnlyList<InputProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InputBinding>> GetBindingsAsync(string profileId, CancellationToken cancellationToken = default);
    Task SaveProfileAsync(InputProfile profile, IReadOnlyList<InputBinding> bindings, CancellationToken cancellationToken = default);
    Task ResetAsync(string? profileId, CancellationToken cancellationToken = default);
}

public interface IInputBindingResolver
{
    InputAction? ResolveAction(PhysicalInput physicalInput, InputContext context, string? deviceId = null);
    PhysicalInput? ResolvePhysical(InputAction action, InputContext context, string? deviceId = null);
    IReadOnlyList<InputBinding> ActiveBindings { get; }
}

public interface IInputContextService
{
    InputContext Current { get; }
    void SetCurrent(InputContext context);
}

public sealed class InputContextService : IInputContextService
{
    private InputContext _current = InputContext.Global;
    public InputContext Current => _current;
    public void SetCurrent(InputContext context) => _current = context;
}
