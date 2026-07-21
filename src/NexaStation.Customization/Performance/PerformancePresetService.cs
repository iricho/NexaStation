using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Performance;

public sealed class PerformancePresetService(ISettingsService settings):IPerformancePresetService
{
    public Task<PerformanceSettings> GetAsync(CancellationToken cancellationToken=default)=>settings.GetAsync("Interface.Performance",Preset(VisualPerformancePreset.Balanced,false),cancellationToken);
    public async Task<PerformanceSettings> ApplyAsync(VisualPerformancePreset preset,bool reducedMotion=false,CancellationToken cancellationToken=default){if(preset==VisualPerformancePreset.Custom)throw new ArgumentException("Use SaveCustomAsync for custom settings.",nameof(preset));PerformanceSettings value=Preset(preset,reducedMotion);await settings.SetAsync("Interface.Performance",value,cancellationToken);return value;}
    public async Task<PerformanceSettings> SaveCustomAsync(PerformanceSettings settingsValue,CancellationToken cancellationToken=default){PerformanceSettings saved=settingsValue with{Preset=VisualPerformancePreset.Custom,AnimationIntensity=settingsValue.ReducedMotion?AnimationIntensity.None:settingsValue.AnimationIntensity,BackgroundTransitions=!settingsValue.ReducedMotion&&settingsValue.BackgroundTransitions};await settings.SetAsync("Interface.Performance",saved,cancellationToken);return saved;}
    private static PerformanceSettings Preset(VisualPerformancePreset preset,bool reducedMotion)=>preset switch
    {
        VisualPerformancePreset.Minimal=>new(preset,false,0,AnimationIntensity.None,false,55,32,512,true,true,true,12,reducedMotion),
        VisualPerformancePreset.Cinematic=>new(preset,true,2,reducedMotion?AnimationIntensity.None:AnimationIntensity.High,!reducedMotion,100,192,4096,true,true,true,24,reducedMotion),
        _=>new(VisualPerformancePreset.Balanced,true,1,reducedMotion?AnimationIntensity.None:AnimationIntensity.Balanced,!reducedMotion,80,96,2048,true,true,true,18,reducedMotion)
    };
}
