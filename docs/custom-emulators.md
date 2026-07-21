# Custom emulators

Custom emulator templates support `{EMULATOR_PATH}`, `{GAME_PATH}`, `{GAME_DIRECTORY}`, `{GAME_FILENAME}`, `{GAME_FILENAME_NO_EXTENSION}`, `{PLATFORM_ID}`, `{GAME_IDENTIFIER}`, `{CORE_PATH}`, and `{USER_DATA_DIRECTORY}`.

Unknown variables, unmatched quotes, line breaks, and shell chaining characters (`&`, `&&`, `|`, `||`, `;`) are rejected. Templates are tokenized into an argument list and never passed through `cmd.exe`. Arbitrary environment expansion and script evaluation are not supported. Environment overrides are explicit profile values rather than captured user-session variables.
