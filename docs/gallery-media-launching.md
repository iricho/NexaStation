# Gallery media launching

Images use the built-in viewer. Videos and explicit **Open with default app** actions are dispatched only after a direct user action. The Windows default association is the fallback.

Preferred players can be resolved in this order: media-item preference, source preference, then global video preference. A preferred player references an existing NexaStation application library item. Its argument template is tokenized without a command shell and supports the literal `{MEDIA_PATH}` placeholder. The path is passed as one argument even when it contains spaces or shell characters.

Templates containing shell operators or script hosts (`cmd`, PowerShell, `wscript`, or `cscript`) are rejected. The referenced executable must still pass normal launch-target validation. Failure can fall back to the Windows default association when the request allows it.

Phase 5 does not embed codecs, inspect browser profiles, download players, install codecs, or elevate a player. Process-duration tracking remains conservative for externally associated applications.

Phase 6 adds `MediaAssociations` with item, source, extension, and global-media-type scopes; item rules win. Existing Gallery preferences remain compatible. The preferred player and target are validated, a preview is generated, and fallback to Windows default is explicit. Successful opens add `RecentMedia`; private-source media is excluded outside Gallery and failed opens add nothing.
