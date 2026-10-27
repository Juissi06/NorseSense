# Changelog

## 0.13.0

New features
- Map layers: two toggles on the large map. Wards and stations shows each ward's area (green where you may build, red where you may not, grey when off) and crafting stations' build range. Portals draws a line between each pair, marks unpaired ones in yellow and shows their tags when zoomed in. Complete when you host; on a server it fills in as you travel.
- Harvest an area: hold Left Alt while picking a crop to pick every ripe crop of the same kind within 5 m. With a cultivator and seeds on you, they're replanted, paid like planting by hand.
- Farm check: with the cultivator out, every crop around you gets its grow ring, and the ones that aren't growing say why (no open sky, too close, not cultivated...).
- Removal preview: hold Left Alt (or lock removal with F6) and aim at a piece to see which pieces would fall if you removed it, tinted red, with a count in the info box.
- Precision helpers while holding Left Alt: snap points of the piece you place and of the pieces around it, the snapped pair in green, and the piece's height above the ground or the piece below.
- Raise ground works with the area sizes: every tile is raised, then the area is levelled to one height for a flat platform (two hoe uses per tile).
- A 1 × 3 strip for the hoe and cultivator, along where you look, for paths and roads.
- Server limits: a server or host can cap freecam (or turn it off), area tool and harvest sizes, stamina multipliers, build distance and pickup range for every player with the mod. Players can pick less, never more. No effect on vanilla players or servers.
- Layout mode (Ctrl + L): drag the legend, build info and comfort card where you want them; right-click one to put it back.
- A one-time welcome the first time you load into a world, with the main keys.

Cleaner look
- One set of colours for every overlay: green safe, yellow needs attention, red danger. Safe ground is green now instead of blue.
- Softer ground colours, and no painting at all out in the open with no base nearby: the legend just says so.
- One outline around the edge of your protected area instead of a ring around every fire and workbench.
- The legend lists only the colours in view, in plain words ("Raids can come here").
- Comfort mode only paints sheltered floor.
- The build info box stays quiet unless something matters: support only when it's weak, "sheltered" only while placing comfort pieces. Hold Left Alt to see everything. With a lot going on, it keeps the pressing rows and folds the rest into one "More" row.
- Shelter readout: a small compass lights up the open sides and the sky instead of a long list of directions.
- Every panel has the same look: icons in inventory-style slots, and panels fade in and ease between sizes.
- All messages in one style, and no pop-ups for things already on screen (base view mode, freecam on/off, layout mode).
- Settings in six sections (General, Base View, Building, Freecam, Farming, Map and Portals) instead of fourteen, with the fine-tuning ones under Configuration Manager's advanced settings, leaving about 25 everyday ones. Your old values carry over.

Improvements
- Planting grids space seeds so their grow rings touch instead of overlapping, and the preview also keeps out of a bigger neighbour's grow space, so planting can't stop it growing.
- Freecam picks up where you left it if you go back in without walking off.
- Repairs mode: pieces keep their tint while you hover them with the hammer instead of flickering.
- Area planting and harvesting turn themselves off next to Plant Easily or Mass Farming.

## 0.12.0
- The comfort card no longer shows while you sit on a ship.
- Spawn view: water where no monster but a sea serpent can spawn shows grey instead of red, on the ground and on the map, and no longer counts as exposed ground or against the protected %. Read from the game's own spawn lists and the raids you've unlocked, so shallow water where necks or leeches can spawn stays red, and water where only an unlocked raid could spawn something shows orange; fish, seagulls and other animals that never attack don't count. With only water in view the bar says so instead of "Protected 100%".
- Area tools: the extra tiles now wear down the hoe or cultivator like the clicked one, stop when it breaks, use the right stamina check, and pay off the skill debt from removing pieces before raising the skill, as vanilla does.
- Freecam stops at walls, pieces and rocks like the game's own camera, so it can't look into sealed rooms or other bases; it turns off if you walk away with a wall between you and it.
- Marker manager: follows the game's ward rule for writing to a table, "keep one per duplicate group" only groups markers that are all close to each other (no more chains along a road), long names are no longer cut and rewritten, and saving on a table someone else owns shows what you saved instead of the old list.
- Spawn view: the grid is built over a few frames instead of in one, so big grids don't hitch; zones bordering another biome count that biome's spawns too.
- Freecam no longer turns itself off at full health when food wearing off lowers your max health ("you took damage").
- Marker manager: checkboxes no longer draw a grey bar behind their label, also right after you click them, and ticked ones show green.

