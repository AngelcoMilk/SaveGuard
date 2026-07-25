# MoreSlots

> Expands YAPYAP inventory from 3 to up to 10 slots with configurable key bindings.

## Features

- **Extended inventory slots** — Increase your wand slots from 3 up to 10
- **Configurable key bindings** — Assign custom keys for slots 4–10 via BepInEx config
- **Full UI support** — Extra slots are visible, draggable, and selectable
- **Save persistence** — Extended slot contents survive game restarts
- **Multiplayer compatible** — Server-authoritative, uses same save key convention as Artisan

## Installation

Install via Thunderstore Mod Manager, Gale, or r2modman. Or manually:

1. Copy `MoreSlots.dll` to `BepInEx/plugins/MoreSlots/`
2. Launch the game once to generate config
3. Edit `BepInEx/config/com.angelcomilk.moreslots.cfg` to adjust settings

## Configuration

Config file: `BepInEx/config/com.angelcomilk.moreslots.cfg`

### General

| Key | Default | Description |
|---|---|---|
| `EnableExtendedSlots` | `true` | Enable or disable extended inventory |
| `MaxSlots` | `6` | Maximum inventory slots (3–10) |

### Key Bindings

| Key | Default | Description |
|---|---|---|
| `Slot4` | `<Keyboard>/4` | Key for slot 4 |
| `Slot5` | `<Keyboard>/5` | Key for slot 5 |
| `Slot6` | `<Keyboard>/6` | Key for slot 6 |
| `Slot7` | `<Keyboard>/7` | Key for slot 7 |
| `Slot8` | `<Keyboard>/8` | Key for slot 8 |
| `Slot9` | *(empty)* | Key for slot 9 |
| `Slot10` | *(empty)* | Key for slot 10 |

Binding format follows Unity Input System path convention. Examples:
- `<Keyboard>/4` — number key 4
- `<Keyboard>/f1` — function key F1
- `<Mouse>/middleButton` — middle mouse button
- Leave empty to disable

### UI

| Key | Default | Description |
|---|---|---|
| `ExpandInventoryUI` | `true` | Clone slot UI for extra slots |
| `HideInventoryFrame` | `true` | Hide vanilla frame (designed for 3 slots) |

## Multiplayer

- **Host** determines slot logic — all players should use the same `MaxSlots` value
- Save persistence uses `PLAYER.{id}.INV` key prefix (compatible with Artisan saves)
- Slot count reduction drops overflow items safely

## Known Limitations

- Vanilla inventory frame is designed for 3 slots; hidden by default
- No in-game rebind UI yet (edit config file to change keys)
- Some third-party inventory mods may conflict
