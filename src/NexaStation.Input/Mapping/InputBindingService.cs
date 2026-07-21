using NexaStation.Core;

namespace NexaStation.Input.Mapping;

public sealed class InputBindingService : IInputBindingResolver
{
    private readonly object _gate = new();
    private IReadOnlyList<InputBinding> _bindings = DefaultInputBindings.Create();

    public IReadOnlyList<InputBinding> ActiveBindings { get { lock (_gate) return _bindings; } }

    public void Apply(IReadOnlyList<InputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        lock (_gate) _bindings = bindings.OrderBy(binding => binding.SortOrder).ToArray();
    }

    public InputAction? ResolveAction(PhysicalInput physicalInput, InputContext context, string? deviceId = null)
    {
        ArgumentNullException.ThrowIfNull(physicalInput);
        lock (_gate)
        {
            return _bindings.FirstOrDefault(binding =>
                binding.PhysicalInput.Kind == physicalInput.Kind &&
                string.Equals(binding.PhysicalInput.NormalizedCode, physicalInput.NormalizedCode, StringComparison.Ordinal) &&
                binding.PhysicalInput.Direction == physicalInput.Direction &&
                binding.Context == context)?.Action
                ?? _bindings.FirstOrDefault(binding =>
                    binding.PhysicalInput.Kind == physicalInput.Kind &&
                    string.Equals(binding.PhysicalInput.NormalizedCode, physicalInput.NormalizedCode, StringComparison.Ordinal) &&
                    binding.PhysicalInput.Direction == physicalInput.Direction &&
                    binding.Context == InputContext.Global)?.Action;
        }
    }

    public PhysicalInput? ResolvePhysical(InputAction action, InputContext context, string? deviceId = null)
    {
        lock (_gate)
            return _bindings.FirstOrDefault(binding => binding.Action == action && binding.Context == context)?.PhysicalInput
                ?? _bindings.FirstOrDefault(binding => binding.Action == action && binding.Context == InputContext.Global)?.PhysicalInput;
    }
}

public sealed class InputMappingValidator
{
    private readonly HashSet<InputAction> _requiredSafetyActions = [InputAction.Cancel, InputAction.EmergencyDisable];
    private readonly HashSet<InputAction> _directions = [InputAction.NavigateUp, InputAction.NavigateDown, InputAction.NavigateLeft, InputAction.NavigateRight];

    public InputMappingValidationResult Validate(IReadOnlyList<InputBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        List<MappingConflict> conflicts = [];
        foreach (InputAction action in _requiredSafetyActions.Where(action => bindings.All(binding => binding.Action != action)))
            conflicts.Add(new(MappingConflictType.ReservedSafetyConflict, action, null, $"{action} must remain bound so input can be recovered.", true));

        foreach (IGrouping<(InputContext Context, PhysicalInputKind Kind, string Code, int Direction), InputBinding> group in bindings.GroupBy(binding => (binding.Context, binding.PhysicalInput.Kind, binding.PhysicalInput.NormalizedCode, binding.PhysicalInput.Direction)).Where(group => group.Select(binding => binding.Action).Distinct().Count() > 1))
        {
            InputBinding first = group.First();
            foreach (InputBinding duplicate in group.Skip(1))
                conflicts.Add(new(MappingConflictType.ExactConflict, duplicate.Action, first.Action, $"{duplicate.Action} conflicts with {first.Action} in {group.Key.Context}.", true));
        }

        foreach (InputBinding binding in bindings.Where(binding => _directions.Contains(binding.Action) && binding.PhysicalInput.Kind is not (PhysicalInputKind.KeyboardKey or PhysicalInputKind.ControllerButton or PhysicalInputKind.ControllerAxis)))
            conflicts.Add(new(MappingConflictType.UnsupportedInput, binding.Action, null, "Navigation directions require a safe directional key, button, or axis.", true));

        foreach (InputBinding binding in bindings.Where(binding => binding.PhysicalInput.Kind == PhysicalInputKind.ControllerAxis && binding.PhysicalInput.Direction == 0))
            conflicts.Add(new(MappingConflictType.AmbiguousAnalogConflict, binding.Action, null, "Analog navigation bindings must specify a direction.", true));
        return new(conflicts.All(conflict => !conflict.Blocking), conflicts);
    }
}

public sealed class InputMappingService(IInputProfileStore store, InputBindingService resolver, InputMappingValidator validator)
{
    public async Task<InputMappingValidationResult> SaveAsync(InputProfile profile, IReadOnlyList<InputBinding> bindings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(bindings);
        InputMappingValidationResult result = validator.Validate(bindings);
        if (!result.IsValid) return result;
        await store.SaveProfileAsync(profile, bindings, cancellationToken).ConfigureAwait(false);
        resolver.Apply(bindings);
        return result;
    }

    public async Task RestoreDefaultsAsync(string? profileId = null, CancellationToken cancellationToken = default)
    {
        await store.ResetAsync(profileId, cancellationToken).ConfigureAwait(false);
        resolver.Apply(DefaultInputBindings.Create());
    }
}

public sealed class InMemoryInputProfileStore : IInputProfileStore
{
    private readonly Dictionary<string, (InputProfile Profile, IReadOnlyList<InputBinding> Bindings)> _profiles = [];
    public Task<IReadOnlyList<InputProfile>> GetProfilesAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<InputProfile>>(_profiles.Values.Select(value => value.Profile).ToArray()); }
    public Task<IReadOnlyList<InputBinding>> GetBindingsAsync(string profileId, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_profiles.TryGetValue(profileId, out var value) ? value.Bindings : (IReadOnlyList<InputBinding>)[]); }
    public Task SaveProfileAsync(InputProfile profile, IReadOnlyList<InputBinding> bindings, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _profiles[profile.Id] = (profile, bindings.ToArray()); return Task.CompletedTask; }
    public Task ResetAsync(string? profileId, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); if (profileId is null) _profiles.Clear(); else _profiles.Remove(profileId); return Task.CompletedTask; }
}
