namespace NexaStation.Domain;

public enum LaunchState
{
    Idle,
    Validating,
    Starting,
    Running,
    Restoring,
    Completed,
    Failed,
}
