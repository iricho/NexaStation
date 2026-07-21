# Card specifications

## Game

Wide 16:9 surface for dashboard activity, 304 × 166 logical pixels. Prefer local wide background artwork, then a valid cover where appropriate. The lower scrim contains title and real activity. Provider badges are secondary. Never invent progress.

## Application and media service

Square 148 × 148 logical-pixel tile. Preserve extracted icon aspect ratio and center it at a useful size. If no icon is available, use a large initial over a deterministic color field derived from the stable item ID. Show `Last opened` only after real activity; otherwise use the add time.

## Gallery

Wide 184 × 116 logical-pixel photographic thumbnail. Load the Gallery thumbnail cache lazily when the control enters the visual tree and cancel the request when it leaves. Use image/video-specific fallbacks and a text video badge. Filename stays in a subtle lower scrim.

## Mixed content and collections

Mixed rows select the appropriate game/application template. Collection surfaces use the existing collection model and never masquerade as application tiles. A future mosaic may be added from project-owned/user-owned assets; Phase 7.5 does not synthesize commercial cover art.

## Shared focus and fallback rules

All cards use the centralized focus resources: solid accent border, focused surface contrast, and a compact target size. High Contrast retains a solid outline. Missing, inaccessible, narrow-for-hero, or unsupported files immediately fall back without leaving a blank surface. Fallback colors must be stable across sessions.
