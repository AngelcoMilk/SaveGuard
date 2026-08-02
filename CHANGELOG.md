# Changelog

## 0.1.4

- Fixed a quota-failure regression that could delete the active save slot after the host continued through the Game Over screen
- Preserved the quota-failure protection token across `RestartGame` until the matching `SvExecuteGameOver` deletion call is safely suppressed
- Added fail-closed restart exception handling so protected quota failures retain one-time deletion protection even if reset work throws
- Added lifecycle regression tests covering delayed Game Over execution, successful quota cleanup, disabled protection and restart exceptions
- Synchronized BepInEx, assembly and Thunderstore package versions for reliable installed-build identification

## 0.1.3

- Fixed Thunderstore README rendering: switched to details/summary fold layout, Chinese open by default
- Removed unsupported HTML anchors, CSS toggles, and fragment links incompatible with Thunderstore renderer

## 0.1.2

- Fixed settings injection on client-side multiplayer (tab now only appears for host and main menu)
- Fixed font fallback for machines where scene text search fails
- Fixed template selection to always use original game section instead of mod-dependent last section
- Fixed transpiler exception on method signature mismatch (now logs warning instead of throwing)
- Fixed `SoftFailureOccurred` state leak when `RestartGame` throws after marking

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
