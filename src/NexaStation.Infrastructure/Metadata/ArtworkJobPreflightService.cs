using System.Globalization;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;

namespace NexaStation.Infrastructure.Metadata;

public sealed class ArtworkJobPreflightService(ISqliteConnectionFactory connections,IEnumerable<IArtworkProvider> providers,ISettingsService settings) : IArtworkJobPreflightService
{
    private readonly IArtworkProvider[] _providers=providers.ToArray();
    public async Task<ArtworkJobPreflightResult> CheckAsync(ArtworkJobRequest request,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);bool allowOnline=await settings.GetAsync("Metadata.Privacy.AllowOnlineArtwork",false,cancellationToken).ConfigureAwait(false);Dictionary<string,(bool Enabled,int Priority,ProviderValidationState? Validation)> configuration=await ReadConfigurationAsync(cancellationToken).ConfigureAwait(false);List<ArtworkProviderReadiness> readiness=[];
        foreach(IArtworkProvider provider in _providers.OrderBy(value=>configuration.TryGetValue(value.Id,out var state)?state.Priority:int.MaxValue).ThenBy(value=>value.DisplayName,StringComparer.CurrentCultureIgnoreCase))
        {
            bool keyless=provider.Id is "steam-public" or "gog-public" or "ps2-covers";bool configured=configuration.TryGetValue(provider.Id,out var state);bool enabled=keyless||configured&&state.Enabled;ArtworkProviderStatus status=await provider.GetStatusAsync(cancellationToken).ConfigureAwait(false);bool permitted=provider.Id=="local"||allowOnline;bool validated=keyless||provider.Id=="local"||configured&&state.Validation==ProviderValidationState.Valid;string? message=!permitted?"Online artwork lookups are disabled.":!validated&&status.Authenticated?"Test this provider connection before starting a job.":status.Message;
            readiness.Add(new(provider.Id,provider.DisplayName,enabled,status.Available&&permitted,status.Authenticated&&permitted&&validated,provider.Capabilities,!validated&&status.Authenticated?"credential_unvalidated":status.FailureCode,message,status.RemainingQuota,status.QuotaResetsAtUtc));
        }
        bool supports=readiness.Any(value=>value.Enabled&&value.Available&&value.Authenticated&&request.AssetTypes.Any(type=>Supports(value.Capabilities,type)));List<string> blocking=[];List<string> warnings=[];
        if(request.LibraryItemIds.Count==0)blocking.Add("No eligible games were selected.");if(request.AssetTypes.Count==0)blocking.Add("Choose at least one artwork type.");if(!supports)blocking.Add(allowOnline?"No enabled artwork provider is ready. Get a free personal SteamGridDB or TheGamesDB API key, test it, or add matching files to the local import folder.":"Online artwork is disabled and the local artwork source is unavailable or empty.");
        foreach(ArtworkProviderReadiness provider in readiness.Where(value=>value.Enabled&&(!value.Available||!value.Authenticated)))warnings.Add($"{provider.DisplayName}: {provider.Message??provider.FailureCode??"not ready"}");
        return new(blocking.Count==0,request.LibraryItemIds.Distinct(StringComparer.Ordinal).Count(),readiness,blocking,warnings);
    }
    private async Task<Dictionary<string,(bool Enabled,int Priority,ProviderValidationState? Validation)>> ReadConfigurationAsync(CancellationToken token)
    {
        await using SqliteConnection connection=await connections.OpenAsync(token).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT m.Id,m.Enabled,m.Priority,c.ValidationState FROM MetadataProviders m LEFT JOIN ProviderCredentialReferences c ON c.ProviderId=m.Id;";Dictionary<string,(bool Enabled,int Priority,ProviderValidationState? Validation)> result=new(StringComparer.OrdinalIgnoreCase);await using SqliteDataReader r=await command.ExecuteReaderAsync(token).ConfigureAwait(false);while(await r.ReadAsync(token).ConfigureAwait(false))result[r.GetString(0)]=(r.GetInt32(1)!=0,r.GetInt32(2),r.IsDBNull(3)?null:(ProviderValidationState)r.GetInt32(3));return result;
    }
    private static bool Supports(ArtworkProviderCapabilities capabilities,ArtworkKind type)=>type switch{ArtworkKind.CoverPortrait or ArtworkKind.BoxFront=>capabilities.HasFlag(ArtworkProviderCapabilities.CoverArtwork),ArtworkKind.GridWide=>capabilities.HasFlag(ArtworkProviderCapabilities.WideArtwork),ArtworkKind.HeroBackground=>capabilities.HasFlag(ArtworkProviderCapabilities.HeroArtwork),ArtworkKind.Logo=>capabilities.HasFlag(ArtworkProviderCapabilities.LogoArtwork),ArtworkKind.Icon=>capabilities.HasFlag(ArtworkProviderCapabilities.IconArtwork),ArtworkKind.Screenshot=>capabilities.HasFlag(ArtworkProviderCapabilities.ScreenshotArtwork),_=>false};
}

