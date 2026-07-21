# Emulator installations

An `EmulatorInstallation` represents one user-selected executable. Several installations may share an adapter, supporting stable, nightly, portable, development, and forked builds.

Safe probes use only adapter-approved single arguments such as `--version` or `--help`. Output and runtime are bounded; only the probe process is stopped after timeout. Output is treated as text and never executed. Probe failure remains recoverable through explicit manual configuration.

Relinking updates the same installation record, re-probes it, and preserves dependent games, folders, profiles, artwork, favorites, and playtime. Disabling an installation does not delete games or external files.
