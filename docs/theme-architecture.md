# Theme architecture

NexaStation now distinguishes two layers of visual customization.

The existing customization service owns color/background/card/sound presets such as Nexa Dark, Obsidian, Aurora, Light Room, and High Contrast. Preview is transient, cancel restores the prior preset, and apply validates and persists. High Contrast and reduced-motion policy continue to apply across preset changes.

Phase 13.5 adds a higher-level UI theme contract with the built-in IDs `nexa.default` and `nexa.crossbar`. These themes own independent settings and shell presentations and share declarative destinations/settings rather than page controls. See [Phase 13.5 theme architecture](themes/phase13-5-architecture.md) and [categorized settings architecture](settings/architecture.md).

Downloaded themes, remote XAML, remote code, arbitrary asset paths, and manifest-declared commands are not supported.
