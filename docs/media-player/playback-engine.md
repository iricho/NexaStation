# Playback engine

NexaStation uses `Windows.Media.Playback.MediaPlayer`, `MediaSource`, `MediaPlaybackItem`, and `MediaPlaybackSession`; no codec engine is bundled. `MediaPlayerElement` renders the session with its system transport controls disabled. Engine events are detached and the source is released on close.

An extension is only a candidate signal. Failure to open or decode produces a failed snapshot and external-player recovery actions.
