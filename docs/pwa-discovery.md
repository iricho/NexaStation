# Installed web application discovery

PWA detection is deliberately confidence-based. NexaStation examines only Windows-visible Start Menu and packaged-app candidates, recognizes known browser launch identities, and looks for explicit app markers such as `--app-id`, `--app`, `--app-url`, or a packaged PWA identity. High/medium-confidence candidates can be imported through `windows-pwa`; ordinary website shortcuts remain web shortcuts and are not selected by default.

Browser profiles, history, cookies, bookmarks, extensions, login state, and browser databases are never scanned. This privacy boundary also prevents browser-version-specific profile parsing. The original Windows shortcut is preserved when present.

PWA duration tracking defaults to `None` because a browser process or tab cannot be attributed reliably. NexaStation records a launch event with zero duration rather than inventing usage time.
