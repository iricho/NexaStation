using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Discovery;

public sealed class RetroArchCoreDiscovery : IRetroArchCoreDiscovery
{
    public Task<IReadOnlyList<RetroArchCore>> DiscoverAsync(EmulatorInstallation installation,string coreFolder,CancellationToken cancellationToken=default)
    {
        if(!string.Equals(installation.AdapterId,"retroarch",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Core discovery requires a RetroArch installation.",nameof(installation));
        string folder=Path.GetFullPath(coreFolder);if(!Directory.Exists(folder))throw new DirectoryNotFoundException("The selected RetroArch core folder does not exist.");List<RetroArchCore> result=[];
        foreach(string path in Directory.EnumerateFiles(folder,"*_libretro.dll",SearchOption.TopDirectoryOnly))
        {cancellationToken.ThrowIfCancellationRequested();FileInfo info=new(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;string file=Path.GetFileName(path);string name=Path.GetFileNameWithoutExtension(file).Replace("_libretro",string.Empty,StringComparison.OrdinalIgnoreCase).Replace('_',' ');result.Add(new(Guid.NewGuid().ToString("N"),installation.Id,name,path,file,[],false,true,DateTime.UtcNow));}
        return Task.FromResult<IReadOnlyList<RetroArchCore>>(result.OrderBy(core=>core.DisplayName,StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