public sealed class ArtworkProviderConnectionTester(ISqliteConnectionFactory connections,IEnumerable<IArtworkProvider> providers) : IArtworkProviderConnectionTester
{
    private readonly IArtworkProvider[] _providers=providers.ToArray();
    public async Task<ArtworkProviderConnectionTestResult> TestAsync(string providerId,CancellationToken cancellationToken=default)
    {
        IArtworkProvider provider=_providers.FirstOrDefault(value=>string.Equals(value.Id,providerId,StringComparison.OrdinalIgnoreCase))??throw new KeyNotFoundException("The artwork provider was not found.");ArtworkProviderStatus status=provider is IArtworkProviderConnectionProbe probe?await probe.TestConnectionAsync(cancellationToken).ConfigureAwait(false):await provider.GetStatusAsync(cancellationToken).ConfigureAwait(false);ProviderValidationState validation=status.Available&&status.Authenticated?ProviderValidationState.Valid:status.FailureCode is "credential_missing" or "authentication_failed"?ProviderValidationState.Invalid:ProviderValidationState.Unavailable;DateTimeOffset now=DateTimeOffset.UtcNow;
        await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="UPDATE ProviderCredentialReferences SET ValidationState=$state,LastValidatedAtUtc=$now,UpdatedAtUtc=$now WHERE ProviderId=$provider; UPDATE MetadataProviders SET Enabled=CASE WHEN $valid=1 THEN 1 ELSE Enabled END,LastSuccessfulRequestAtUtc=CASE WHEN $valid=1 THEN $now ELSE LastSuccessfulRequestAtUtc END,LastFailureAtUtc=CASE WHEN $valid=0 THEN $now ELSE LastFailureAtUtc END,LastFailureCode=CASE WHEN $valid=1 THEN NULL ELSE $failure END,UpdatedAtUtc=$now WHERE Id=$provider;";Add(command,"$state",(int)validation);Add(command,"$valid",validation==ProviderValidationState.Valid?1:0);Add(command,"$failure",status.FailureCode);Add(command,"$now",now.ToString("O",CultureInfo.InvariantCulture));Add(command,"$provider",provider.Id);await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);string message=validation==ProviderValidationState.Valid?"Provider connection verified and enabled.":status.Message??"The provider connection could not be verified.";return new(provider.Id,validation,message,status.RemainingQuota,status.QuotaResetsAtUtc);
    }
    public async Task SetEnabledAsync(string providerId,bool enabled,CancellationToken cancellationToken=default){await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="UPDATE MetadataProviders SET Enabled=$enabled,UpdatedAtUtc=$now WHERE Id=$provider;";Add(command,"$enabled",enabled?1:0);Add(command,"$now",DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture));Add(command,"$provider",providerId);if(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)==0)throw new KeyNotFoundException("The artwork provider was not found.");}
    private static void Add(SqliteCommand command,string name,object? value)=>command.Parameters.AddWithValue(name,value??DBNull.Value);
}
