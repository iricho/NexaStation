# Screenshots

NexaStation supports PNG and JPEG output to an approved folder. Every screenshot requires a direct action in the current session and a short-lived single-use authorization token. Frames are bounded to 16,384 pixels per dimension and 100 MiB, signatures are validated, the file is moved atomically, and activity/Gallery records are created only after success.

No launch, exit, achievement, timer, activity, provider, controller-connection, startup, shutdown, or background event can trigger capture. NexaStation never uploads captures.
