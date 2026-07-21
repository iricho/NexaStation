# Focus versus selection

The white card outline is the WinUI focus visual, not `SelectedItem`. Card lists intentionally use `SelectionMode=None`, so selection can be null while a card is visibly focused. Pointer hover is decorative and never replaces the actionable controller or keyboard target.
