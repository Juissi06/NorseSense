# Roadmap

Ideas for Norse Sense, in rough order. Rules for anything on this list:
- Shows what the game knows but doesn't tell you, or makes building/upkeep less fiddly.
- Vanilla balance. Anything that changes balance is opt-in and off by default.
- Client-side only, works on vanilla servers.
- Doesn't overlap the popular UI mods: MyLittleUI already shows plant growth, taming and fuel; QuickStackStore already filters auto-pickup; MyLittleUI shows weather.

## Next
1. [x] **Planting preview.** While planting, show whether the seed will grow where you aim and why not (too close, no open sky, wrong biome, too cold), with grow-space rings around nearby plants.
2. [x] **Shelter readout.** While building, show cover % where you stand and whether it counts as shelter, so you know why "Sheltered" doesn't show up.
3. [x] **Terrain limits.** With the hoe or pickaxe, show how much further the ground can be raised or lowered here (vanilla caps it at ±8 m from the original height).
4. [x] **Fire status.** Hovering a fire that has fuel but won't burn, or burns low, says why (smoke blocked, buried, rain, wind). MyLittleUI covers fuel.
5. [x] **Support readout.** Exact structural support of the piece you look at while building.
6. [x] **Station extensions.** Station level, which extensions count and why others don't; extension placement preview.
7. [x] **Comfort breakdown.** What adds to comfort while resting, and what's skipped because a better piece of the same kind is near.
8. [x] **Placement reasons.** Name what makes the ghost red instead of "Invalid placement".
9. [x] **Portal pairing.** Where a portal leads, why it isn't connected, warn about shared tags.
10. [x] **Freecam building.** Fly the camera around the build within a set range; building rules still apply where you stand.

## Done: area tools (planting grid and bigger hoe)
One system for both: extra placements of the same vanilla piece around the ghost, each paid for like a normal click.
- **Size:** L Ctrl + scroll while holding the cultivator or hoe. Shown as an "Area" row in the info box with a key-cap hint.
- **Planting grid:** 1x1 up to 5x5, or a row along where you look. Spacing is the closest the game still lets plants grow. Lines up with crops already there. Each cell is checked like the planting preview (green or red ring); bad cells are skipped.
- **Bigger hoe and cultivator:** level, paths, paving and cultivate are covered with several vanilla-sized operations, so every player and a vanilla server see the same result. Level flattens the whole area to the ghost's height. Raise stays single, because overlapping raises make bumps. Can only go bigger than vanilla, not smaller.
- **Cost:** seeds, stamina, durability and skill per seed or tile, same as clicking each one. Places as many as you can afford, nearest first.
- **Why tiles and not a radius setting:** the game sends only the tool's ID, and whoever owns that patch of terrain applies their own radius. A changed radius would only work in single player or when every player has the same setting.
- **Compat:** off if Plant Easily or another grid-planting mod is installed; info only if another mod patches terrain ops.

## Ideas
Grouped by the feature they extend, so the mod gains depth without new screens or hotkeys.

### Base view (grows out of the spawn view, F7)
F7 cycles overlay modes instead of only switching spawns on and off: **Spawns → Repairs → Comfort → off**. One key, one legend that changes with the mode, the same ground and map overlay underneath.
1. [x] **Repairs mode** (merges *weathering* and *damaged pieces*): pieces tinted by health, with the ones that rain or sea water is wearing down marked. Legend: "14 pieces need repair, 6 exposed to rain". The game only shows health dropping, never why.
2. [x] **Comfort mode** (*comfort map*): the ground grid coloured by the comfort you'd get standing there, to find the best spot for a bed or chair. Reuses the comfort breakdown and the spawn grid.
3. [x] **Base summary** in the legend (*build summary*): pieces and materials in the area, e.g. "412 pieces: 830 wood, 96 stone".

### Precision building (grows out of fine rotation, hold L Alt)
Holding the fine-rotation key already means "I'm placing carefully", so it shows the precision helpers together instead of each needing a setting.
4. [x] **Snap points** of the ghost and of the piece you aim at, with the pair that will snap highlighted.
5. [x] **Height** of the ghost above the ground or the piece below, e.g. "2.0 m above Wood floor".

### Support readout
6. **Removal preview**: while aiming at a piece with the hammer, pieces that would fall if you removed it are tinted red. Pairs naturally with the removal lock. Needs a support simulation, so the biggest item here.

### Planting preview
7. [x] **Farm check**: with the cultivator out, every crop nearby gets its grow ring, red with the reason if it isn't growing, not only the ones next to the seed you're placing.

### Map layers (grows out of the spawn map overlay)
Small toggles on the large map instead of separate features:
8. [x] **Workbenches and wards**: build ranges and ward areas, and whether you're permitted on each ward.
9. [x] **Portals**: lines between paired portals with their tags. Complete when hosting; on a dedicated server it fills in as you travel.

