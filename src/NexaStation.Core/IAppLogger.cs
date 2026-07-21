namespace NexaStation.Core;

public interface IAppLogger
{
    void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null);

    void LogError(string eventName, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);
}
