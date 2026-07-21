using NexaStation.Domain;

namespace NexaStation.Core;

public sealed class LaunchStateChangedEventArgs(LaunchSession session) : EventArgs
{
    public LaunchSession Session { get; } = session;
}
