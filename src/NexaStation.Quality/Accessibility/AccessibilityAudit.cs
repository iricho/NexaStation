namespace NexaStation.Quality.Accessibility;

public enum AccessibilityIssueSeverity { Critical, High, Medium, Low }

public sealed record AccessibilityControlSnapshot(
    string SemanticId,
    bool Interactive,
    bool Visible,
    bool Focusable,
    string? AccessibleName,
    double Width,
    double Height,
    bool HasTransparentForeground = false,
    bool IsDialog = false,
    bool TrapsFocus = true,
    bool HasBackAction = true);

public sealed record AccessibilityFinding(AccessibilityIssueSeverity Severity, string SemanticId, string Message);

public interface IAccessibilityAuditService
{
    IReadOnlyList<AccessibilityFinding> Audit(IReadOnlyList<AccessibilityControlSnapshot> controls, bool highContrastResourcesPresent, bool reducedMotionHonored);
}

public sealed class AccessibilityAuditService : IAccessibilityAuditService
{
    public IReadOnlyList<AccessibilityFinding> Audit(IReadOnlyList<AccessibilityControlSnapshot> controls, bool highContrastResourcesPresent, bool reducedMotionHonored)
    {
        ArgumentNullException.ThrowIfNull(controls);
        List<AccessibilityFinding> findings = [];
        foreach (AccessibilityControlSnapshot control in controls)
        {
            if (control.Interactive && control.Visible && string.IsNullOrWhiteSpace(control.AccessibleName)) findings.Add(new(AccessibilityIssueSeverity.High, control.SemanticId, "Interactive control has no accessible name."));
            if (!control.Visible && control.Focusable) findings.Add(new(AccessibilityIssueSeverity.High, control.SemanticId, "Hidden control remains focusable."));
            if (control.Interactive && control.Visible && (control.Width < 32 || control.Height < 32)) findings.Add(new(AccessibilityIssueSeverity.Medium, control.SemanticId, "Interactive focus target is smaller than 32x32 effective pixels."));
            if (control.HasTransparentForeground) findings.Add(new(AccessibilityIssueSeverity.Critical, control.SemanticId, "Text uses a transparent foreground."));
            if (control.IsDialog && !control.TrapsFocus) findings.Add(new(AccessibilityIssueSeverity.Critical, control.SemanticId, "Dialog does not trap focus."));
            if (control.IsDialog && !control.HasBackAction) findings.Add(new(AccessibilityIssueSeverity.High, control.SemanticId, "Dialog has no keyboard/controller back action."));
        }
        if (!highContrastResourcesPresent) findings.Add(new(AccessibilityIssueSeverity.High, "application", "High Contrast resources were not detected."));
        if (!reducedMotionHonored) findings.Add(new(AccessibilityIssueSeverity.High, "application", "Reduced Motion does not disable required animation flags."));
        return findings;
    }
}
