# Typography specifications

Typography resources live in `Styles/NexaTypography.xaml` and are consumed by semantic roles rather than page-local font sizes.

| Role | Intended use |
|---|---|
| Display title | Full page and major empty states |
| Hero title | Featured content title |
| Page title | Non-home section heading |
| Section heading | Dashboard row and column titles |
| Card title | One-line content identity |
| Card subtitle | Activity, category, provider, or filename |
| Navigation label | Primary section tabs |
| Button text | Hero and dialog actions |
| Caption/status | Badges, provider, clock, and footer hints |

Artwork always receives a dark scrim before text. Text trimming is limited to card titles; primary navigation, page titles, notifications, and empty-state explanations must remain readable at Windows text scaling. The compact state removes nonessential subtitle text before reducing primary labels.
