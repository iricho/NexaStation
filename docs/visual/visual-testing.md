# Visual verification

Build and test from the repository root:

```powershell
dotnet build NexaStation.sln -c Release --no-restore
dotnet test NexaStation.sln -c Release --no-build --no-restore
```

On machines without the matching Windows App Runtime registration, create the self-contained x64 development build:

```powershell
dotnet publish src/NexaStation.App/NexaStation.App.csproj -c Release -p:Platform=x64 -p:PublishSelfContainedApp=true --no-restore
```

Capture 1280 × 720, 1920 × 1080, and 2560 × 1440 with the same deterministic local sample database. For each capture verify: no outer desktop/margins, readable navigation, correct active tab, nonblank hero, truthful row headings, real Gallery thumbnails, type-specific fallbacks, no clipped focused card, and a footer that remains on the display edge.

Manual matrix:

- Keyboard, mouse, then a physical controller when available.
- Fullscreen toggle, development desktop shortcut, Alt+Tab, and return from an external launch.
- Valid, missing, unavailable, and corrupt local artwork.
- Empty and populated Gallery; private sources must not appear on Home.
- 100%, 125%, 150%, and 200% Windows scaling.
- Reduced Motion and High Contrast.
- Rapid horizontal navigation to confirm delayed background requests cancel.
- Invalid launch to confirm a contextual dismissible toast and retained navigation.

Do not mark a row or environment as verified unless it was actually exercised. Record startup, idle CPU/GPU, working/private memory, and any Windows App Runtime prerequisites beside each test run.
