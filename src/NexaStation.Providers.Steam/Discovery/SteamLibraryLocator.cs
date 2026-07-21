using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Domain;

namespace NexaStation.Providers.Steam.Discovery;

public sealed class SteamLibraryLocator(IKeyValuesParser parser) : ISteamLibraryLocator
{
    public async Task<IReadOnlyList<SteamLibraryLocation>> FindLibrariesAsync(string steamRoot, CancellationToken cancellationToken = default)
    {
        string root=SteamInstallationLocator.Normalize(steamRoot);
        Dictionary<string,SteamLibraryLocation> locations=new(StringComparer.OrdinalIgnoreCase);
        Add(locations,Create(root,SteamLibrarySource.PrimaryInstallation));
        string configuration=Path.Combine(root,"steamapps","libraryfolders.vdf");
        if(!File.Exists(configuration))return locations.Values.ToArray();
        string content;
        try{content=await File.ReadAllTextAsync(configuration,cancellationToken).ConfigureAwait(false);}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){locations[root]=locations[root] with{DiagnosticMessage=$"Library configuration could not be read: {exception.Message}"};return locations.Values.ToArray();}
        KeyValuesDocument document=parser.Parse(content);
        KeyValuesNode? rootNode=document.Root.Find("libraryfolders")??document.Root.Children.FirstOrDefault(node=>node.IsObject);
        if(rootNode is null||document.HasErrors){locations[root]=locations[root] with{DiagnosticMessage="Library configuration is malformed; the primary library remains available."};}
        if(rootNode is not null)
        {
            foreach(KeyValuesNode child in rootNode.Children.Where(node=>int.TryParse(node.Key,out _)))
            {
                string? path=child.Value??FindPath(child);
                if(string.IsNullOrWhiteSpace(path))continue;
                try{Add(locations,Create(path,SteamLibrarySource.LibraryConfiguration));}catch(Exception exception) when(exception is ArgumentException or NotSupportedException or PathTooLongException){locations[root]=locations[root] with{DiagnosticMessage=$"A configured library path was invalid: {exception.Message}"};}
            }
        }
        return locations.Values.ToArray();
    }

    private static string? FindPath(KeyValuesNode node)
    {
        string? direct=node.ValueOf("path");if(!string.IsNullOrWhiteSpace(direct))return direct;
        foreach(KeyValuesNode child in node.Children.Where(value=>value.IsObject)){string? nested=FindPath(child);if(!string.IsNullOrWhiteSpace(nested))return nested;}
        return null;
    }
    private static SteamLibraryLocation Create(string path,SteamLibrarySource source){string normalized=SteamInstallationLocator.Normalize(path);string apps=Path.Combine(normalized,"steamapps");bool available=Directory.Exists(apps);return new(Id(normalized),normalized,apps,Path.Combine(apps,"common"),available,source,available?null:"The library is currently unavailable or does not contain steamapps.");}
    private static void Add(Dictionary<string,SteamLibraryLocation> values,SteamLibraryLocation value){if(!values.ContainsKey(value.Path))values[value.Path]=value;}
    private static string Id(string path)=>ProviderLibraryIdentity.CreateId("steam",path);
}
