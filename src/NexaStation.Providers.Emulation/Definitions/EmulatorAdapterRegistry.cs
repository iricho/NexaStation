using NexaStation.Core;

namespace NexaStation.Providers.Emulation.Definitions;

public sealed class EmulatorAdapterRegistry(IEnumerable<IEmulatorAdapter> adapters) : IEmulatorAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IEmulatorAdapter> _adapters = adapters.ToDictionary(adapter => adapter.Id, StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<IEmulatorAdapter> Adapters => _adapters.Values.ToArray();
    public IEmulatorAdapter? GetById(string adapterId) => _adapters.TryGetValue(adapterId, out IEmulatorAdapter? adapter) ? adapter : null;
}
