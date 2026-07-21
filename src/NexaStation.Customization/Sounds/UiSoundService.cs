using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Sounds;

public sealed class NullUiSoundPlayer:IUiSoundPlayer{public Task PlayPackagedAsync(string assetKey,double volume,CancellationToken cancellationToken=default)=>Task.CompletedTask;}

public sealed class UiSoundService(ISettingsService settings,IUiSoundPlayer player):IUiSoundService
{
    private static readonly UiSoundPack[] Packs=[Pack("nexa-soft","Nexa Soft","Soft original interface cues.","Assets/Sounds/NexaSoft","Original NexaStation project assets; CC0-style project grant."),Pack("minimal-click","Minimal Click","Short restrained click cues.","Assets/Sounds/MinimalClick","Original NexaStation project assets; CC0-style project grant."),Pack("silent","Silent","Disables every interface sound.",null,"No audio assets."),Pack("arcade-light","Arcade Light","Bright original lightweight cues.","Assets/Sounds/ArcadeLight","Original NexaStation project assets; CC0-style project grant.")];
    private DateTime _lastNavigation=DateTime.MinValue;private string _current="nexa-soft";private double _volume=.6;
    public Task<IReadOnlyList<UiSoundPack>> GetPacksAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<UiSoundPack>>(Packs);
    public async Task ApplyPackAsync(string soundPackId,CancellationToken cancellationToken=default){if(Packs.All(x=>x.Id!=soundPackId))soundPackId="silent";_current=soundPackId;await settings.SetAsync("Interface.SoundPackId",soundPackId,cancellationToken);}
    public async Task SetVolumeAsync(double volume,CancellationToken cancellationToken=default){_volume=Math.Clamp(volume,0,1);await settings.SetAsync("Interface.SoundVolume",_volume,cancellationToken);}
    public async Task PlayAsync(UiSoundEvent soundEvent,CancellationToken cancellationToken=default){_current=await settings.GetAsync("Interface.SoundPackId",_current,cancellationToken);_volume=await settings.GetAsync("Interface.SoundVolume",_volume,cancellationToken);if(_current=="silent"||_volume<=0)return;if(soundEvent==UiSoundEvent.Navigate&&DateTime.UtcNow-_lastNavigation<TimeSpan.FromMilliseconds(70))return;if(soundEvent==UiSoundEvent.Navigate)_lastNavigation=DateTime.UtcNow;UiSoundPack pack=Packs.FirstOrDefault(x=>x.Id==_current)??Packs.First(x=>x.Id=="silent");if(pack.EventMappings.TryGetValue(soundEvent,out string? asset)&&asset is not null)try{await player.PlayPackagedAsync(asset,_volume,cancellationToken);}catch(Exception ex) when(ex is FileNotFoundException or InvalidOperationException){}}
    private static UiSoundPack Pack(string id,string name,string description,string? root,string license)=>new(id,name,description,Enum.GetValues<UiSoundEvent>().ToDictionary(x=>x,x=>root is null?null:$"{root}/{x}.wav"),.6,true,true,license);
}
