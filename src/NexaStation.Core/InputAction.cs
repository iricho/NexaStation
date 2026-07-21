namespace NexaStation.Core;

public enum InputAction
{
    NavigateUp,
    NavigateDown,
    NavigateLeft,
    NavigateRight,
    Confirm,
    Cancel,
    Search,
    Favorite,
    ContextMenu,
    Options,
    QuickMenu,
    PreviousSection,
    NextSection,
    PreviousPage,
    NextPage,
    ToggleDetails,
    ToggleKeyboard,
    DeleteCharacter,
    SubmitText,
    SwitchKeyboardLayout,
    Shift,
    Space,
    CursorLeft,
    CursorRight,
    Home,
    ReturnToDesktop,
    EmergencyDisable,
    CaptureScreenshot,

    // Compatibility aliases retained for providers compiled against the Phase 0 contract.
    OpenSearch = Search,
    OpenContextMenu = ContextMenu,
    OpenQuickMenu = QuickMenu,
}
