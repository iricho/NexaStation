# Navigation audit

The opt-in diagnostics mode records semantic focus movement/missing/trapped/invalid-target events, dialog lifecycle, origin restoration, controller disconnect, and input-device changes. It never logs raw stick values. Reports are bounded, redacted, local JSON.

Automated focus scenarios supplement rather than replace controller/keyboard testing on Home, Games, Apps, Media, Gallery, Library, Collections, Settings, Quick Menu, onboarding, recovery, Safe Mode, details, setup, viewers, and confirmation dialogs.
## Phase 10 additions

Adaptive prompts always include text/automation labels. Onboarding, recovery, the dashboard, Quick Menu, remapping, calibration, and OSK use semantic navigation and safe Cancel behavior. Manual Beta verification must cover controller-only, keyboard-only, mouse, touch, RTL, high contrast, reduced motion, disconnect, and focus restoration.

## Phase 11.5 context menus

Context-menu audits must record origin identity, modal entry, action focus, child-level navigation, close reason, and restoration target without logging source paths. Cancelling restores the exact live origin where possible, then the stable item ID, then the selected section surface. Disabled actions expose a readable reason.
