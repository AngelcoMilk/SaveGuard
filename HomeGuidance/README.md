# HomeGuidance

Client-side extraction navigation for YAPYAP. Replaces the compass directional animation with a real fastest-route arrow and adds world-space trail dots after any player reaches the extraction zone.

## Features

- **Smart navigation arrow** replaces the vanilla compass direction display while keeping the compass background, frame, layout, and settings toggle intact
- **Route planning** across NavMesh walk paths and directional teleporter chains (A→B→C→A rings supported)
- **Color-coded direction**: white (same level), blue (going up), red (going down), purple (teleporter entrance)
- **World trail dots** appear for players who haven't yet reached extraction, after **any** player first reaches the circle
- **Client-local only** — no custom Mirror messages, works with unmodded clients in the same lobby

## Installation

Install via Thunderstore Mod Manager or copy the `HomeGuidance` folder into `BepInEx/plugins/`.

### Requirements

- BepInEx 5.4.2100+
- YAPYAP (any supported build)

## Configuration

All settings are in `BepInEx/config/com.angelcomilk.homeguidance.cfg`.

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master toggle |
| `ArrivalRadius` | `2.0` | Radius (m) around extraction for arrival detection |
| `TrailDotSpacing` | `1.5` | Spacing (m) between trail dots |
| `LookAheadDistance` | `6.0` | Distance (m) along path for arrow target |
| `DebugLogging` | `false` | Verbose graph/hierarchy logging |

## Compatibility

- Clients can install independently; unmodded clients can still join
- Only the installing player sees navigation visuals
- Compatible with other UI mods (arrow is a separate layer inside the compass)

## Known Limitations (v0.1.0)

- Late-joining clients cannot recover pre-join arrival history (they start observing from join time)
- Teleporter "Finished" state is treated conservatively (no teleporter edge during that window)
- Remote player positions are subject to Mirror sync frequency

## Credits

AngelcoMilk — SaveGuard / MoreSlots / HomeGuidance
