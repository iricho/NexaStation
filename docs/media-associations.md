# Media associations

Preferred players can be selected at global media-type, file-extension, Gallery-source, or individual-item scope. Resolution favors item, then source, extension, and global rules. The selected library application and launch target are validated before launch.

`{MEDIA_PATH}` is the only supported template variable. Templates reject unknown braces and shell chaining/redirection operators. Launching uses argument data, never `cmd.exe`, PowerShell, or a constructed command line. Unicode and paths containing spaces are preserved, and a launch preview is produced.

If a preferred player is missing, NexaStation reports the failure. Falling back to the Windows default requires an explicit user-confirmed request. Recent history is written only after the selected launch succeeds.
