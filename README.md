# Norse Sense

*Common sense for Vikings: see what the game knows but never shows you.*

Norse Sense shows you what Valheim keeps hidden: where mobs can spawn, how much of your base is protected, whether raids can reach you, how much comfort a piece adds and why a portal won't let you through. Spawn-proof your base, build precisely and keep your map tidy.

Vanilla balance: most features only show you information the game already uses. Nothing is unlocked, spawned or made free unless you turn it on yourself.

**Client-side only.** Works on vanilla servers, alongside players without the mod, and does not need to be installed on the server.

## Features

### Base view (F7)
One key for three overlays. **F7** steps through **Spawns → Repairs → Comfort → off**, and the legend shows which mode you're in. You can leave modes out of the cycle in the config.

#### Spawns
Shows where mobs can and can't spawn, using the same checks the game itself uses, so you can spawn-proof without guessing distances.

- **Ground overlay** around you:
  - 🟦 **Protected:** inside the range of a workbench, fire or other base piece. Mobs can't spawn here.
  - 🟥 **Exposed:** mobs can spawn here.
  - 🟨 **Steep:** too steep for most mobs.
  - No colour: covered by a building or rock, so nothing spawns there.
- **Map overlay:** the same colours on the minimap and the large map.
- **Placement preview:** while holding a workbench, fire or other base piece, the area it will protect pulses cyan, even with the base view off.
- **Base rings** around every piece that blocks spawns.
- **Nests, bone piles and creature spawners** are marked. Nests ignore workbenches, so you'll need to destroy them.
- **Raid check:** tells you whether raids can target you where you stand. Raids need at least 3 base pieces within 20 m of you. During a raid, its area is shown as an orange ring.
- **Legend** with coverage % and how much exposed ground is left.

#### Repairs
- **Damaged pieces** glow yellow, or deep red below 50% health, with a count and the lowest health in the legend, so a repair round misses nothing.
- **Weathering:** pieces with no roof above are marked blue, because rain wears them down to 50% health. Pieces standing in water are marked teal. The game never tells you which pieces are exposed.
- **Base summary:** how many pieces are around you and the three materials they use most, e.g. *830 Wood, 96 Stone, 40 Core wood*.

#### Comfort
- The floor around you is coloured by the comfort you'd get standing there: green is the most comfort in view, yellow is less, orange is not sheltered. A ring marks the best spot, so you know where the bed or chair should go.
- The legend compares your spot with the best one and how long Rested would last at each.

### Building
- **Fine rotation:** hold **Left Alt** and scroll to rotate in 5° steps instead of 22.5°. Snapping still works.
- **Match rotation:** **Left Alt + T** copies the exact rotation of the piece you're looking at. Looking at nothing resets fine rotation.
- **Comfort preview:** when placing a chair, banner, rug and so on, shows the comfort level you'd have after placing it, or why it wouldn't add any, e.g. *Raven throne (+3) already counts as your chair*.
- **Shelter readout:** while building under a roof or placing a comfort piece, shows whether your spot counts as shelter, and if not, why: *no roof above* or *open behind and to the left, 71% covered, needs 80%*. Directions are relative to where you look.
- **Terrain limits:** with the hoe or pickaxe, shows how much further the ground can be dug or raised where you aim, e.g. *Ground: 6.2 m below original. Can dig 1.8 m deeper*. The game silently stops at 8 m above or below the original height.
- **Support readout:** looking at a piece that isn't on the ground shows its exact support, e.g. *Wood beam: support 34 / 100 (wood falls below 10)*. The game only tints pieces blue to red.
- **Placement reasons:** when a piece turns red, says why instead of just *Invalid placement*: *overlaps Wood beam*, *surface too steep (42°, max 37°)*, *Bed can't hold other pieces*, *too close to Chopping block*, *only in Plains*.
- **Crafting stations:** looking at a workbench, forge and so on shows its level and which extensions count, e.g. *Level 3 of 5*, *Adze: 5.6 m away, must be within 5 m*, *Doesn't count: second Tanning rack*, *Can't craft: too exposed, cover 55% of 70%*. While placing an extension: *Workbench level 3 » 4*, or why it adds nothing.
- **Removal lock (F6):** stops the hammer from removing pieces, so you can't delete your build by accident.
- **F8** hides or shows the build info box. The box moves itself above the game's own build bar and key hints, so it never covers them.

### Freecam building (F10)
In build mode, press **F10** to detach the camera and fly around your build. Your hammer works from wherever the camera looks, so roofs, overhangs and the far side of a wall are easy to reach.

