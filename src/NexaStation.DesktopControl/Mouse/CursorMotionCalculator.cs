using NexaStation.Domain;

namespace NexaStation.DesktopControl.Mouse;

public static class CursorMotionCalculator
{
    public static DesktopCursorMotion Calculate(double x,double y,double elapsedSeconds,DesktopControlProfile profile,bool slowMode,bool fastMode)
    {
        if(elapsedSeconds<=0||elapsedSeconds>0.25)elapsedSeconds=Math.Clamp(elapsedSeconds,0,0.25);double magnitude=Math.Sqrt(x*x+y*y);if(magnitude<=profile.CursorDeadzone)return new(0,0);double normalized=Math.Clamp((magnitude-profile.CursorDeadzone)/(1-profile.CursorDeadzone),0,1);double curve=Math.Pow(normalized,Math.Max(0.1,profile.CursorAcceleration));double multiplier=slowMode?0.35:fastMode?1.75:1;double distance=Math.Min(120,profile.CursorSensitivity*curve*multiplier*elapsedSeconds);return new(Math.Clamp(x/magnitude*distance,-120,120),Math.Clamp(-y/magnitude*distance,-120,120));
    }
}
