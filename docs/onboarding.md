# Onboarding

Onboarding is a persisted, resumable ten-step flow: Welcome, Layout, Performance, Input, Providers, Applications, Gallery, Startup, Privacy, and Review. Its state is stored separately from library emptiness, so an intentionally empty library does not trigger setup again.

Closing setup leaves it in progress and resumes the saved step next launch. **Skip setup** is also persisted. Existing databases without onboarding state are marked as upgrades. Provider imports and folder selection remain explicit actions in their normal sections; onboarding stores pending guidance instead of inventing data or launching broad scans silently.

Start-with-Windows is off by default. Gallery folder access, diagnostics export contents, Steam/emulation guidance, and Windows application discovery are all explained before a choice is stored.
