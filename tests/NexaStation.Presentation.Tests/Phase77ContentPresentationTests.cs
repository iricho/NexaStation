using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Presentation.CardPresentation;
using NexaStation.Presentation.Collections;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class Phase77ContentPresentationTests
{
    private readonly ContentCardPresentationResolver _resolver=new();

    [Theory]
    [InlineData(ContentType.Game,NavigationSection.Games,false,false,false,false,false,ContentCardPresentationType.GameWide)]
    [InlineData(ContentType.Application,NavigationSection.Apps,false,false,false,false,false,ContentCardPresentationType.ApplicationSquare)]
    [InlineData(ContentType.Application,NavigationSection.Gallery,true,false,false,false,false,ContentCardPresentationType.GalleryThumbnail)]
    [InlineData(ContentType.Media,NavigationSection.Gallery,true,true,false,false,false,ContentCardPresentationType.VideoThumbnail)]
    [InlineData(ContentType.Application,NavigationSection.Home,false,false,true,false,false,ContentCardPresentationType.StreamingService)]
    [InlineData(ContentType.Application,NavigationSection.Collections,false,false,false,true,false,ContentCardPresentationType.CollectionMosaic)]
    [InlineData(ContentType.Application,NavigationSection.Apps,false,false,false,false,true,ContentCardPresentationType.WebApplication)]
    [InlineData(ContentType.Media,NavigationSection.Media,false,false,false,false,false,ContentCardPresentationType.ApplicationSquare)]
    public void ResolvesContentSpecificType(ContentType content,NavigationSection page,bool gallery,bool video,bool streaming,bool collection,bool web,ContentCardPresentationType expected)
    {
        ContentCardPresentation result=_resolver.Resolve(new(content,page,IsGalleryItem:gallery,IsVideo:video,IsStreamingService:streaming,IsCollection:collection,IsWebApplication:web));Assert.Equal(expected,result.PresentationType);
    }

    [Fact] public void UserOverrideWins()=>Assert.Equal(ContentCardPresentationType.CompactList,_resolver.Resolve(new(ContentType.Game,NavigationSection.Games,UserOverride:ContentCardPresentationType.CompactList)).PresentationType);
    [Fact] public void MissingContentIsExplicit()=>Assert.Equal(ContentCardPresentationType.MissingContent,_resolver.Resolve(new(ContentType.Application,NavigationSection.Library,IsAvailable:false)).PresentationType);
    [Fact] public void GameCoverModeDoesNotStretchApplications(){Assert.Equal(ContentCardPresentationType.GameCover,_resolver.Resolve(new(ContentType.Game,NavigationSection.Library,SelectedCardStyle:"GameCover")).PresentationType);Assert.Equal(ContentCardPresentationType.ApplicationSquare,_resolver.Resolve(new(ContentType.Application,NavigationSection.Library,SelectedCardStyle:"GameCover")).PresentationType);}
    [Fact] public void CompactViewportUses720pAppSize(){ContentCardPresentation value=_resolver.Resolve(new(ContentType.Application,NavigationSection.Apps,ViewportWidth:1100));Assert.InRange(value.Width,120,145);Assert.Equal(value.Width,value.Height);}
    [Fact] public void StandardViewportUses1080pAppSize(){ContentCardPresentation value=_resolver.Resolve(new(ContentType.Application,NavigationSection.Apps,ViewportWidth:1920));Assert.InRange(value.Width,150,180);Assert.Equal(value.Width,value.Height);}
    [Fact] public void CompactListIsHorizontal()=>Assert.True(_resolver.Resolve(new(ContentType.Application,NavigationSection.Library,SelectedCardStyle:"CompactList")).Width>_resolver.Resolve(new(ContentType.Application,NavigationSection.Library,SelectedCardStyle:"CompactList")).Height);
    [Fact] public void MixedGridPresentationsWrap(){Assert.True(_resolver.Resolve(new(ContentType.Game,NavigationSection.Library)).Wraps);Assert.True(_resolver.Resolve(new(ContentType.Application,NavigationSection.Library)).Wraps);}
}

public sealed class Phase77CollectionMosaicTests:IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"NexaStation-MosaicTests-"+Guid.NewGuid().ToString("N"));
    [Fact] public async Task EmptyCollectionUsesFallback(){CollectionMosaicResult result=await new CollectionMosaicService(_root).ResolveAsync(new("empty",[],DateTime.UtcNow));Assert.True(result.IsFallback);Assert.Null(result.CachePath);}
    [Fact] public async Task MosaicUsesAtMostFourSources(){string[] paths=Enumerable.Range(0,6).Select(CreatePng).ToArray();CollectionMosaicResult result=await new CollectionMosaicService(_root).ResolveAsync(new("bounded",paths,DateTime.UtcNow,192));Assert.Equal(4,result.SourceCount);Assert.True(File.Exists(result.CachePath));}
    [Fact] public async Task MosaicCacheIsReused(){string path=CreatePng(1);CollectionMosaicService service=new(_root);CollectionMosaicRequest request=new("cache",[path],DateTime.UnixEpoch,192);CollectionMosaicResult first=await service.ResolveAsync(request);DateTime written=File.GetLastWriteTimeUtc(first.CachePath!);CollectionMosaicResult second=await service.ResolveAsync(request);Assert.Equal(first.CachePath,second.CachePath);Assert.Equal(written,File.GetLastWriteTimeUtc(second.CachePath!));}
    [Fact] public async Task ContentChangeInvalidatesMosaic(){string path=CreatePng(2);CollectionMosaicService service=new(_root);CollectionMosaicResult first=await service.ResolveAsync(new("invalidate",[path],DateTime.UnixEpoch,192));CollectionMosaicResult second=await service.ResolveAsync(new("invalidate",[path],DateTime.UnixEpoch.AddSeconds(1),192));Assert.NotEqual(first.CacheKey,second.CacheKey);}
    private string CreatePng(int value){Directory.CreateDirectory(_root);string path=Path.Combine(_root,$"{value}-{Guid.NewGuid():N}.png");using System.Drawing.Bitmap bitmap=new(16,16);using System.Drawing.Graphics graphics=System.Drawing.Graphics.FromImage(bitmap);graphics.Clear(System.Drawing.Color.FromArgb(255,20+value,40+value,60+value));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);return path;}
    public void Dispose(){try{Directory.Delete(_root,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
