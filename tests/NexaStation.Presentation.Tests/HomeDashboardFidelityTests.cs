using NexaStation.Domain;
using NexaStation.Presentation.Dashboard;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class HomeDashboardFidelityTests : IDisposable
{
    private readonly string _folder=Path.Combine(Path.GetTempPath(),"NexaStation.HomeFidelity."+Guid.NewGuid().ToString("N"));
    public HomeDashboardFidelityTests()=>Directory.CreateDirectory(_folder);

    [Theory]
    [InlineData(1280,720,HomeDashboardDensity.Compact,true,3,4)]
    [InlineData(1920,1080,HomeDashboardDensity.Standard,false,5,5)]
    [InlineData(2560,1440,HomeDashboardDensity.Large,false,5,5)]
    public void DensityPolicyUsesWidthAndHeight(double width,double height,HomeDashboardDensity expected,bool scrolls,int gameLimit,int applicationLimit)
    {
        HomeDashboardLayout result=new HomeDashboardDensityPolicy().Resolve(width,height);
        Assert.Equal(expected,result.Density);Assert.Equal(scrolls,result.UsesVerticalScrolling);Assert.Equal(gameLimit,result.GameLimit);Assert.Equal(applicationLimit,result.ApplicationLimit);Assert.Equal(2,result.RecentActivityLimit);
    }

    [Fact]
    public void WideButShortViewportUsesCompactScrollableLayout()
    {
        HomeDashboardLayout result=new HomeDashboardDensityPolicy().Resolve(2560,800);
        Assert.Equal(HomeDashboardDensity.Compact,result.Density);Assert.True(result.UsesVerticalScrolling);
    }

    [Fact]
    public void InvalidViewportIsRejected()
    {
        HomeDashboardDensityPolicy policy=new();
        Assert.Throws<ArgumentOutOfRangeException>(()=>policy.Resolve(double.NaN,1080));
        Assert.Throws<ArgumentOutOfRangeException>(()=>policy.Resolve(1920,-1));
    }

    [Theory]
    [InlineData("ghala", "GHA")]
    [InlineData("John.Doe", "JOH")]
    [InlineData("  ", "USR")]
    [InlineData("أحمد", "أحم")]
    public void UserBadgeUsesThreeSafeCharacters(string userName,string expected) =>
        Assert.Equal(expected,UserBadgeFormatter.CreateInitials(userName));

    [Fact]
    public void HeroCarouselIsDeterministicTruthfulAndCapped()
    {
        LibraryItem[] items=Enumerable.Range(0,7).Select(index=>Item($"game-{index}",DateTime.UtcNow.AddHours(-index),index==2)).ToArray();
        HeroSelectionService service=new(new ArtworkPresentationService());
        HeroSelectionRequest request=new(items,[items[4].Id,items[4].Id],items[5].Id);
        IReadOnlyList<HeroSelectionResult> first=service.SelectHeroes(request);IReadOnlyList<HeroSelectionResult> second=service.SelectHeroes(request);
        Assert.Equal(5,first.Count);Assert.Equal(items[5].Id,first[0].Item?.Id);Assert.Equal(first.Select(value=>value.Item?.Id),second.Select(value=>value.Item?.Id));Assert.All(first,value=>Assert.True(value.Artwork.Available));Assert.Equal(first.Count,first.Select(value=>value.Item?.Id).Distinct().Count());
    }

    [Fact]
    public void HeroCarouselUsesOneTruthfulFallbackWhenNothingIsEligible()
    {
        LibraryItem hidden=Item("hidden",DateTime.UtcNow,false) with{Hidden=true};
        HeroSelectionResult result=Assert.Single(new HeroSelectionService(new ArtworkPresentationService()).SelectHeroes(new([hidden],[])));
        Assert.True(result.IsFallback);Assert.Null(result.Item);
    }

    private LibraryItem Item(string id,DateTime launched,bool favorite)
    {
        string path=Path.Combine(_folder,id+".png");File.WriteAllBytes(path,[1,2,3,4]);LaunchTarget target=new(LaunchType.Uri,Uri:new Uri("https://example.invalid/"+id)){Id=id+"-target",LibraryItemId=id};
        return new(){Id=id,Title=id,SortTitle=id,ContentType=ContentType.Game,Favorite=favorite,AddedAtUtc=launched.AddDays(-1),UpdatedAtUtc=DateTime.UtcNow,LastLaunchedAtUtc=launched,BackgroundAsset=new(id+"-background",id,AssetType.Background,path,1600,700,4,DateTime.UtcNow),DefaultLaunchTargetId=target.Id,LaunchTargets=[target]};
    }

    public void Dispose(){try{Directory.Delete(_folder,true);}catch(IOException){}}
}
