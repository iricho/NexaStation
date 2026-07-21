# Native media-player architecture

`INexaMediaPlayerService` owns the playback lifetime, state snapshot, resume policy, preferences, and recovery result. `WindowsMediaPlaybackEngine` is the only Windows decoding adapter. `NexaMediaPlayerPage` observes the service and never owns long-lived playback state.

Gallery and Media create a local-only `MediaOpenRequest`. The same engine session backs windowed and fullscreen rendering and is closed when the overlay exits.
