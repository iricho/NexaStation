using NexaStation.Domain;

namespace NexaStation.Providers.Steam.Validation;

public static class SteamProviderValidator
{
    public static ProviderValidationResult Validate(ProviderSource source)
    {
        List<string> diagnostics=[];
        if(!string.Equals(source.ProviderId,"steam",StringComparison.OrdinalIgnoreCase))diagnostics.Add("The source is not owned by the Steam provider.");
        if(!long.TryParse(source.ProviderItemId,out long appId)||appId<=0)diagnostics.Add("Steam App ID must be a positive number.");
        if(!source.Installed)diagnostics.Add("The Steam source is marked as not installed.");
        if(string.IsNullOrWhiteSpace(source.InstallationPath)||!Directory.Exists(source.InstallationPath))diagnostics.Add("The installation directory is unavailable.");
        return diagnostics.Count==0?new(true,"The Steam source is valid.",[]):new(false,diagnostics[0],diagnostics);
    }
}
