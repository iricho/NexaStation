# Gallery manual testing

Use a disposable folder containing landscape/portrait images, a large image, duplicate filenames in subfolders, one supported video, one corrupt image, a hidden file, and an unsupported file. Keep originals backed up even though NexaStation is read-only toward them.

1. Add the folder without scanning; verify no cards appear until refresh.
2. Add/scan with images only, then enable videos and refresh.
3. Verify corrupt/unsupported files do not abort the scan and hidden files obey the source option.
4. Add a nested overlapping source; verify the warning. Verify an exact duplicate is refused.
5. Favorite an item, add it to an album, rename/move it inside the source, refresh, and inspect availability/state behavior.
6. Disconnect or rename the source folder, refresh, and verify existing rows become unavailable only after a definitive successful absence—not after an inaccessible scan. Relink the source and refresh.
7. Enable watching, create/rename/delete files, and verify one debounced refresh. Overflow behavior should fall back to a full refresh.
8. Scroll a large collection and verify lazy thumbnails, placeholders, bounded cache growth, and responsive cancellation.
9. Open the viewer; test wraparound, unavailable-item skipping, resize, fit/fill/actual size, 8× zoom clamp, panning bounds, and focus restoration.
10. Test slideshow pause/resume, loop, shuffle, cancellation, and foreground loss.
11. Set a source private; verify it disappears from Home/global recent/search while remaining accessible through its explicit source context.
12. Test Windows default video playback and a preferred player with a path containing spaces. Verify malformed/shell templates are rejected.
13. Remove an album, source, and thumbnail cache; verify every original file remains byte-for-byte present.

For scale testing, use at least 10,000 small fixture files and capture scan time, UI responsiveness, process working set, thumbnail-cache size, and cancellation latency on the target device. Hardware-controller and packaged-MSIX tests must be performed on Windows hardware with the intended controller and codecs.
