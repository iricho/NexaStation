# Safe remote-play arguments

Profiles produce an argument list directly; no command shell is used. Only `{TARGET_LABEL}`, `{HOST_HINT}`, `{DISPLAY_WIDTH}`, and `{DISPLAY_HEIGHT}` are accepted. Command chaining, redirection, expansion syntax, newlines, percent expansion, and unknown variables are rejected. Host hints are user-managed labels/configuration, not discovered credentials.
