namespace NexaStation.Recovery;

public interface INexaStationPaths
{
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
    string CollectionMosaicCache { get; }
    string Backgrounds { get; }
    string ThemeBackgrounds { get; }
    string Themes { get; }
    void EnsureCreated();
}

public sealed class NexaStationPaths : INexaStationPaths
{
    public NexaStationPaths(string? root = null)
    {
        Root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexaStation"));
        Data = Path.Combine(Root, "Data");
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
        CollectionMosaicCache = Path.Combine(Cache, "CollectionMosaics");
        Backgrounds = Path.Combine(Root, "Backgrounds");
        ThemeBackgrounds = Path.Combine(Backgrounds, "Themes");
        Themes = Path.Combine(Root, "Themes");
    }

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
    public string CollectionMosaicCache { get; }
    public string Backgrounds { get; }
    public string ThemeBackgrounds { get; }
    public string Themes { get; }

    public void EnsureCreated()
    {
        foreach (string directory in new[] { Root, Data, State, Logs, CrashReports, Backups, Diagnostics, Cache, ArtworkCache, ThumbnailCache, IconCache, ProviderCache, CollectionMosaicCache, Backgrounds, ThemeBackgrounds, Themes })
        {
            Directory.CreateDirectory(directory);
        }
    }
}
