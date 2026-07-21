using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;
using Windows.Networking.Connectivity;

namespace NexaStation.SystemIntegration.Network;

public sealed class WindowsNetworkStatusService:INetworkStatusService,IDisposable
{
    public WindowsNetworkStatusService()=>NetworkInformation.NetworkStatusChanged+=OnChanged;
    public event EventHandler<NetworkStatusChangedEventArgs>? StatusChanged;
    public Task<NetworkStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();try{ConnectionProfile? profile=NetworkInformation.GetInternetConnectionProfile();if(profile is null)return Task.FromResult(new NetworkStatusSnapshot(false,"None",null,null,null,null,DateTime.UtcNow));ConnectionCost cost=profile.GetConnectionCost();string? type=profile.IsWlanConnectionProfile?"Wi-Fi":profile.IsWwanConnectionProfile?"Cellular":"Ethernet";return Task.FromResult(new NetworkStatusSnapshot(true,profile.GetNetworkConnectivityLevel().ToString(),profile.ProfileName,type,cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable,cost.Roaming,DateTime.UtcNow));}catch(Exception ex){return Task.FromResult(new NetworkStatusSnapshot(false,"Unknown",null,null,null,null,DateTime.UtcNow,ex.Message));}}
    public void Dispose()=>NetworkInformation.NetworkStatusChanged-=OnChanged;
    private async void OnChanged(object sender){StatusChanged?.Invoke(this,new(await GetSnapshotAsync()));}
}
