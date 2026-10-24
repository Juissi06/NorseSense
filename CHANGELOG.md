# Changelog

## 0.9.0
New features
- Planting preview: while planting with the cultivator, shows whether the seed will grow where you aim, and if not, why: too close to another plant or a rock, no open sky, wrong biome, too cold, ground not cultivated. Nearby plants get grow-space rings, red if they aren't growing.
- Shelter readout: while building under a roof, or placing a comfort piece, shows whether your spot counts as shelter, and if not, why: no roof, or which directions are still open (the game needs 80% cover).
- Terrain limits: with the hoe or pickaxe, shows how much further the ground can be dug or raised where you aim. The game keeps terrain within 8 m of its original height and never tells you.
- Support readout: while building, looking at a piece that isn't grounded shows its exact structural support and the level where it falls, instead of just a colour.
- Fire status: looking at a fire that has fuel but isn't burning, or burns low, tells you why: buried, something too close above the flames, rain without a roof, or wind without enough cover.
- General → Enabled switches the whole mod on or off in game. Off removes all its patches and overlays and restores build distance and pickup range, so the game is fully vanilla.

New look
- Every panel uses Valheim's own UI art and fonts: tooltip-style HUD panels, the wooden crafting-window panel for the marker manager, and the game's buttons, text fields, checkboxes, dividers and bars. Text has a soft shadow so it reads over snow and sky. Falls back to a matching built-in style if the game's art can't be found.
- Build info lines show the matching icon: the seed you're planting, the Rested and Sheltered status icons, the piece you're looking at, your pickaxe.
- Spawn legend shows a coverage bar. The hull readout is a small health bar with your ship's icon that flashes when hit and pulses when critical.
- The build info box shows "Removal locked" while the lock is on, and sizes itself to its text.
- Marker manager: column headings, and selected rows are highlighted.
- Toggle messages share one style. Shorter hull warnings: "Hull health critical: 25%" and "Hull damaged: 50%".

Changes
- The config file is now `BepInEx/config/NorseSense.cfg` (plugin ID `NorseSense`). Your settings from `juissi.norsesense.cfg` are copied over automatically the first time.

Fixes
- If the marker manager couldn't start on a player's setup, it broke all interaction, keys and hover text. Now only the marker manager turns off, with a message.
- The compatibility check also looks for mods that change plant growth or terrain editing.

## 0.8.0
- Renamed from QoL Pack to **Norse Sense**. New plugin ID (`juissi.norsesense`) and config file (`juissi.norsesense.cfg`).
- Added requirements and installation instructions.

## 0.7.0
- New: cartography table marker manager (Left Shift + E). Search, filter, sort, rename, change icon, cross out, delete, bulk actions, a duplicate finder with one-click merge, and safe saving that merges with the table's current data. Ward/builder/admin edit permission.
- New: reading a table updates shared markers you already have when their name, icon or crossed-out state changed.

## 0.6.0
- New: hull readout and hull damage warnings while aboard a ship.
- New: cartography table marker filters. Skip crossed-out markers, merge duplicates, skip icon types, or import exploration only. Writing to a table still keeps every marker on it.

## 0.5.0
- New: portals list the carried items that stop you from teleporting.
- New: removal lock (F9) stops the hammer from removing pieces by accident.
- Changed: building and planting stamina now default to vanilla (1.0). Existing configs keep their value.
- Release packaging for Thunderstore and Nexus.

## 0.4.0
- New: compatibility check. Detects overlapping mods (Gizmo, Valheim Plus, Spawn That, Custom Raids, Expand World, or any mod changing the same game code), adjusts features automatically and warns once.
- New: F8 shows or hides the build info box. The rotation readout now hides itself after a moment.
- The mod no longer loads on dedicated servers.

## 0.3.0
- New: spawn view on the minimap and large map.
- New: raid check in the legend, plus an orange ring showing the area of an active raid.
- New: fine rotation (Left Alt + scroll), match rotation (Left Alt + T) and comfort preview.

## 0.2.0
- New: spawn-proofing view. Ground overlay of protected and exposed spots, base rings, placement preview, nest and spawner markers, legend.
- New: optional build distance and auto-pickup range.

## 0.1.1
- Fixed: stamina reduction only applies to building and planting actions, not to running, jumping or dodging while holding the tool.
- Fixed: the spawner circles follow the terrain.
