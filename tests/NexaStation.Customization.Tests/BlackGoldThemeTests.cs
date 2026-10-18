using NexaStation.Customization.Layouts;
using NexaStation.Customization.Models;
using NexaStation.Customization.Themes;
using Xunit;

namespace NexaStation.Customization.Tests;

public sealed class BlackGoldThemeTests
{
    [Fact]
    public async Task ColorSchemeSurvivesLayoutChangesAndReload()
    {
        MemorySettings settings=new();
        MemoryCustomizationStore store=new();
        ThemeService themes=new(store,settings);
        InterfaceLayoutService layouts=new(settings);
        await themes.ApplyAsync("black-gold");
        foreach(string layout in new[]{InterfaceLayoutService.ModernId,InterfaceLayoutService.CrossbarId,InterfaceLayoutService.HandheldId})
        {
            await layouts.ApplyAsync(layout);
            ThemeService reloaded=new(store,settings);
            await reloaded.GetAvailableAsync();
            Assert.Equal("black-gold",reloaded.CurrentThemeId);
        }
    }

    [Fact]
    public async Task CustomGoldAccentsPersistAndCanceledPreviewRestoresThem()
    {
        MemorySettings settings=new();
        MemoryCustomizationStore store=new();
        ThemeService themes=new(store,settings);
        ThemeDefinition gold=(await themes.GetAvailableAsync()).Single(theme=>theme.Id=="black-gold");
        await themes.SaveCustomAsync(gold with {Id="custom-black-gold",ThemeType=ThemeType.Custom,AccentColor="#F2CC75",SecondaryAccentColor="#C18E3D"});
        await themes.ApplyAsync("custom-black-gold");
        ThemeService reloaded=new(store,settings);
        await reloaded.GetAvailableAsync();
        ThemeDefinition? applied=null;
        reloaded.ThemeChanged+=(_,e)=>applied=e.Theme;
        await reloaded.PreviewAsync("light-room");
        await reloaded.CancelPreviewAsync();
        Assert.Equal("custom-black-gold",applied?.Id);
        Assert.Equal("#F2CC75",applied?.AccentColor);
        Assert.Equal("#C18E3D",applied?.SecondaryAccentColor);
    }
}
