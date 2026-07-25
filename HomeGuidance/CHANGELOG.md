# Changelog

## 0.1.0 (2026-07-25)

- Initial release
- Client-local fastest-route navigation arrow (white/blue/red/purple)
- A* replacement: Dijkstra solver with directed teleporter graph
- World trail dots with arc-length sampling and object pooling
- Conservative teleporter sweep availability (Activating-window only, Finished/Unknown skipped)
- Dual-threshold vertical color state for arrow
- Compass background/frame/layout/settings preserved
- 14-point Build Guard with SHA-256 hash verification
- Local arrival tracking with monotonic state (never forgets)
- Teleport deduplication and position jump fallback
- Three-phase graph construction (create, resolve, compute)
- `LocalTransition` edges for chained teleporter routing
- No custom Mirror messages
