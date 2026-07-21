using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteLibraryRepository(ISqliteConnectionFactory factory) : ILibraryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<LibraryItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        List<string> predicates = [];
        if (!query.IncludeHidden) predicates.Add("Hidden=0");
        if (query.FavoriteOnly) predicates.Add("Favorite=1");
        if (query.ContentType is not null) { predicates.Add("ContentType=$type"); command.Parameters.AddWithValue("$type", (int)query.ContentType); }
        if (!string.IsNullOrWhiteSpace(query.SearchText)) { predicates.Add("(Title LIKE $search ESCAPE '\\' OR Subtitle LIKE $search ESCAPE '\\' OR Description LIKE $search ESCAPE '\\')"); command.Parameters.AddWithValue("$search", $"%{EscapeLike(query.SearchText.Trim())}%"); }
        if (query.InstalledOnly) predicates.Add("EXISTS(SELECT 1 FROM LaunchTargets t WHERE t.LibraryItemId=LibraryItems.Id AND ((t.ExecutablePath IS NOT NULL AND t.ExecutablePath<>'') OR (t.ShortcutPath IS NOT NULL AND t.ShortcutPath<>'') OR t.Uri IS NOT NULL))");
        string order = query.SortMode switch { LibrarySortMode.RecentlyAdded => "AddedAtUtc DESC", LibrarySortMode.RecentlyLaunched => "LastLaunchedAtUtc IS NULL, LastLaunchedAtUtc DESC", LibrarySortMode.MostPlayed => "TotalPlaytimeSeconds DESC, SortTitle", _ => "SortTitle COLLATE NOCASE" };
        command.CommandText = $"SELECT * FROM LibraryItems{(predicates.Count == 0 ? "" : " WHERE " + string.Join(" AND ", predicates))} ORDER BY {order} LIMIT $max;";
        command.Parameters.AddWithValue("$max", Math.Clamp(query.MaximumResults, 1, 5000));
        List<LibraryItem> items = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadItem(reader));
        await reader.DisposeAsync().ConfigureAwait(false);
        for (int index = 0; index < items.Count; index++) items[index] = await HydrateAsync(connection, items[index], cancellationToken).ConfigureAwait(false);
        return items;
    }

    public async Task<LibraryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT * FROM LibraryItems WHERE Id=$id;"; command.Parameters.AddWithValue("$id", id);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        LibraryItem item = ReadItem(reader); await reader.DisposeAsync().ConfigureAwait(false);
        return await HydrateAsync(connection, item, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await WriteItemAsync(connection, transaction, item, insert: true, cancellationToken).ConfigureAwait(false);
        await ReplaceChildrenAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await WriteItemAsync(connection, transaction, item, insert: false, cancellationToken).ConfigureAwait(false);
        await ReplaceChildrenAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => ExecuteItemAsync("DELETE FROM LibraryItems WHERE Id=$id;", id, null, cancellationToken);
    public Task SetFavoriteAsync(string id, bool favorite, CancellationToken cancellationToken = default) => ExecuteItemAsync("UPDATE LibraryItems SET Favorite=$value,UpdatedAtUtc=$at WHERE Id=$id;", id, favorite, cancellationToken);
    public Task SetHiddenAsync(string id, bool hidden, CancellationToken cancellationToken = default) => ExecuteItemAsync("UPDATE LibraryItems SET Hidden=$value,UpdatedAtUtc=$at WHERE Id=$id;", id, hidden, cancellationToken);

    public async Task<LibraryItem?> FindDuplicateAsync(LaunchTarget target, CancellationToken cancellationToken = default)
    {
        string? value = target.LaunchType switch { LaunchType.Executable => target.ExecutablePath, LaunchType.WindowsShortcut => target.ShortcutPath, _ => target.Uri?.AbsoluteUri };
        if (string.IsNullOrWhiteSpace(value)) return null;
        string column = target.LaunchType switch { LaunchType.Executable => "ExecutablePath", LaunchType.WindowsShortcut => "ShortcutPath", _ => "Uri" };
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = $"SELECT LibraryItemId FROM LaunchTargets WHERE {column}=$value COLLATE NOCASE LIMIT 1;"; command.Parameters.AddWithValue("$value", Normalize(value, target.LaunchType));
        string? id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return id is null ? null : await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteItemAsync(string sql, string id, bool? value, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql; command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O")); if (value is not null) command.Parameters.AddWithValue("$value", value.Value ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteItemAsync(SqliteConnection connection, SqliteTransaction tx, LibraryItem item, bool insert, CancellationToken ct)
    {
        await using SqliteCommand c = connection.CreateCommand(); c.Transaction = tx;
        c.CommandText = insert
            ? "INSERT INTO LibraryItems(Id,Title,SortTitle,Subtitle,Description,ContentType,Favorite,Hidden,AddedAtUtc,UpdatedAtUtc,LastLaunchedAtUtc,TotalPlaytimeSeconds,DefaultLaunchTargetId,CoverAssetId,BackgroundAssetId,ApplicationCardStyle) VALUES($id,$title,$sort,$subtitle,$description,$type,$favorite,$hidden,$added,$updated,$launched,$playtime,$target,$cover,$background,$card);"
            : "UPDATE LibraryItems SET Title=$title,SortTitle=$sort,Subtitle=$subtitle,Description=$description,ContentType=$type,Favorite=$favorite,Hidden=$hidden,UpdatedAtUtc=$updated,LastLaunchedAtUtc=$launched,TotalPlaytimeSeconds=$playtime,DefaultLaunchTargetId=$target,CoverAssetId=$cover,BackgroundAssetId=$background,ApplicationCardStyle=$card WHERE Id=$id;";
        Add(c,"$id",item.Id); Add(c,"$title",item.Title); Add(c,"$sort",item.SortTitle); Add(c,"$subtitle",item.Subtitle); Add(c,"$description",item.Description); Add(c,"$type",(int)item.ContentType); Add(c,"$favorite",item.Favorite?1:0); Add(c,"$hidden",item.Hidden?1:0); Add(c,"$added",item.AddedAtUtc.ToUniversalTime().ToString("O")); Add(c,"$updated",item.UpdatedAtUtc.ToUniversalTime().ToString("O")); Add(c,"$launched",item.LastLaunchedAtUtc?.ToUniversalTime().ToString("O")); Add(c,"$playtime",item.TotalPlaytimeSeconds); Add(c,"$target",item.DefaultLaunchTargetId); Add(c,"$cover",item.CoverAsset?.Id); Add(c,"$background",item.BackgroundAsset?.Id); Add(c,"$card",(int)item.CardStyle);
        await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task ReplaceChildrenAsync(SqliteConnection connection, SqliteTransaction tx, LibraryItem item, CancellationToken ct)
    {
        await using (SqliteCommand delete = connection.CreateCommand()) { delete.Transaction=tx; delete.CommandText="DELETE FROM LaunchTargets WHERE LibraryItemId=$id; DELETE FROM Assets WHERE LibraryItemId=$id;"; delete.Parameters.AddWithValue("$id",item.Id); await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
        foreach (LaunchTarget t in item.LaunchTargets) { await using SqliteCommand c=connection.CreateCommand(); c.Transaction=tx; c.CommandText="INSERT INTO LaunchTargets (Id,LibraryItemId,Name,LaunchType,ExecutablePath,ShortcutPath,WorkingDirectory,Arguments,Uri,ExpectedProcessName,HideFrontendWhileRunning,RestoreFrontendAfterExit,CreatedAtUtc,UpdatedAtUtc,ProviderId,IsEnabled,UserManaged,EmulatedGameSourceId,EmulatorInstallationId,LaunchProfileId,ArgumentListJson,EnvironmentOverridesJson,WindowsApplicationSourceId,WindowsLaunchMethod,AppUserModelId,ShellParsingPath,TrackingMode,ExpectedExecutablePath,ExpectedWindowClass,DetectionTimeoutSeconds,ExitGracePeriodSeconds) VALUES($id,$item,$name,$type,$exe,$shortcut,$working,$args,$uri,$process,$hide,$restore,$created,$updated,$provider,$enabled,$managed,$game,$emulator,$profile,$arglist,$environment,$windowsSource,$method,$aumid,$shell,$tracking,$expectedExe,$windowClass,$timeout,$grace);"; Add(c,"$id",t.Id);Add(c,"$item",item.Id);Add(c,"$name",t.Name);Add(c,"$type",(int)t.LaunchType);Add(c,"$exe",Normalize(t.ExecutablePath,t.LaunchType));Add(c,"$shortcut",Normalize(t.ShortcutPath,t.LaunchType));Add(c,"$working",NormalizePath(t.WorkingDirectory));Add(c,"$args",t.Arguments);Add(c,"$uri",t.Uri?.AbsoluteUri);Add(c,"$process",t.ExpectedProcessName);Add(c,"$hide",t.HideFrontendWhileRunning?1:0);Add(c,"$restore",t.RestoreFrontendAfterExit?1:0);Add(c,"$created",t.CreatedAtUtc.ToUniversalTime().ToString("O"));Add(c,"$updated",t.UpdatedAtUtc.ToUniversalTime().ToString("O"));Add(c,"$provider",t.ProviderId);Add(c,"$enabled",t.IsEnabled?1:0);Add(c,"$managed",t.UserManaged?1:0);Add(c,"$game",t.EmulatedGameSourceId);Add(c,"$emulator",t.EmulatorInstallationId);Add(c,"$profile",t.LaunchProfileId);Add(c,"$arglist",JsonSerializer.Serialize(t.ArgumentList,JsonOptions));Add(c,"$environment",JsonSerializer.Serialize(t.EnvironmentOverrides,JsonOptions));Add(c,"$windowsSource",t.WindowsApplicationSourceId);Add(c,"$method",t.WindowsLaunchMethod is null?null:(int)t.WindowsLaunchMethod);Add(c,"$aumid",t.AppUserModelId);Add(c,"$shell",t.ShellParsingPath);Add(c,"$tracking",(int)t.TrackingMode);Add(c,"$expectedExe",t.ExpectedExecutablePath);Add(c,"$windowClass",t.ExpectedWindowClass);Add(c,"$timeout",t.DetectionTimeoutSeconds);Add(c,"$grace",t.ExitGracePeriodSeconds);await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
        foreach (Asset a in new[]{item.CoverAsset,item.BackgroundAsset}.OfType<Asset>().DistinctBy(a=>a.Id)) { await using SqliteCommand c=connection.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO Assets VALUES($id,$item,$type,$path,$width,$height,$size,$added);";Add(c,"$id",a.Id);Add(c,"$item",item.Id);Add(c,"$type",(int)a.AssetType);Add(c,"$path",NormalizePath(a.LocalPath));Add(c,"$width",a.Width);Add(c,"$height",a.Height);Add(c,"$size",a.FileSize);Add(c,"$added",a.AddedAtUtc.ToUniversalTime().ToString("O"));await c.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
    }

    private static LibraryItem ReadItem(SqliteDataReader r) => new() { Id=r.GetString(r.GetOrdinal("Id")),Title=r.GetString(r.GetOrdinal("Title")),SortTitle=r.GetString(r.GetOrdinal("SortTitle")),Subtitle=Nullable(r,"Subtitle"),Description=Nullable(r,"Description"),ContentType=(ContentType)r.GetInt32(r.GetOrdinal("ContentType")),Favorite=r.GetInt32(r.GetOrdinal("Favorite"))!=0,Hidden=r.GetInt32(r.GetOrdinal("Hidden"))!=0,AddedAtUtc=DateTime.Parse(r.GetString(r.GetOrdinal("AddedAtUtc")),null,DateTimeStyles.RoundtripKind),UpdatedAtUtc=DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAtUtc")),null,DateTimeStyles.RoundtripKind),LastLaunchedAtUtc=NullableDate(r,"LastLaunchedAtUtc"),TotalPlaytimeSeconds=r.GetInt64(r.GetOrdinal("TotalPlaytimeSeconds")),DefaultLaunchTargetId=Nullable(r,"DefaultLaunchTargetId"),CardStyle=(ApplicationCardStyle)r.GetInt32(r.GetOrdinal("ApplicationCardStyle")) };

    private static async Task<LibraryItem> HydrateAsync(SqliteConnection connection, LibraryItem item, CancellationToken ct)
    {
        List<LaunchTarget> targets=[]; await using(SqliteCommand c=connection.CreateCommand()){c.CommandText="SELECT * FROM LaunchTargets WHERE LibraryItemId=$id ORDER BY CreatedAtUtc;";c.Parameters.AddWithValue("$id",item.Id);await using SqliteDataReader r=await c.ExecuteReaderAsync(ct).ConfigureAwait(false);while(await r.ReadAsync(ct).ConfigureAwait(false)){string? uri=Nullable(r,"Uri");targets.Add(new LaunchTarget((LaunchType)r.GetInt32(r.GetOrdinal("LaunchType")),Nullable(r,"ExecutablePath"),Nullable(r,"WorkingDirectory"),Nullable(r,"Arguments"),uri is null?null:new Uri(uri),Nullable(r,"ExpectedProcessName"),r.GetInt32(r.GetOrdinal("HideFrontendWhileRunning"))!=0,r.GetInt32(r.GetOrdinal("RestoreFrontendAfterExit"))!=0){Id=r.GetString(r.GetOrdinal("Id")),LibraryItemId=item.Id,Name=r.GetString(r.GetOrdinal("Name")),ShortcutPath=Nullable(r,"ShortcutPath"),ProviderId=Nullable(r,"ProviderId"),IsEnabled=r.GetInt32(r.GetOrdinal("IsEnabled"))!=0,UserManaged=r.GetInt32(r.GetOrdinal("UserManaged"))!=0,EmulatedGameSourceId=Nullable(r,"EmulatedGameSourceId"),EmulatorInstallationId=Nullable(r,"EmulatorInstallationId"),LaunchProfileId=Nullable(r,"LaunchProfileId"),ArgumentList=JsonList(Nullable(r,"ArgumentListJson")),EnvironmentOverrides=JsonDictionary(Nullable(r,"EnvironmentOverridesJson")),WindowsApplicationSourceId=Nullable(r,"WindowsApplicationSourceId"),WindowsLaunchMethod=NullableInt(r,"WindowsLaunchMethod") is int method?(WindowsLaunchMethod)method:null,AppUserModelId=Nullable(r,"AppUserModelId"),ShellParsingPath=Nullable(r,"ShellParsingPath"),TrackingMode=(ApplicationTrackingMode)r.GetInt32(r.GetOrdinal("TrackingMode")),ExpectedExecutablePath=Nullable(r,"ExpectedExecutablePath"),ExpectedWindowClass=Nullable(r,"ExpectedWindowClass"),DetectionTimeoutSeconds=r.GetInt32(r.GetOrdinal("DetectionTimeoutSeconds")),ExitGracePeriodSeconds=r.GetInt32(r.GetOrdinal("ExitGracePeriodSeconds")),CreatedAtUtc=DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAtUtc")),null,DateTimeStyles.RoundtripKind),UpdatedAtUtc=DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAtUtc")),null,DateTimeStyles.RoundtripKind)});}}
        Asset? cover=null,background=null; await using(SqliteCommand c=connection.CreateCommand()){c.CommandText="SELECT * FROM Assets WHERE LibraryItemId=$id;";c.Parameters.AddWithValue("$id",item.Id);await using SqliteDataReader r=await c.ExecuteReaderAsync(ct).ConfigureAwait(false);while(await r.ReadAsync(ct).ConfigureAwait(false)){Asset a=new(r.GetString(0),item.Id,(AssetType)r.GetInt32(2),r.GetString(3),r.IsDBNull(4)?null:r.GetInt32(4),r.IsDBNull(5)?null:r.GetInt32(5),r.IsDBNull(6)?null:r.GetInt64(6),DateTime.Parse(r.GetString(7),null,DateTimeStyles.RoundtripKind));if(a.AssetType is AssetType.Cover or AssetType.Icon)cover=a;if(a.AssetType==AssetType.Background)background=a;}}
        string? category=null;bool desktop=false;await using(SqliteCommand c=connection.CreateCommand()){c.CommandText="SELECT ac.Name FROM LibraryItemApplicationCategories lc JOIN ApplicationCategories ac ON ac.Id=lc.CategoryId WHERE lc.LibraryItemId=$id AND lc.IsPrimary=1 LIMIT 1;";c.Parameters.AddWithValue("$id",item.Id);category=await c.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;}await using(SqliteCommand c=connection.CreateCommand()){c.CommandText="SELECT EnableOnLaunch FROM LibraryItemDesktopControlProfiles WHERE LibraryItemId=$id;";c.Parameters.AddWithValue("$id",item.Id);object? value=await c.ExecuteScalarAsync(ct).ConfigureAwait(false);desktop=value is not null&&value is not DBNull&&Convert.ToInt32(value,CultureInfo.InvariantCulture)!=0;}
        return item with { LaunchTargets=targets,CoverAsset=cover,BackgroundAsset=background,PrimaryCategoryName=category,DesktopControlEnabled=desktop };
    }

    private static string EscapeLike(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("%","\\%",StringComparison.Ordinal).Replace("_","\\_",StringComparison.Ordinal);
    private static string? Normalize(string? value, LaunchType type)=>string.IsNullOrWhiteSpace(value)?null:type is LaunchType.Executable or LaunchType.WindowsShortcut?NormalizePath(value):value.Trim();
    private static string? NormalizePath(string? value)=>string.IsNullOrWhiteSpace(value)?null:Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
    private static void Add(SqliteCommand c,string name,object? value)=>c.Parameters.AddWithValue(name,value??DBNull.Value);
    private static string? Nullable(SqliteDataReader r,string name){int i=r.GetOrdinal(name);return r.IsDBNull(i)?null:r.GetString(i);}
    private static DateTime? NullableDate(SqliteDataReader r,string name){string? s=Nullable(r,name);return s is null?null:DateTime.Parse(s,null,DateTimeStyles.RoundtripKind);}
    private static int? NullableInt(SqliteDataReader r,string name){int i=r.GetOrdinal(name);return r.IsDBNull(i)?null:r.GetInt32(i);}
    private static string[] JsonList(string? value){try{return string.IsNullOrWhiteSpace(value)?[]:JsonSerializer.Deserialize<string[]>(value,JsonOptions)??[];}catch(JsonException){return [];}}
    private static Dictionary<string,string> JsonDictionary(string? value){try{return string.IsNullOrWhiteSpace(value)?new Dictionary<string,string>():JsonSerializer.Deserialize<Dictionary<string,string>>(value,JsonOptions)??new Dictionary<string,string>();}catch(JsonException){return new Dictionary<string,string>();}}
}
