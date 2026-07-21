# Windows application provider

Phase 4 adds three local, read-only providers: `windows-start-menu`, `windows-apps-folder`, and `windows-pwa`. They discover Windows-visible launch entries, normalize them into structured candidates, classify them conservatively, and present a selective import preview. NexaStation never crawls all of Program Files because installation directories contain helpers, uninstallers, services, updaters, and private implementation files that are not user-facing applications.

Imported records keep the provider identity, original shortcut or shell identity, target and argument boundaries, confidence, category, tracking policy, and user overrides. Refresh updates provider-managed fields, marks vanished sources unavailable only after a successful complete scan, and never deletes cards or overwrites user categories, favorites, artwork, play history, card style, or manual launch targets.

External files are never modified or deleted. Discovery does not install, uninstall, repair, update, or elevate software. See the source-specific guides and [manual checklist](windows-app-manual-testing.md).
