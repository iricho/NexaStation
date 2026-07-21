using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;

namespace NexaStation.SystemIntegration.Sessions;

public sealed class CurrentActivityService:ICurrentActivityService
{
    private CurrentActivitySnapshot _current=new("NexaStation Home",null,null,"Direct");
    public CurrentActivitySnapshot Current=>_current.Duration is null?_current:_current with{Duration=DateTime.UtcNow-_startedAtUtc};
    private DateTime _startedAtUtc;
    public void SetFocusedItem(string? itemId,string? title,string? provider){if(_current.Duration is not null)return;_current=new(title??"NexaStation Home",provider,null,"Direct",itemId);}
    public void SetTrackedActivity(string itemId,string title,string? provider,DateTime startedAtUtc,string confidence){_startedAtUtc=startedAtUtc;_current=new(title,provider,DateTime.UtcNow-startedAtUtc,confidence,itemId);}
    public void ClearTrackedActivity(){_startedAtUtc=default;_current=new("NexaStation Home",null,null,"Direct");}
}
