# Dynamic Shelves

Dynamic Shelves use structured field/operator/value rules and Match All or Match Any semantics. Supported fields include content type, provider, platform, favorites, hidden and availability state, recent activity, dates, playtime, category, emulator, title, lifecycle status, Gallery media type, and Gallery source kind. Unsupported fields are not offered by the editor. No arbitrary SQL, expressions, scripts, or executable content is accepted.

The guided editor validates every rule and provides a live preview capped at 100 results. Hidden content is excluded unless the Shelf explicitly defines a hidden-state rule. Results are cached for 15 seconds and invalidated after relevant Shelf or status changes.

Rules are validated before evaluation. Unknown fields or operators, missing required values, missing Shelves, and invalid sources fail locally without breaking dashboard startup.