## 0.11.2
- Comfort breakdown is a small card on the right, under the status effects it explains, instead of a big box in the middle, with each comfort piece as an icon and its value.
- It only shows while you sit, not every time you walk into your base by a fire.

## 0.11.1
- Area tools: Left Ctrl + scroll now also works with the hoe (its pieces can't rotate, so the wheel zoomed the camera instead).
- Area tools: the hoe and cultivator area grows in even steps (single, 2 × 2, 3 × 3, 4 × 4) instead of jumping from single straight to a very large 3 × 3. New setting: Area Tools → Max hoe size.
- Area tools: the planting grid follows the cursor smoothly, and no longer jumps around as you look about with seeds that re-roll their rotation.
- With the cultivator or hammer out the game hides hover text, so the info box now shows the crop you look at (growing, ready in about 12 min, or why it isn't growing) and the fire you look at (fuel, and why it burns low).

## 0.11.0
New features
- Comfort breakdown: while resting or sitting, lists what adds to your comfort, which pieces don't count because a better one of the same kind is near, and how long Rested will last.
- Crafting station readout: looking at a station shows its level, which extensions count, which are too far or doubled up, and why you can't craft there (no roof, too exposed, no fire).
- Extension preview: while placing a station extension, shows the level it brings the station to, or why it adds nothing.
- Placement reasons: when a piece can't be placed, says why: what it overlaps, too steep, wrong biome, too close to another piece, needs water or ground, and so on.
- Portal pairing: looking at a portal shows where it leads (distance, direction, biome), or why it isn't connected, and warns when 3 or more portals share a tag.
- Base view (F7): F7 now steps through three overlays, Spawns → Repairs → Comfort → off, with one shared legend that shows the current mode.
  - Repairs: damaged pieces tinted yellow, or deep red under 50% health; pieces with no roof (they wear in rain down to 50%) and pieces in water marked; how many pieces are around you and what they're built from.
  - Comfort: the floor coloured by the comfort you'd get standing there, with the best spot ringed.
- Area tools (Left Ctrl + scroll): plant a row of 3 or 5 or a 3 × 3 or 5 × 5 grid of seeds, or level, path, pave and cultivate 3 × 3 tiles, in one click. Each seed or tile costs the same as clicking it, and every one is a normal vanilla placement, so it works on vanilla servers.
- Freecam building (F10): in build mode, fly the camera around your build and place from there. Your character stays put, the camera stays within 25 m, the piece itself must be inside a workbench's range, wards still apply, and taking damage ends it.

New look
- The info box is laid out in columns: icon, label, value and muted detail, with a colour mark for good, warning and bad rows instead of coloured sentences.
- Key hints are drawn as key caps in a footer instead of long text in brackets.
- The box moves itself above the game's build bar and key hints instead of covering them.
- The box eases smoothly between sizes and fades in, instead of jumping each time a row appears or goes.
- Key tips: the first time you build each session, the box lists the mod's keys for a few seconds (Building → Key tips).

Changes
- Spawn view settings moved: the mode, key, legend and display distance are under **Base View**, and Legend scale is now **General → UI scale**. Your old values are carried over.
- The removal lock key is now F6. F9 also switched the game's controller layout. If yours was still F9, it's moved to F6 automatically.
- The comfort preview names the piece that already fills the slot when the one you're placing adds nothing.

## 0.10.0
- Shorter, clearer setting descriptions in the config.
- Hull readout and hull warnings moved to Seaworthy, the sailing mod, with the rest of the ship features. The old Ships settings can be deleted from NorseSense.cfg.

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
