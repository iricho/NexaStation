namespace NexaStation.Recovery;

public interface INexaStationPaths
{
    string InstallationDirectory { get; }
    string PortableMarkerPath { get; }
    bool IsPortable { get; }
    string Root { get; }
    string Data { get; }
    string Database { get; }
    string State { get; }
    string OnboardingState { get; }
    string StartupHealthState { get; }
    string Logs { get; }
    string CrashReports { get; }
    string Backups { get; }
    string Diagnostics { get; }
    string Cache { get; }
    string ArtworkCache { get; }
    string ThumbnailCache { get; }
    string IconCache { get; }
    string ProviderCache { get; }
    string UiSoundCache { get; }
    string CollectionMosaicCache { get; }
    string Backgrounds { get; }
    string ThemeBackgrounds { get; }
    string Themes { get; }
    void EnsureCreated();
}

public sealed record NexaStationPathOptions(
    string? InstallationDirectory = null,
    string? LocalApplicationDataDirectory = null,
    string? RootOverride = null);

public sealed class NexaStationPaths : INexaStationPaths
{
    public NexaStationPaths(string? root = null)
        : this(new NexaStationPathOptions(RootOverride: root))
    {
    }

    public NexaStationPaths(NexaStationPathOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        InstallationDirectory = NormalizeDirectory(options.InstallationDirectory ?? AppContext.BaseDirectory);
        PortableMarkerPath = Path.Combine(InstallationDirectory, "portable.flag");
        IsPortable = options.RootOverride is null && File.Exists(PortableMarkerPath);

        string localApplicationData = NormalizeDirectory(options.LocalApplicationDataDirectory ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Root = NormalizeDirectory(options.RootOverride ?? (IsPortable
            ? Path.Combine(InstallationDirectory, "Data")
            : Path.Combine(localApplicationData, "NexaStation")));

        // Preserve the established installed layout while making the portable Data folder
        // the complete mutable-data root. Existing installed databases therefore need no move.
        Data = IsPortable ? Root : Path.Combine(Root, "Data");
        Database = Path.Combine(Data, "nexastation.db");
        State = Path.Combine(Root, "State");
        OnboardingState = Path.Combine(State, "onboarding.v1.json");
        StartupHealthState = Path.Combine(State, "startup-health.v1.json");
        Logs = Path.Combine(Root, "Logs");
        CrashReports = Path.Combine(Root, "CrashReports");
        Backups = Path.Combine(Root, "Backups");
        Diagnostics = Path.Combine(Root, "Diagnostics");
        Cache = Path.Combine(Root, "Cache");
        ArtworkCache = Path.Combine(Cache, "Artwork");
        ThumbnailCache = Path.Combine(Cache, "Gallery");
        IconCache = Path.Combine(Cache, "ApplicationIcons");
        ProviderCache = Path.Combine(Cache, "Providers");
        UiSoundCache = Path.Combine(Cache, "UiSounds");
        CollectionMosaicCache = Path.Combine(Cache, "CollectionMosaics");
        Backgrounds = Path.Combine(Root, "Backgrounds");
        ThemeBackgrounds = Path.Combine(Backgrounds, "Themes");
        Themes = Path.Combine(Root, "Themes");
    }

    public string InstallationDirectory { get; }
    public string PortableMarkerPath { get; }
    public bool IsPortable { get; }
    public string Root { get; }
    public string Data { get; }
    public string Database { get; }
    public string State { get; }
    public string OnboardingState { get; }
    public string StartupHealthState { get; }
    public string Logs { get; }
    public string CrashReports { get; }
    public string Backups { get; }
    public string Diagnostics { get; }
    public string Cache { get; }
    public string ArtworkCache { get; }
    public string ThumbnailCache { get; }
    public string IconCache { get; }
    public string ProviderCache { get; }
    public string UiSoundCache { get; }
    public string CollectionMosaicCache { get; }
    public string Backgrounds { get; }
    public string ThemeBackgrounds { get; }
    public string Themes { get; }

    public void EnsureCreated()
    {
        try
        {
            foreach (string directory in new[] { Root, Data, State, Logs, CrashReports, Backups, Diagnostics, Cache, ArtworkCache, ThumbnailCache, IconCache, ProviderCache, UiSoundCache, CollectionMosaicCache, Backgrounds, ThemeBackgrounds, Themes })
            {
                Directory.CreateDirectory(directory);
            }

            string writeProbe = Path.Combine(Root, $".write-test-{Guid.NewGuid():N}.tmp");
            using FileStream stream = new(writeProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            string guidance = IsPortable
                ? "Move the extracted NexaStation folder to a writable location and try again."
                : "Check access to the NexaStation folder under LocalAppData and try again.";
            throw new IOException($"NexaStation cannot write to its {(IsPortable ? "portable" : "installed")} data folder '{Root}'. {guidance}", exception);
        }
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A NexaStation directory path cannot be empty.", nameof(path));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
