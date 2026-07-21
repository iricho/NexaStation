# System audio

`ISystemVolumeService` controls only the Windows default rendering endpoint through Core Audio `IAudioEndpointVolume`. Values are normalized and clamped to 0.0–1.0. Mute is independent from volume. The service registers an endpoint-volume callback and does not continuously poll.

COM, endpoint, or policy failures return an unavailable state and leave the rest of NexaStation usable. Phase 6 does not change per-process audio, microphone levels, recording devices, enhancements, or spatial-audio settings.

Quick Menu left/right adjustment uses 5% steps; the slider supports 1% steps. Tests use fakes and never alter the test machine’s volume.