- Move with your movement keys, **Jump** to go up, **Crouch** to go down, hold **Run** to fly faster.
- Your character stays where it is. The piece must be inside a workbench's range (not just you), wards still apply, and resources still come from your inventory.
- The camera stays within 25 m of you, and you can place and remove pieces up to 8 m from the camera.
- Freecam ends when you leave build mode or take damage.

### Resting
- **Comfort breakdown:** while you sit, a small card under the status effects on the right (below Resting and Rested) shows your comfort, how long Rested will last, each piece that adds to it as an icon with its value, and how many nearby pieces don't count because a better one of the same kind already does. If you're not sheltered, it says which sides are open.

### Farming
- **Planting preview:** while planting with the cultivator, shows whether the seed will grow where you're aiming, and if not, why: *won't grow, too close to Carrot seedling*, *no open sky*, *wrong biome*, *too cold*...
- **Plant spacing rings:** the grow space of the seed and of nearby plants is drawn on the ground. Plants that aren't growing are red.
- **Crop info while farming:** the game hides hover text while the cultivator or hammer is out, so the build info box shows the crop you look at instead: *Growing, ready in about 12 min*, or why it isn't growing. Fires show their fuel and why they burn low.

### Area tools (Left Ctrl + scroll)
Hold **Left Ctrl** and scroll with the cultivator or hoe to work on more than one spot per click.
- **Planting:** a single seed, a row of 3 or 5, or a 3 × 3 or 5 × 5 grid. Seeds are spaced as close as the game still lets them grow. Each spot gets a green or red ring, and red spots are skipped. Rows turn with the piece, so scroll, fine rotation and Left Alt + T (match rotation) line them up with a fence.
- **Hoe and cultivator:** level ground, paths, paving and cultivate grow in steps from a single spot to 2 × 2, 3 × 3 and 4 × 4 tiles, each step about one hoe radius wider. Even sizes grow forward and to the right of where you aim. Levelling flattens the whole area to the height you aim at. Raise ground always stays single, because overlapping raises leave bumps.
- **Same cost as clicking:** every seed or tile takes its own seed, stamina, durability and skill gain. If you run out, it places what you can afford, nearest first.
- Each tile is an ordinary vanilla placement, so other players and vanilla servers see exactly the same result.

### Portals
- **Where it leads:** looking at a connected portal shows how far its partner is, in which direction and in which biome, e.g. *Leads to: 1.4 km north-east, Swamp*.
- **Why it isn't connected:** no other portal with the tag, every match already paired, or 3 or more portals sharing one tag (only two can pair).
- **Teleport blockers:** lists exactly which items in your inventory stop you from teleporting, e.g. *Can't teleport: Copper ore x30, Tin x12*.

### Fires
- Looking at a fire that has fuel but won't burn, or burns low, tells you why: *something is too close above the flames, the smoke can't get out*, *rain and no roof above*, *strong wind and not enough cover*.

### Cartography table
Reading a table normally adds every shared marker to your map. Norse Sense can filter what you take:
- **Skip crossed-out markers** (cleared dungeons, emptied spots).
- **Merge duplicates:** skip markers with the same icon within 20 m of one you already have.
- **Skip whole icon types**, e.g. `fire, dot`.
- **Exploration only:** take just the explored map and no markers.

The explored map is always imported in full, and your own markers are never touched. **Writing** to a table always keeps every marker on it, so other players' markers aren't lost.

Reading also **updates shared markers you already have** when someone renamed, re-iconed or crossed them out. Vanilla never does this.

**Marker manager (Left Shift + E at a table).** A window listing every marker stored on the table:
- Search by name or player, filter by icon, sort by name, distance, player or icon.
- Rename, change icon, cross out or delete markers. Bulk-select to cross out or delete many at once.
- **Possible duplicates** view groups same-icon markers that are close together. **Keep one per duplicate group** cleans them all up in one click.
- Nothing changes until you press **Save to table**. Saving merges your edits into the table's current data, so changes someone else saved meanwhile aren't overwritten. **Undo all** and a discard prompt protect you from mistakes.
- Editing is limited to whoever built the table, players permitted on a ward covering it, and admins. Everyone else can open it to look.

### Optional tweaks (off by default)
- Stamina cost multiplier for building (hammer) and planting (cultivator).
- Build distance and auto-pickup range.

## Keys

