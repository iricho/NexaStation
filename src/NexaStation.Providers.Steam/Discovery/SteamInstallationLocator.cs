using Microsoft.Win32;
using NexaStation.Core;

namespace NexaStation.Providers.Steam.Discovery;

public sealed class SteamInstallationLocator(ISettingsService? settings = null) : ISteamInstallationLocator
{
    public const string ConfirmedPathSetting = "Providers.Steam.InstallationPath";

    public async Task<SteamInstallationDetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        List<(string? Path,string Source)> candidates=[];
        if(settings is not null)candidates.Add((await settings.GetAsync<string?>(ConfirmedPathSetting,null,cancellationToken).ConfigureAwait(false),"user-confirmed setting"));
        cancellationToken.ThrowIfCancellationRequested();
        candidates.Add((ReadRegistry(Registry.CurrentUser,@"Software\Valve\Steam","SteamPath"),"current-user registry"));
        candidates.Add((ReadRegistry(Registry.CurrentUser,@"Software\Valve\Steam","SteamExe") is string exe?Path.GetDirectoryName(exe):null,"current-user executable registry"));
        candidates.Add((ReadRegistry(Registry.LocalMachine,@"SOFTWARE\WOW6432Node\Valve\Steam","InstallPath"),"machine registry"));
        candidates.Add((ReadRegistry(Registry.LocalMachine,@"SOFTWARE\Valve\Steam","InstallPath"),"machine registry"));
        candidates.Add((Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"),"common path"));

        List<string> diagnostics=[];HashSet<string> seen=new(StringComparer.OrdinalIgnoreCase);
        foreach((string? candidate,string source) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(candidate))continue;
            string normalized;
            try{normalized=Normalize(candidate);}catch(Exception exception) when(exception is ArgumentException or NotSupportedException or PathTooLongException){diagnostics.Add($"Ignored invalid {source} candidate.");continue;}
            if(!seen.Add(normalized))continue;
            if(IsValid(normalized))return new(true,normalized,"Steam was detected.",[ $"Validated {source}." ]);
            diagnostics.Add($"The {source} candidate did not contain steam.exe and steamapps.");
        }
        return new(false,null,"Steam was not detected automatically.",diagnostics);
    }

    public static bool IsValid(string path)=>File.Exists(Path.Combine(path,"steam.exe"))&&Directory.Exists(Path.Combine(path,"steamapps"));
    public static string Normalize(string path)=>Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    private static string? ReadRegistry(RegistryKey root,string keyPath,string valueName){try{using RegistryKey? key=root.OpenSubKey(keyPath,false);return key?.GetValue(valueName) as string;}catch(Exception exception) when(exception is UnauthorizedAccessException or IOException or System.Security.SecurityException){return null;}}
}
