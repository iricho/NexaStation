# Gallery viewer

Opening an image enters a fullscreen overlay while retaining the Gallery focus ID. Previous/next navigation uses the active query order, wraps at the ends, and skips unavailable files. Closing returns focus to the originating card or the nearest surviving card.

Images load asynchronously with cancellation when navigation changes. Fit, fill, actual-size, and custom zoom modes are represented in the viewport controller. Zoom is clamped from the fitted scale through 8× fitted scale. Pointer drag pans only within computed image bounds; resize and item changes clamp or reset the viewport.

Keyboard/controller left and right navigate, up/down zoom, and Back/Escape closes. Videos use external Windows playback rather than an embedded decoder. See [media launching](gallery-media-launching.md).
