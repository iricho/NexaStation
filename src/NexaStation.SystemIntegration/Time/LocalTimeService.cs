using System.Globalization;
using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;

namespace NexaStation.SystemIntegration.Time;

public sealed class LocalTimeService:ITimeService,IDisposable
{
    private Timer? _timer;
    public event EventHandler<LocalTimeChangedEventArgs>? TimeChanged;
    public LocalTimeSnapshot Current{get;private set;}=Create();
    public void StartUpdates(){if(_timer is not null)return;Current=Create();int due=Math.Max(1,60-DateTime.Now.Second)*1000;_timer=new Timer(_=>Tick(),null,due,60_000);}
    public void StopUpdates()=>Interlocked.Exchange(ref _timer,null)?.Dispose();
    public void Dispose()=>StopUpdates();
    private void Tick(){Current=Create();TimeChanged?.Invoke(this,new(Current));}
    private static LocalTimeSnapshot Create(){DateTimeOffset now=DateTimeOffset.Now;CultureInfo culture=CultureInfo.CurrentCulture;return new(now,now.ToString(culture.DateTimeFormat.ShortTimePattern,culture),now.ToString(culture.DateTimeFormat.ShortDatePattern,culture));}
}
