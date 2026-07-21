# Compatibility system

NexaStation resolves validated declarative profiles before launch-target validation. Precedence is: exact local user profile; exact built-in provider identity; exact signed-feed provider identity; executable identity and version; platform plus emulator adapter; provider-wide profile; safe defaults. More specific rules win within a source, then the newer validated profile version. Conflicts are retained in diagnostics.

Display names never match. Generated diagnostic suggestions remain disabled until the user confirms them. Providers consume typed resolved rules and never execute profile JSON.

Status values are Untested, Launches, Launches with Limitations, Fully Compatible, Broken, Unsupported, and Needs Retest. “Fully Compatible” requires repeat verification of launch, tracking, restoration, focus, input, relaunch, and absence of destructive side effects.
