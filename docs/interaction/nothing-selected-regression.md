# Nothing-selected regression

The defect came from resolving commands through `SelectedItem` or an ad-hoc focused `DataContext` while non-selecting lists only displayed focus. Popup transitions and recycled containers made that lookup null or stale. Phase 13 fixes the cause with a persistent focus-derived action target and keypress-time revalidation; null selection is no longer interpreted as no actionable item.
