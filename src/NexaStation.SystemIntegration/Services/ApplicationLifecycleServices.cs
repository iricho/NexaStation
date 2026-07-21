using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using StartupTask = Windows.ApplicationModel.StartupTask;
using StartupTaskState = Windows.ApplicationModel.StartupTaskState;

namespace NexaStation.SystemIntegration.Services;

public enum ActivationRouteKind
{
    Launch,
    Section,
    Unsupported,
}

public sealed record ActivationRoute(ActivationRouteKind Kind, string? Section = null, Uri? Uri = null);

public interface IActivationRouter
{
    ActivationRoute Route(AppActivationArguments arguments);
    ActivationRoute Route(Uri uri);
}

public sealed class ActivationRouter : IActivationRouter
{
    private static readonly HashSet<string> SupportedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "home", "games", "apps", "media", "gallery", "library", "collections", "settings",
    };

    public ActivationRoute Route(AppActivationArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Data is IProtocolActivatedEventArgs protocolArguments)
        {
            return Route(protocolArguments.Uri);
        }

        return arguments.Kind == ExtendedActivationKind.Launch
            ? new(ActivationRouteKind.Launch)
            : new(ActivationRouteKind.Unsupported);
    }

    public ActivationRoute Route(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!string.Equals(uri.Scheme, "nexastation", StringComparison.OrdinalIgnoreCase))
        {
            return new(ActivationRouteKind.Unsupported, Uri: uri);
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? section = string.Equals(uri.Host, "section", StringComparison.OrdinalIgnoreCase) && segments.Length > 0 ? segments[0] : null;
        return section is not null && SupportedSections.Contains(section)
            ? new(ActivationRouteKind.Section, section.ToLowerInvariant(), uri)
            : new(ActivationRouteKind.Unsupported, Uri: uri);
    }
}

public sealed record SingleInstanceResult(bool IsPrimary, bool RedirectCompleted, ActivationRoute InitialRoute);

public interface ISingleInstanceCoordinator : IDisposable
{
    event EventHandler<ActivationRoute>? ActivationReceived;
    Task<SingleInstanceResult> RegisterAsync(string key, AppActivationArguments arguments, TimeSpan redirectTimeout, CancellationToken cancellationToken = default);
}

public sealed class AppInstanceCoordinator(IActivationRouter router) : ISingleInstanceCoordinator
{
    private AppInstance? _instance;

    public event EventHandler<ActivationRoute>? ActivationReceived;

    public async Task<SingleInstanceResult> RegisterAsync(string key, AppActivationArguments arguments, TimeSpan redirectTimeout, CancellationToken cancellationToken = default)
    {
        if (_instance is not null)
        {
            throw new InvalidOperationException("The single-instance coordinator has already been registered.");
        }

        _instance = AppInstance.FindOrRegisterForKey(key);
        ActivationRoute initialRoute = router.Route(arguments);
        if (!_instance.IsCurrent)
        {
            try
            {
                await _instance.RedirectActivationToAsync(arguments).AsTask(cancellationToken).WaitAsync(redirectTimeout, cancellationToken).ConfigureAwait(false);
                return new(false, true, initialRoute);
            }
            catch (TimeoutException)
            {
                return new(false, false, initialRoute);
            }
        }

        _instance.Activated += OnActivated;
        return new(true, false, initialRoute);
    }

    public void Dispose()
    {
        if (_instance is not null)
        {
            _instance.Activated -= OnActivated;
            _instance = null;
        }
    }

    private void OnActivated(object? sender, AppActivationArguments arguments) => ActivationReceived?.Invoke(this, router.Route(arguments));
}

public enum StartupTaskAvailability
{
    NotSupported,
    Disabled,
    Enabled,
    DisabledByPolicy,
}

public sealed record StartupTaskStatus(StartupTaskAvailability Availability, string Message);

public interface IStartupTaskService
{
    Task<StartupTaskStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<StartupTaskStatus> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}

public sealed class PackagedStartupTaskService : IStartupTaskService
{
    public async Task<StartupTaskStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync("NexaStationStartup").AsTask(cancellationToken).ConfigureAwait(false);
            return Map(task.State);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            return new(StartupTaskAvailability.NotSupported, "Start with Windows is available only in an installed MSIX build.");
        }
    }

    public async Task<StartupTaskStatus> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync("NexaStationStartup").AsTask(cancellationToken).ConfigureAwait(false);
            if (!enabled)
            {
                task.Disable();
                return Map(task.State);
            }

            StartupTaskState state = await task.RequestEnableAsync().AsTask(cancellationToken).ConfigureAwait(false);
            return Map(state);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            return new(StartupTaskAvailability.NotSupported, "Start with Windows is available only in an installed MSIX build.");
        }
    }

    private static StartupTaskStatus Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => new(StartupTaskAvailability.Enabled, "NexaStation will start after Windows sign-in."),
        StartupTaskState.DisabledByPolicy => new(StartupTaskAvailability.DisabledByPolicy, "Windows policy prevents this startup task from being enabled."),
        _ => new(StartupTaskAvailability.Disabled, "Start with Windows is off."),
    };
}
