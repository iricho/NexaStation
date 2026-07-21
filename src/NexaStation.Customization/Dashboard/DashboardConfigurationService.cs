using NexaStation.Customization.Layouts;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Dashboard;

public sealed class DashboardConfigurationService(ICustomizationStore store):IDashboardConfigurationService
{
    public async Task<DashboardConfiguration> GetOrCreateAsync(string layoutId,CancellationToken cancellationToken=default)
    {
        DashboardConfiguration? current=await store.GetDashboardAsync(layoutId,cancellationToken);if(current is not null)return current;
        DateTime now=DateTime.UtcNow;current=new(Guid.NewGuid().ToString("N"),layoutId,"Default",true,layoutId==InterfaceLayoutService.ModernId,DashboardCardDensity.Balanced,18,true,true,now,now);await store.SaveDashboardAsync(current,cancellationToken);await store.ReplaceRowsAsync(current.Id,Defaults(current,now),cancellationToken);return current;
    }
    public Task<IReadOnlyList<DashboardRowConfiguration>> GetRowsAsync(string dashboardId,CancellationToken cancellationToken=default)=>store.GetRowsAsync(dashboardId,cancellationToken);
    public async Task SaveRowsAsync(string dashboardId,IReadOnlyList<DashboardRowConfiguration> rows,CancellationToken cancellationToken=default)
    {
        if(rows.Count==0||rows.All(x=>!x.Enabled))throw new InvalidOperationException("At least one navigable dashboard row must remain enabled.");
        DashboardRowConfiguration[] normalized=rows.OrderBy(x=>x.SortOrder).Select((x,index)=>x with{SortOrder=index,MaximumItems=Math.Clamp(x.MaximumItems,1,100),UpdatedAtUtc=DateTime.UtcNow}).ToArray();await store.ReplaceRowsAsync(dashboardId,normalized,cancellationToken);
    }
    public async Task MoveRowAsync(string dashboardId,string rowId,int offset,CancellationToken cancellationToken=default){List<DashboardRowConfiguration> rows=(await store.GetRowsAsync(dashboardId,cancellationToken)).OrderBy(x=>x.SortOrder).ToList();int from=rows.FindIndex(x=>x.Id==rowId);if(from<0)throw new KeyNotFoundException("Dashboard row was not found.");int target=Math.Clamp(from+offset,0,rows.Count-1);if(target==from)return;DashboardRowConfiguration row=rows[from];rows.RemoveAt(from);rows.Insert(target,row);rows=rows.Select((value,index)=>value with{SortOrder=index}).ToList();await SaveRowsAsync(dashboardId,rows,cancellationToken);}
    public async Task ResetAsync(string layoutId,CancellationToken cancellationToken=default){DashboardConfiguration current=await GetOrCreateAsync(layoutId,cancellationToken);DateTime now=DateTime.UtcNow;await store.ReplaceRowsAsync(current.Id,Defaults(current,now),cancellationToken);}
    private static DashboardRowConfiguration[] Defaults(DashboardConfiguration dashboard,DateTime now)
    {
        (string Key,string Name,DashboardRowType Type,CardStyle Style,int Max)[] rows=dashboard.LayoutId switch
        {
            InterfaceLayoutService.CrossbarId=>[("pinned","Quick Access",DashboardRowType.Pinned,CardStyle.Icon,10),("continue","Continue Playing",DashboardRowType.Recent,CardStyle.List,12),("favorites","Favorites",DashboardRowType.BuiltIn,CardStyle.List,16),("all","All Content",DashboardRowType.BuiltIn,CardStyle.List,30)],
            InterfaceLayoutService.HandheldId=>[("pinned","Favorites Strip",DashboardRowType.Pinned,CardStyle.Compact,8),("continue","Continue",DashboardRowType.Recent,CardStyle.Compact,10),("library","Library",DashboardRowType.BuiltIn,CardStyle.Square,18)],
            _=>[("pinned","Pinned",DashboardRowType.Pinned,CardStyle.Wide,12),("continue","Continue / Recent Activity",DashboardRowType.Recent,CardStyle.Wide,12),("apps","Recently Used Apps",DashboardRowType.Recent,CardStyle.Square,12),("favorites","Favorites",DashboardRowType.BuiltIn,CardStyle.Wide,12),("added","Recently Added",DashboardRowType.BuiltIn,CardStyle.VerticalCover,12),("games","Games",DashboardRowType.BuiltIn,CardStyle.VerticalCover,12),("media","Media",DashboardRowType.Media,CardStyle.Wide,12)]
        };
        return rows.Select((x,index)=>new DashboardRowConfiguration(Guid.NewGuid().ToString("N"),dashboard.Id,x.Key,x.Name,x.Type,null,index,true,x.Max,x.Style,CardSize.Medium,true,false,true,false,now,now)).ToArray();
    }
}
