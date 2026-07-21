using NexaStation.SystemIntegration.Models;

namespace NexaStation.QuickMenu.Models;

public enum QuickMenuSection{QuickControls,Audio,MediaControls,Controllers,Network,System,Power,Notifications}
public enum QuickMenuActionType{Normal,Toggle,Slider,Navigation,Destructive,System}
public sealed record QuickMenuAction(string Id,string Label,string Description,string IconKey,QuickMenuActionType ActionType,bool RequiresConfirmation=false,bool RequiresHold=false,int HoldDurationMilliseconds=0,bool Enabled=true,string? DisabledReason=null);
public sealed record QuickMenuOpenRequest(string OriginPageId,string? OriginItemId,string? OriginControlKey,Func<Task>? RestoreFocusAsync=null);
public sealed record PendingQuickMenuConfirmation(string ActionId,string Label,bool RequiresHold,int HoldDurationMilliseconds);
public sealed record QuickMenuState(bool IsOpen,QuickMenuSection ActiveSection,string? OriginPageId,string? OriginItemId,string? OriginControlKey,CurrentActivitySnapshot CurrentActivity,SystemVolumeState AudioStatus,IReadOnlyList<ControllerStatus> ControllerStatus,NetworkStatusSnapshot NetworkStatus,PendingQuickMenuConfirmation? PendingConfirmation,bool IsSystemActionRunning,IReadOnlyList<QuickMenuAction> Actions);
public sealed class QuickMenuStateChangedEventArgs(QuickMenuState state):EventArgs{public QuickMenuState State{get;}=state;}
public enum NotificationType{Information,Success,Warning,Error,ActionRequired}
public sealed record NotificationItem(string Id,NotificationType Type,string Title,string Message,DateTime CreatedAtUtc,bool Read=false,string? ActionId=null,DateTime? ExpiresAtUtc=null);
public sealed record SystemActionHistoryEntry(string Id,SystemPowerAction ActionType,DateTime RequestedAtUtc,DateTime? CompletedAtUtc,int Result,string? FailureMessage);
