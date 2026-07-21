# Action targets

`IActionTargetService` records the stable item id, kind, page, region, data context, focus origin, input origin, and timestamp whenever actionable focus changes. The target is resolved again at the keypress boundary. Hover updates are rejected, and stale targets are cleared rather than silently redirected.
