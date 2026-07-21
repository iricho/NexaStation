# Audio output devices

Active rendering endpoints are enumerated with Windows Core Audio and the current default endpoint is identified. NexaStation displays Windows-provided identifiers when a friendly name is not safely available; it does not guess speaker, headphone, television, or HDMI types.

Windows has no supported public API for changing the default endpoint. Phase 6 therefore isolates switching behind `IAudioOutputDeviceService` and uses the documented `ms-settings:sound` Sound Settings fallback. No undocumented policy COM interface, command shell, third-party switch utility, or bundled executable is used.

Device enumeration failures are reported as an unavailable snapshot and never block Quick Menu opening.
