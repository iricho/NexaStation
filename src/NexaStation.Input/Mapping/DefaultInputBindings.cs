using NexaStation.Core;

namespace NexaStation.Input.Mapping;

public static class DefaultInputBindings
{
    public const string BuiltInProfileId = "builtin.default";

    public static IReadOnlyList<InputBinding> Create(DateTimeOffset? now = null)
    {
        DateTimeOffset timestamp = now ?? DateTimeOffset.UnixEpoch;
        List<InputBinding> result = [];
        AddController(result, InputAction.NavigateUp, "DPAD_UP", timestamp);
        AddController(result, InputAction.NavigateDown, "DPAD_DOWN", timestamp);
        AddController(result, InputAction.NavigateLeft, "DPAD_LEFT", timestamp);
        AddController(result, InputAction.NavigateRight, "DPAD_RIGHT", timestamp);
        AddController(result, InputAction.Confirm, "FACE_SOUTH", timestamp);
        AddController(result, InputAction.Cancel, "FACE_EAST", timestamp);
        AddController(result, InputAction.Search, "FACE_WEST", timestamp);
        AddController(result, InputAction.Favorite, "FACE_NORTH", timestamp);
        AddController(result, InputAction.ContextMenu, "FACE_WEST", timestamp, InputContext.Dashboard);
        AddController(result, InputAction.ContextMenu, "FACE_WEST", timestamp, InputContext.MediaPlayer);
        AddController(result, InputAction.ToggleDetails, "FACE_NORTH", timestamp, InputContext.MediaPlayer);
        AddController(result, InputAction.Options, "MENU", timestamp);
        AddController(result, InputAction.QuickMenu, "VIEW", timestamp);
        AddController(result, InputAction.PreviousSection, "LEFT_SHOULDER", timestamp);
        AddController(result, InputAction.NextSection, "RIGHT_SHOULDER", timestamp);
        AddController(result, InputAction.PreviousPage, "LEFT_TRIGGER", timestamp);
        AddController(result, InputAction.NextPage, "RIGHT_TRIGGER", timestamp);
        AddController(result, InputAction.EmergencyDisable, "LEFT_SHOULDER+RIGHT_SHOULDER+LEFT_STICK+RIGHT_STICK", timestamp);
        AddController(result, InputAction.CaptureScreenshot, "LEFT_SHOULDER+RIGHT_SHOULDER+FACE_WEST", timestamp, InputContext.Dashboard);
        AddKey(result, InputAction.NavigateUp, "UP", timestamp); AddKey(result, InputAction.NavigateDown, "DOWN", timestamp);
        AddKey(result, InputAction.NavigateLeft, "LEFT", timestamp); AddKey(result, InputAction.NavigateRight, "RIGHT", timestamp);
        AddKey(result, InputAction.NavigateUp, "W", timestamp); AddKey(result, InputAction.NavigateDown, "S", timestamp);
        AddKey(result, InputAction.NavigateLeft, "A", timestamp); AddKey(result, InputAction.NavigateRight, "D", timestamp);
        AddKey(result, InputAction.Confirm, "ENTER", timestamp); AddKey(result, InputAction.Cancel, "ESCAPE", timestamp);
        AddKey(result, InputAction.Confirm, "SPACE", timestamp); AddKey(result, InputAction.Cancel, "BACK", timestamp);
        AddKey(result, InputAction.Search, "F", timestamp); AddKey(result, InputAction.Favorite, "Y", timestamp);
        AddKey(result, InputAction.ContextMenu, "X", timestamp); AddKey(result, InputAction.ContextMenu, "APPLICATION", timestamp);
        AddKey(result, InputAction.QuickMenu, "CTRL+Q", timestamp); AddKey(result, InputAction.PreviousSection, "Q", timestamp);
        AddKey(result, InputAction.NextSection, "E", timestamp);
        return result;
    }

    private static void AddController(List<InputBinding> bindings, InputAction action, string code, DateTimeOffset at, InputContext context = InputContext.Global) =>
        bindings.Add(new($"controller.{context}.{action}", BuiltInProfileId, action, new(code.Contains('+') ? PhysicalInputKind.ControllerChord : PhysicalInputKind.ControllerButton, code), context, bindings.Count, at, at));

    private static void AddKey(List<InputBinding> bindings, InputAction action, string code, DateTimeOffset at) =>
        bindings.Add(new($"keyboard.{action}.{code}", BuiltInProfileId, action, new(PhysicalInputKind.KeyboardKey, code), InputContext.Global, bindings.Count, at, at));
}
