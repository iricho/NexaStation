namespace NexaStation.Input.Validation;

public sealed class EmergencyMappingResetDetector(TimeProvider? timeProvider = null, TimeSpan? holdDuration = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _holdDuration = holdDuration ?? TimeSpan.FromSeconds(3);
    private DateTimeOffset? _startedAt;
    private bool _raised;

    public bool Update(bool leftShoulder, bool rightShoulder, bool leftStick, bool rightStick)
    {
        bool completeChord = leftShoulder && rightShoulder && leftStick && rightStick;
        if (!completeChord) { _startedAt = null; _raised = false; return false; }
        _startedAt ??= _timeProvider.GetUtcNow();
        if (_raised || _timeProvider.GetUtcNow() - _startedAt < _holdDuration) return false;
        _raised = true;
        return true;
    }
}
