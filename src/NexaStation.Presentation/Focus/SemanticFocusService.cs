using System.Text.Json;
using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Presentation.LayoutContracts;

namespace NexaStation.Presentation.Focus;

public sealed class SemanticFocusService(IApplicationStateService state):ISemanticFocusService
{
    private const string Key="Interface.SemanticFocus";
    public async Task SaveAsync(SemanticFocusState focusState,CancellationToken cancellationToken=default){state.SetDebounced(Key,JsonSerializer.Serialize(focusState));await state.FlushAsync(cancellationToken);}
    public async Task<FocusRestoreResult> RestoreAsync(SemanticFocusRequest request,CancellationToken cancellationToken=default)
    {
        string? json=await state.GetAsync(Key,cancellationToken);SemanticFocusState? saved=null;if(json is not null)try{saved=JsonSerializer.Deserialize<SemanticFocusState>(json);}catch(JsonException){}
        string? preferred=request.PreferredTargetId??saved?.TargetId;if(preferred is not null&&request.VisibleTargetIds.Contains(preferred,StringComparer.Ordinal))return new(true,preferred,request.VisibleTargetIds.IndexOf(preferred));if(request.VisibleTargetIds.Count==0)return new(false,null,0,"The destination has no focusable content.");int index=Math.Clamp(saved?.FallbackIndex??request.FallbackIndex,0,request.VisibleTargetIds.Count-1);return new(true,request.VisibleTargetIds[index],index,preferred is null?null:"The previous target is unavailable; the nearest item was selected.");
    }
}
internal static class FocusCollectionExtensions{public static int IndexOf<T>(this IReadOnlyList<T> values,T value){for(int index=0;index<values.Count;index++)if(EqualityComparer<T>.Default.Equals(values[index],value))return index;return-1;}}
