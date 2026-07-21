namespace NexaStation.Domain;

public enum DiscoverAppKind { StoreApplication,DesktopApplication,ProgressiveWebApplication,WebService,Mixed }
public enum DiscoverAppInstallationState { Installed,NotInstalled,InstallationUnknown,StoreUnavailable,UpdateAvailableUnknown,BrokenInstallation }
public enum DiscoverAppActionKind { Open,OpenStore,Install,OpenWebApp,CheckInstallation,Unavailable }

public sealed record DiscoverAppDefinition
{
    public required string Id{get;init;}public required string DisplayName{get;init;}public required string Description{get;init;}public required string Category{get;init;}public DiscoverAppKind AppKind{get;init;}
    public string? StoreProductId{get;init;}public string? WingetPackageId{get;init;}public IReadOnlyList<string> KnownAppUserModelIds{get;init;}=[];public IReadOnlyList<string> KnownPackageFamilyNames{get;init;}=[];public IReadOnlyList<string> KnownExecutableNames{get;init;}=[];public IReadOnlyList<string> KnownPwaIdentities{get;init;}=[];
    public Uri? WebFallbackUri{get;init;}public string? IconAssetPath{get;init;}public bool Featured{get;init;}public int SortOrder{get;init;}public IReadOnlyList<string> SupportedRegions{get;init;}=["*"];public bool Enabled{get;init;}=true;
}

public sealed record DiscoverAppState(DiscoverAppInstallationState State,string? LibraryItemId=null,string? MatchedIdentity=null,string? Reason=null)
{
    public bool IsInstalled=>State==DiscoverAppInstallationState.Installed&&LibraryItemId is not null;
}

public sealed record StoreNavigationResult(bool Succeeded,bool StoreAvailable,Uri? Uri,string Message);
public sealed record ExternalUriNavigationResult(bool Succeeded,Uri Uri,string Message);
