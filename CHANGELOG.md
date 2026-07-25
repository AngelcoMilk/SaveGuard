# Changelog

## 0.1.1

- Fixed dropdown label showing another mod's text by replacing heuristic text matching with exclusion-based label targeting
- Changed in-game setting labels: "任务失败存档" / "物品回收率"
- Removed in-game emergency backup toggle (still always on, configurable via file)
- Preserved full Game Over flow with call-site-scoped deletion suppression
- Added GitHub source link to Thunderstore metadata
- Added bilingual README with anchor navigation

## 0.1.0

- Added configurable failed-extraction item recovery with discrete 0/25/50/75/100% choices, defaulting to 100%.
- Added quota-failure soft reset that preserves the current save, gold, inventory, Hub, Grimoire and quota tier.
- Added automatic restart from night one with the current Session score reset to zero.
- Added call-site-scoped quota-failure deletion protection that preserves the complete Game Over flow and avoids third-party `DeleteSlot` postfix side effects.
- Added fresh pre-reset, timestamped emergency save backups with retention control.
- Added a native-style SaveGuard tab to the in-game Settings panel.
- Added verified-build compatibility protection and policy tests.
