using System.Runtime.InteropServices;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Capture;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const uint WmHotkey=0x0312;private readonly Dictionary<string,int> _ids=new(StringComparer.Ordinal);private readonly Dictionary<int,string> _names=[];private readonly Func<nint> _windowHandle;private readonly SubclassProc _proc;private bool _subclassed;private int _next=0x4E00;
    public WindowsGlobalHotkeyService(Func<nint> windowHandle){_windowHandle=windowHandle;_proc=WindowProc;}
    public event EventHandler<GlobalHotkeyPressedEventArgs>? Pressed;
    public Task<HotkeyRegistrationResult> RegisterAsync(GlobalHotkeyDefinition definition,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();if(!definition.Enabled||!definition.UserApproved)return Task.FromResult(new HotkeyRegistrationResult(false,false,"The global screenshot hotkey is disabled until explicitly enabled."));if(_ids.ContainsKey(definition.Id))return Task.FromResult(new HotkeyRegistrationResult(true,false,"The hotkey is already registered."));nint hwnd=_windowHandle();if(hwnd==0)return Task.FromResult(new HotkeyRegistrationResult(false,false,"The NexaStation window is not ready."));if(!_subclassed){if(!SetWindowSubclass(hwnd,_proc,1,0))return Task.FromResult(new HotkeyRegistrationResult(false,false,"Windows could not attach the hotkey handler."));_subclassed=true;}int id=Interlocked.Increment(ref _next);if(!RegisterHotKey(hwnd,id,definition.Modifiers,definition.VirtualKey))return Task.FromResult(new HotkeyRegistrationResult(false,true,"That global hotkey is already used by Windows or another application."));_ids[definition.Id]=id;_names[id]=definition.Id;return Task.FromResult(new HotkeyRegistrationResult(true,false,"Global screenshot hotkey registered."));
    }
    public Task UnregisterAsync(string hotkeyId,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();if(_ids.Remove(hotkeyId,out int id)){UnregisterHotKey(_windowHandle(),id);_names.Remove(id);}return Task.CompletedTask;}
    public Task UnregisterAllAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();foreach(int id in _ids.Values)UnregisterHotKey(_windowHandle(),id);_ids.Clear();_names.Clear();return Task.CompletedTask;}
    public void Dispose(){UnregisterAllAsync().GetAwaiter().GetResult();nint hwnd=_windowHandle();if(_subclassed&&hwnd!=0)RemoveWindowSubclass(hwnd,_proc,1);_subclassed=false;GC.SuppressFinalize(this);}
    private nint WindowProc(nint hwnd,uint message,nint wParam,nint lParam,nint id,nint data){if(message==WmHotkey&&_names.TryGetValue((int)wParam,out string? name))Pressed?.Invoke(this,new(name));return DefSubclassProc(hwnd,message,wParam,lParam);}
    private delegate nint SubclassProc(nint hwnd,uint message,nint wParam,nint lParam,nint id,nint data);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);[DllImport("user32.dll",SetLastError=true)]private static extern bool UnregisterHotKey(nint hwnd,int id);[DllImport("comctl32.dll",SetLastError=true)]private static extern bool SetWindowSubclass(nint hwnd,SubclassProc proc,nint id,nint data);[DllImport("comctl32.dll",SetLastError=true)]private static extern bool RemoveWindowSubclass(nint hwnd,SubclassProc proc,nint id);[DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint hwnd,uint message,nint wParam,nint lParam);
}
