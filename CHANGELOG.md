# Changelog

## Unreleased
- New: planting preview. While planting with the cultivator, shows whether the seed will grow where you aim, and if not, why: too close to another plant or a rock, no open sky, wrong biome, too cold, ground not cultivated. Nearby plants get grow-space rings, red if they aren't growing.

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