### Smoothness
- [x] Info box eases between sizes and fades in instead of jumping when rows change.
- [x] Key tips the first time you build each session.
- [x] Remember the base view mode between sessions.
- Remember the freecam position between uses.
- One "Norse Sense" section at the top of ConfigurationManager with the most-used toggles; the rest grouped under it.

### Containers
10. **Craft from nearby containers.** Workbench, forge and the build menu count materials in chests within range (say 20 m, same base), and take them from there when you craft or build. The recipe list shows how many come from chests. Notes:
    - Not covered by QuickStackStore or MyLittleUI. Off if a craft-from-containers mod is installed (Craft From Containers, AzuCraftyBoxes and so on).
    - Multiplayer: a chest's items can only be changed by whoever owns it, so it has to ask for the chest like opening it does, and skip chests someone else has open. Must stay safe on vanilla servers: never take items that didn't arrive.
    - Respect wards: only chests you could open yourself.
    - Balance: no free items, only fewer trips. Opt-in or on by default is a decision for later.
11. **Quick stack to nearby containers.** One key puts items into nearby chests that already hold the same item. QuickStackStore does exactly this (plus restock and sorting), and many players use it, so: off whenever QuickStackStore or a similar mod is installed, and only worth building if craft from containers is done, since they share the "find nearby chests I may use" part.

### Server settings
12. **Server-enforced settings.** Let a server admin set and lock the balance options for everyone: freecam range and reach, area tool sizes, stamina multipliers, build distance and pickup range. Today each player uses their own config. Needs a sync library (ServerSync or the ConditionalConfigSync already in the mod list). Players without the mod are unaffected, and the readouts stay client-side.

### Improvements to existing features
- **Shelter row:** a small compass graphic with the open sides lit, instead of "Open ahead-right (up), behind-right (up), behind (up)…", which is the longest line in the box.
- **Support row:** a small bar coloured like the game's blue-to-red support tint, next to or instead of "80 / 100".
- **Info box priority:** when many rows are active (freecam, shelter, support, station, placement), keep the urgent ones and fold the rest into one line so the box doesn't grow tall.
- **Repairs mode:** re-tint a piece right after the game's own build-mode highlight resets it, so hovered pieces don't flicker.
- **Freecam:** remember the camera position between uses, and an optional setting that stops it passing through walls.
- **Area tools:** a 1 × 3 row shape for hoe paths (road strips), and new crops lining up with rows already planted.
- [x] **Planting grid spacing:** every seed in a grid must have enough room to grow, also once its neighbours are planted. Today the spacing is the seed's grow radius plus its own collider, which can be too tight. Space by the full grow space instead (about twice the grow radius, or the larger radius when crops differ), re-check each spot with the game's own grow test after the neighbours are placed, and skip or warn about spots that would end up "too close".

### UI and HUD
- **Layout mode:** press a key, drag the info box, legend and comfort card where you want them, and it saves. Replaces the X/Y position sliders and solves most "it covers my other mod" cases.
- **Compact legend:** an option to show only the mode tabs and the bar, for players who know the colours.
- **Scale per panel**, instead of one UI scale for everything.
- **Icons on every row:** Freecam, Area and Removal rows get icons too, so the box lines up.
- **Gamepad:** controller bindings for the new keys (base view, freecam, removal lock, area size), and key hints that show controller buttons when a controller is in use.

## Maybe later
- Translations: about 150 in-game strings would move into one table. Only when people ask for a language.

## Standalone project ideas
Too big or too different for Norse Sense; each would be its own mod and repo.

### Huginn: what am I looking at (like WAILA / WTHIT in Minecraft)
A small panel at the top of the screen that names whatever is under the crosshair and which mod added it.
- **Look-at panel:** name, icon, health, owner, creature level and tame state, growth time. Also for things the game shows no tooltip for: build pieces, rocks, trees, terrain. Raycast code can come from Odin's Eye's Inspect tab and Norse Sense's hover info.
- **Item tooltips:** a "Mod: X" line on inventory items, added alongside MyLittleUI's tooltip.
- **Which mod it's from**, most reliable first:
  1. Snapshot the game's own prefab list before mods add theirs: anything outside it is modded.
  2. Jotunn's registry, which records the source mod of every item, piece and creature it adds.
  3. Track which plugin loads each asset bundle and match prefabs to the bundle they came from.
  4. Custom components: a prefab with a script from a plugin's assembly points to that plugin.
  5. Otherwise "Added by a mod" with no name (mods that clone and rename vanilla prefabs in code).
- **Effort:** about a day for the panel with Vanilla/Modded, 2–3 more days for accurate mod names tested against real modpacks.
- **Before starting:** check Nexus and Thunderstore for an existing Valheim mod that already does this.
- The mod lookup could also feed Odin's Eye's Inspect tab ("from: Mod X").

## Not doing
- Sailing overhaul: belongs in its own mod.
- Pickup filters (QuickStackStore), plant/tame hover info (MyLittleUI), weather warning (MyLittleUI).
