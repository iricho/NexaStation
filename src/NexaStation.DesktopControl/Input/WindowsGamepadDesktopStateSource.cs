using NexaStation.Core;
using NexaStation.Domain;
using Windows.Gaming.Input;

namespace NexaStation.DesktopControl.Input;

public sealed class WindowsGamepadDesktopStateSource : IDesktopControllerStateSource
{
    public DesktopControllerReading Read(){IReadOnlyList<Gamepad> gamepads=Gamepad.Gamepads;Gamepad? gamepad=gamepads.Count==0?null:gamepads[0];if(gamepad is null)return new(false,0,0,0,0,0,0,DesktopControllerButtons.None,DateTime.UtcNow);GamepadReading r=gamepad.GetCurrentReading();return new(true,r.LeftThumbstickX,r.LeftThumbstickY,r.RightThumbstickX,r.RightThumbstickY,r.LeftTrigger,r.RightTrigger,Buttons(r.Buttons),DateTime.UtcNow);}
    private static DesktopControllerButtons Buttons(GamepadButtons value){DesktopControllerButtons result=0;if(value.HasFlag(GamepadButtons.A))result|=DesktopControllerButtons.A;if(value.HasFlag(GamepadButtons.B))result|=DesktopControllerButtons.B;if(value.HasFlag(GamepadButtons.X))result|=DesktopControllerButtons.X;if(value.HasFlag(GamepadButtons.Y))result|=DesktopControllerButtons.Y;if(value.HasFlag(GamepadButtons.LeftShoulder))result|=DesktopControllerButtons.LeftShoulder;if(value.HasFlag(GamepadButtons.RightShoulder))result|=DesktopControllerButtons.RightShoulder;if(value.HasFlag(GamepadButtons.Menu))result|=DesktopControllerButtons.Menu;if(value.HasFlag(GamepadButtons.View))result|=DesktopControllerButtons.View;if(value.HasFlag(GamepadButtons.LeftThumbstick))result|=DesktopControllerButtons.LeftThumb;if(value.HasFlag(GamepadButtons.RightThumbstick))result|=DesktopControllerButtons.RightThumb;if(value.HasFlag(GamepadButtons.DPadUp))result|=DesktopControllerButtons.DPadUp;if(value.HasFlag(GamepadButtons.DPadDown))result|=DesktopControllerButtons.DPadDown;if(value.HasFlag(GamepadButtons.DPadLeft))result|=DesktopControllerButtons.DPadLeft;if(value.HasFlag(GamepadButtons.DPadRight))result|=DesktopControllerButtons.DPadRight;return result;}
}
