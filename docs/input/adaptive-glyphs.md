# Adaptive glyphs

`IInputGlyphService` converts semantic actions into keyboard, Xbox, PlayStation, Nintendo, generic-numbered, or minimal-text prompts. Automatic mode follows active input; a persisted prompt lock overrides it. Every glyph has a text and accessibility fallback.

For PlayStation prompts, Cross, Circle, Square, Triangle, and D-pad resolve to central semantic asset keys and then package URIs. Page XAML never names a shape file. High Contrast and missing optional assets use descriptive text; Options, Create, shoulders, and triggers remain semantic text fallbacks until approved assets are supplied.
