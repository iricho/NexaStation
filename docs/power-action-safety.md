# Power-action safety

Every system power request is explicit and user initiated. Sleep and Sign Out require confirmation. Restart PC and Shut Down require confirmation followed by a continuous 1.5-second hold.

The hold is canceled on button release, focus change, controller disconnect, foreground loss, menu close, or cancellation. Only one system action may run at a time. Windows is asked not to force-close applications, so policy or open-application failures are surfaced rather than bypassed.

Tests replace the power service with fakes. Automated tests cannot sleep, sign out, restart, shut down, or change volume on the host machine.
