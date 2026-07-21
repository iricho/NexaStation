using NexaStation.Core;
using NexaStation.Domain;
using Windows.Management.Deployment;
using System.Security.Cryptography;
using System.Text;
using Windows.Storage.Streams;
using NexaStation.Recovery;

namespace NexaStation.Providers.WindowsApps.PackagedApps;

public sealed class PackagedApplicationReader : IAppsFolderApplicationReader
{
    private readonly string _iconCache;

    public PackagedApplicationReader():this(new NexaStationPaths()){}
    public PackagedApplicationReader(INexaStationPaths paths)=>_iconCache=paths.IconCache;

    public async Task<IReadOnlyList<AppsFolderApplication>> ReadAsync(CancellationToken cancellationToken = default)
    {
        List<AppsFolderApplication> result = [];
        PackageManager manager = new();
        foreach (Windows.ApplicationModel.Package package in manager.FindPackagesForUser(string.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<Windows.ApplicationModel.Core.AppListEntry> entries = await package.GetAppListEntriesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                foreach (Windows.ApplicationModel.Core.AppListEntry entry in entries)
                {
                    string aumid = entry.AppUserModelId;
                    if (string.IsNullOrWhiteSpace(aumid) || string.IsNullOrWhiteSpace(entry.DisplayInfo.DisplayName)) continue;
                    string? applicationId = aumid.Contains('!', StringComparison.Ordinal) ? aumid[(aumid.IndexOf('!') + 1)..] : null;
                    string? iconPath=await CacheLogoAsync(entry,aumid,cancellationToken).ConfigureAwait(false);
                    result.Add(new(entry.DisplayInfo.DisplayName, aumid, package.Id.FamilyName, package.Id.FullName, applicationId, iconPath, package.PublisherDisplayName, $"shell:AppsFolder\\{aumid}", package.Status.VerifyIsOK()));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        }
        return result;
    }

    private async Task<string?> CacheLogoAsync(Windows.ApplicationModel.Core.AppListEntry entry,string identity,CancellationToken cancellationToken)
    {
        try
        {
            RandomAccessStreamReference logo=entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(256,256));using IRandomAccessStreamWithContentType stream=await logo.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);if(stream.Size is 0 or >16_777_216)return null;
            using DataReader reader=new(stream);uint size=(uint)stream.Size;await reader.LoadAsync(size).AsTask(cancellationToken).ConfigureAwait(false);byte[] bytes=new byte[(int)size];reader.ReadBytes(bytes);
            Directory.CreateDirectory(_iconCache);string contentHash=Convert.ToHexString(SHA256.HashData(bytes))[..24].ToLowerInvariant();string name=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity+"|"+contentHash)))[..24].ToLowerInvariant()+"-256.png";string path=Path.Combine(_iconCache,name);if(!File.Exists(path))await File.WriteAllBytesAsync(path,bytes,cancellationToken).ConfigureAwait(false);return path;
        }
        catch(Exception exception)when(exception is UnauthorizedAccessException or IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException){return null;}
    }
}
