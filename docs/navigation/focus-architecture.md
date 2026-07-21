# Focus architecture

WinUI focus visuals are independent from list selection. Phase 13 records a stable action target whenever actionable content receives keyboard or controller focus and revalidates it before executing a semantic action. Pointer hover never becomes the target. See [action targets](../interaction/action-targets.md).