| Key | Action |
|---|---|
| F7 | Base view: Spawns, Repairs, Comfort, off |
| F8 | Toggle build info |
| F6 | Toggle removal lock |
| F10 | Freecam building (in build mode) |
| Left Shift + E at a cartography table | Open the marker manager |
| Left Alt + scroll | Fine rotation while building |
| Left Alt + T | Match or reset rotation |
| Left Ctrl + scroll | Area size with the cultivator or hoe |
| Left Ctrl + L | Layout mode: drag the legend, build info and comfort card where you want them |

All keys can be changed in the config.

## Requirements

| | Version | Notes |
|---|---|---|
| **Valheim** | 1.0.20 or newer | Tested on 1.0.20 |
| **BepInExPack for Valheim** | 5.4.2351 or newer | The mod loader. Required. |
| Configuration Manager | any | Optional. Change settings in game with F1. |

Only the players who want the features need it: it's **not required on the server**, and players without it can play alongside you.

## Installation

### With a mod manager (recommended)
- **r2modman / Thunderstore Mod Manager:** search for *Norse Sense* and click Install. BepInEx is installed automatically.
- **Vortex:** click *Mod manager download* on the Nexus page, then enable the mod in Vortex. Install BepInExPack for Valheim the same way if you don't have it.

### Manual
1. Install **BepInExPack for Valheim** if you haven't: extract it into your Valheim folder and start the game once so it creates its folders. To find your Valheim folder, right-click Valheim in Steam → *Manage* → *Browse local files*.
2. Download Norse Sense and extract the zip **into your Valheim folder**. You should end up with `Valheim/BepInEx/plugins/NorseSense/NorseSense.dll`.
3. Start the game. In your base, press **F7** to check it's working.

### Updating
Install the new version over the old one (mod managers do this for you). Your settings are kept.

### Uninstalling
Delete the `BepInEx/plugins/NorseSense` folder, and optionally `BepInEx/config/NorseSense.cfg`. Nothing is saved into your world or character, so it's safe to remove at any time.

### Troubleshooting
- **Nothing happens when pressing F7:** check `BepInEx/LogOutput.log` for `Loading [Norse Sense`. If it's missing, BepInEx isn't installed correctly or the DLL is in the wrong folder.
- **A key does nothing or clashes with another mod:** change it in the config file or with Configuration Manager (F1).
- **Something broke:** report it with your `BepInEx/LogOutput.log` attached.

## Configuration
Settings are in `BepInEx/config/NorseSense.cfg`. With [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) installed, press **F1** to change them in game. Changes take effect immediately.

Settings come in six sections: **1. General**, **2. Base View**, **3. Building**, **4. Freecam**, **5. Farming** and **6. Map and Portals**. Fine-tuning settings (overlay sizes, positions, marker filters) are marked advanced, so Configuration Manager shows them once you tick *Advanced settings*. Panel positions are easiest to set with layout mode (**Left Ctrl + L**).

**1. General → Enabled** switches the whole mod on or off without restarting. Off removes every change Norse Sense makes to the game and hides all its overlays, as if it weren't installed.

## Compatibility
Norse Sense only adds to the game's code and never replaces it, so other mods' changes to the same code still run. When you load into a world, it checks for mods that overlap with its features:

- **Gizmo** (or any mod that handles build rotation): fine rotation turns itself off.
- **Build Camera** and other freecam mods: Norse Sense's freecam turns itself off.
- **Plant Easily, Mass Farming**: area planting turns itself off. **Advanced Terrain Modifiers** and other hoe-radius mods: the bigger hoe turns itself off.
- **Valheim Plus**: build distance and pickup range are left to Valheim Plus.
- **CartographyAdmin**: Norse Sense's marker manager and marker updates turn off.
- **Spawn That, Custom Raids, Expand World** or any mod that changes spawn or raid rules: the legend warns that the overlay may not be exact.
- Any mod that changes how plants grow or how terrain editing works: the planting preview or terrain limits warn that they may not be exact.

Each adjustment is shown once in game and written to the BepInEx log. Turn **Compatibility → Auto-adjust for other mods** off if you want both mods' features running.

Tested alongside MyLittleUI, Quick Stack Store and CameraMod.

## Building from source
Requires the .NET SDK. The project references the game's DLLs from your Valheim install:

```
dotnet build NorseSense/NorseSense.csproj -c Release -p:ValheimDir="D:\Games\Steam\steamapps\common\Valheim"
```

Every build copies the DLL into `BepInEx/plugins`. Release builds also write upload-ready zips for Thunderstore and Nexus to `dist/`. The version is set once in `NorseSense.csproj` (`<Version>`).
