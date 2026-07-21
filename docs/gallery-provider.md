# Gallery provider

Phase 5 adds a local Gallery provider split across `NexaStation.Providers.Gallery`, `NexaStation.Gallery`, SQLite infrastructure, and the WinUI composition layer. It indexes only folders that a user explicitly approves. It does not crawl the user profile, Pictures, Videos, removable drives, network shares, or cloud folders automatically.

The provider owns path policy, read-only traversal, metadata inspection, thumbnails, refresh, and optional debounced watchers. The Gallery layer owns viewer navigation, viewport math, slideshows, albums, managed backgrounds, and media launching. Provider code does not write arbitrary external files; persistence is performed through `IGalleryPersistence`.

Gallery media is separate from ordinary launchable library items. This prevents photos from appearing in Games or Apps and lets private sources be excluded from Home without weakening the ordinary library query contract.

See [sources](gallery-sources.md), [scanning](gallery-scanning.md), [privacy](gallery-privacy.md), and [manual testing](gallery-manual-testing.md).

Phase 7 layouts share this same Gallery index. Pins and collections store typed Gallery IDs only; they do not duplicate media rows or weaken source privacy. Private Gallery data is excluded from interface configuration exports. Gallery entities that become unavailable may retain user pin/collection intent without causing source media changes.

Phase 12 can target-index a successfully written NexaStation screenshot only when its destination is already an enabled, explicitly approved Gallery source. It does not rescan unrelated roots. Recording observation likewise accepts only approved folders, waits for externally written files to stabilize, marks them external/private, and never assumes NexaStation caused the recording.

Fresh Phase 13 onboarding visibly proposes the Windows Pictures folder and lets the user opt out, mark it Private, hide it from Home, or disable subfolders before indexing. The source is normalized and added only once after completion. Existing users receive a proposal state rather than silent enrollment, and source removal never deletes files.
