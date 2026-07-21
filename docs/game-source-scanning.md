# Game-source scanning

Game sources are explicit platform/folder pairs. Recursive scanning defaults on; hidden-file scanning and symbolic-link following default off. Extensions are platform configuration and matched case-insensitively. Archives (`.zip`, `.7z`, `.rar`) are excluded and never extracted.

The scanner uses metadata rather than whole-file reads or hashes, bounds identification concurrency to eight operations, supports cancellation, throttles progress, and continues after individual file errors. It never scans an entire drive unless that drive was explicitly selected as the source.

Filename normalization removes only recognized trailing region, revision, language, dump-quality, and disc tokens. Unknown parenthesized title text remains. Files are never renamed. Identification falls back to `FilenameOnly`; filename guesses are not stored as verified serials.

## Synthetic performance verification

Measured on 2026-07-17 using empty fictional `.iso` files on a warm local filesystem. Times are milliseconds:

| Files | Enumerate | Filter | Normalize | Existing match | Preview | No-change rescan | DB update | First progress |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0.564 | 0.237 | 3.228 | 0.093 | 2.769 | 9.909 | 4.759 | 8.779 |
| 1,000 | 0.461 | 0.036 | 1.405 | 0.409 | 20.216 | 26.051 | 5.893 | 2.425 |
| 5,000 | 1.591 | 0.089 | 2.897 | 1.635 | 96.773 | 133.007 | 11.234 | 2.698 |
| 20,000 | 7.085 | 0.521 | 36.445 | 14.153 | 707.606 | 970.088 | 36.899 | 5.301 |

“First progress” is the first throttled progress event available to the UI, not WinUI render latency. These values are reproducible test measurements, not guarantees for slow, removable, or inaccessible storage.
