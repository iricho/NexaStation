namespace NexaStation.Core;

public interface IInputService
{
    event EventHandler<InputActionEventArgs>? ActionTriggered;

    void Start();

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "The Phase 0 input abstraction explicitly requires Start and Stop methods.")]
    void Stop();
}

public sealed record InputNavigationSettings(
    double NavigationDeadzone = 0.55,
    bool DisableAnalogNavigation = false,
    bool UseDpadOnly = false,
    int RepeatDelayMilliseconds = 350,
    int RepeatRateMilliseconds = 120);

public interface IInputCompatibilitySink
{
    void ApplyCompatibility(InputNavigationSettings settings);
}
