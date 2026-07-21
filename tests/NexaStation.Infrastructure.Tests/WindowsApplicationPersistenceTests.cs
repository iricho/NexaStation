using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class WindowsApplicationPersistenceTests
{
    [Fact]
    public async Task PhaseFourSchemaAndSeedsExist()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();Assert.Equal(3,await db.Scalar("SELECT COUNT(*) FROM Providers WHERE Id LIKE 'windows-%'"));Assert.Equal(13,await db.Scalar("SELECT COUNT(*) FROM ApplicationCategories"));Assert.Equal(3,await db.Scalar("SELECT COUNT(*) FROM DesktopControlProfiles"));
    }

    [Fact]
    public async Task ImportPersistsSourceLaunchCategoryAndTracking()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);WindowsApplicationPreviewCandidate preview=Preview(Candidate("one"));WindowsApplicationImportResult result=await persistence.ImportAsync([new(preview,WindowsImportAction.Import,CategoryId:"utilities")]);Assert.Equal(1,result.Added);Assert.Equal(1,await db.Scalar("SELECT COUNT(*) FROM WindowsApplicationSources"));Assert.Equal(1,await db.Scalar("SELECT COUNT(*) FROM ApplicationLaunchTracking"));Assert.Equal(1,await db.Scalar("SELECT COUNT(*) FROM LibraryItemApplicationCategories WHERE CategoryId='utilities' AND IsPrimary=1"));
    }

    [Fact]
    public async Task ExactProviderIdentityUpdatesInsteadOfDuplicating()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);WindowsApplicationPreviewCandidate preview=Preview(Candidate("same"));await persistence.ImportAsync([new(preview,WindowsImportAction.Import)]);WindowsApplicationImportResult second=await persistence.ImportAsync([new(preview with{Candidate=preview.Candidate with{Description="updated"}},WindowsImportAction.Import)]);Assert.Equal(1,second.Updated);Assert.Equal(1,await db.Scalar("SELECT COUNT(*) FROM LibraryItems"));
    }

    [Fact]
    public async Task AlternateTargetLinksWithoutCreatingSecondItem()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);WindowsApplicationPreviewCandidate first=Preview(Candidate("first",target:"C:\\Windows\\notepad.exe"));await persistence.ImportAsync([new(first,WindowsImportAction.Import)]);string item=(await persistence.GetSourcesAsync())[0].LibraryItemId;WindowsApplicationPreviewCandidate second=Preview(Candidate("second",target:"C:\\Windows\\notepad.exe"))with{ExistingLibraryItemId=item,Status=WindowsImportStatus.PossibleDuplicate};WindowsApplicationImportResult result=await persistence.ImportAsync([new(second,WindowsImportAction.AddAlternateLaunchTarget,item)]);Assert.Equal(1,result.AlternateTargets);Assert.Equal(1,await db.Scalar("SELECT COUNT(*) FROM LibraryItems"));Assert.Equal(2,await db.Scalar("SELECT COUNT(*) FROM LaunchTargets"));
    }

    [Fact]
    public async Task RefreshMarksMissingAndDisablesProviderManagedTarget()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);await persistence.ImportAsync([new(Preview(Candidate("gone")),WindowsImportAction.Import)]);await persistence.ApplyRefreshAsync(new("windows-start-menu",DateTime.UtcNow,DateTime.UtcNow,[],[],[]));Assert.Equal(0,await db.Scalar("SELECT Installed FROM WindowsApplicationSources LIMIT 1"));Assert.Equal(0,await db.Scalar("SELECT IsEnabled FROM LaunchTargets LIMIT 1"));
    }

    [Fact]
    public async Task CancelledRefreshNeverMarksEntriesMissing()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);await persistence.ImportAsync([new(Preview(Candidate("keep")),WindowsImportAction.Import)]);await persistence.ApplyRefreshAsync(new("windows-start-menu",DateTime.UtcNow,DateTime.UtcNow,[],[],[],false,true));Assert.Equal(1,await db.Scalar("SELECT Installed FROM WindowsApplicationSources LIMIT 1"));
    }

    [Fact]
    public async Task UserCategorySurvivesAutomaticCategoryUpdate()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);await persistence.ImportAsync([new(Preview(Candidate("category")),WindowsImportAction.Import)]);string item=(await persistence.GetSourcesAsync())[0].LibraryItemId;await persistence.SetCategoriesAsync(item,[new(item,"development",true,ApplicationCategorySource.User)]);await persistence.SetCategoriesAsync(item,[new(item,"utilities",true,ApplicationCategorySource.Automatic)]);Assert.Equal("Development",(await db.Library.GetByIdAsync(item))!.PrimaryCategoryName);
    }

    [Fact]
    public async Task DesktopAssignmentHydratesAndCanBeRemoved()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);await persistence.ImportAsync([new(Preview(Candidate("control")),WindowsImportAction.Import)]);string item=(await persistence.GetSourcesAsync())[0].LibraryItemId;await persistence.SetDesktopControlAssignmentAsync(new(item,"basic-mouse",true),item);Assert.True((await db.Library.GetByIdAsync(item))!.DesktopControlEnabled);Assert.True((await persistence.ResolveForItemAsync(item)).EnableOnLaunch);await persistence.SetDesktopControlAssignmentAsync(null,item);Assert.False((await persistence.ResolveForItemAsync(item)).EnableOnLaunch);
    }

    [Fact]
    public async Task FiveHundredCandidatePreviewMeetsBudget()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteWindowsApplicationPersistence persistence=new(db.Factory);WindowsApplicationCandidate[] candidates=Enumerable.Range(0,500).Select(index=>Candidate(index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();Stopwatch timer=Stopwatch.StartNew();WindowsApplicationImportPreview preview=await persistence.CreatePreviewAsync(candidates,new FixedClassifier());timer.Stop();Assert.Equal(500,preview.Candidates.Count);Assert.True(timer.Elapsed<TimeSpan.FromSeconds(5),$"Preview took {timer.Elapsed}.");
    }

    private static WindowsApplicationCandidate Candidate(string id,string? target=null)=>new(){ProviderId="windows-start-menu",ProviderItemId=id,DisplayName="App "+id,SourceType=WindowsApplicationSourceType.PackagedApplication,LaunchMethod=WindowsLaunchMethod.AppUserModelId,AppUserModelId="Test.Family!"+id,TargetPath=target,DiscoveryConfidence=ApplicationDiscoveryConfidence.High};
    private static WindowsApplicationPreviewCandidate Preview(WindowsApplicationCandidate candidate)=>new(candidate,new(WindowsCandidateKind.Application,"Utilities",ApplicationDiscoveryConfidence.High,true,"test"),WindowsImportStatus.New,true);
    private sealed class FixedClassifier:IWindowsApplicationClassifier{public WindowsApplicationClassification Classify(WindowsApplicationCandidate candidate)=>new(WindowsCandidateKind.Application,"Utilities",ApplicationDiscoveryConfidence.High,true,"test");}
}
