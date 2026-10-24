# Norse Sense

*Common sense for Vikings: see what the game knows but never shows you.*

Norse Sense shows you what Valheim keeps hidden: where mobs can spawn, how much of your base is protected, whether raids can reach you, how much comfort a piece adds and why a portal won't let you through. Spawn-proof your base, build precisely and keep your map tidy.

Vanilla balance: most features only show you information the game already uses. Nothing is unlocked, spawned or made free unless you turn it on yourself.

**Client-side only.** Works on vanilla servers, alongside players without the mod, and does not need to be installed on the server.

## Features

### Spawn view (F7)
Shows where mobs can and can't spawn, using the same checks the game itself uses, so you can spawn-proof without guessing distances.

- **Ground overlay** around you:
  - 🟦 **Protected:** inside the range of a workbench, fire or other base piece. Mobs can't spawn here.
  - 🟥 **Exposed:** mobs can spawn here.
  - 🟨 **Steep:** too steep for most mobs.
  - No colour: covered by a building or rock, so nothing spawns there.
- **Map overlay:** the same colours on the minimap and the large map.
- **Placement preview:** while holding a workbench, fire or other base piece, the area it will protect pulses cyan, even with the spawn view off.
- **Base rings** around every piece that blocks spawns.
- **Nests, bone piles and creature spawners** are marked. Nests ignore workbenches, so you'll need to destroy them.
- **Raid check:** tells you whether raids can target you where you stand. Raids need at least 3 base pieces within 20 m of you. During a raid, its area is shown as an orange ring.
- **Legend** with coverage % and how much exposed ground is left.

### Building
- **Fine rotation:** hold **Left Alt** and scroll to rotate in 5° steps instead of 22.5°. Snapping still works.
- **Match rotation:** **Left Alt + T** copies the exact rotation of the piece you're looking at. Looking at nothing resets fine rotation.
- **Comfort preview:** when placing a chair, banner, rug and so on, shows the comfort level you'd have after placing it, or why it wouldn't add any.
- **Removal lock (F9):** stops the hammer from removing pieces, so you can't delete your build by accident.
- **F8** hides or shows the build info box.

### Portals
- Looking at a portal lists exactly which items in your inventory stop you from teleporting, e.g. *Can't teleport: Copper ore x30, Tin x12*.

### Ships
- **Hull readout** while you're aboard, coloured by condition.
- **Hull warnings:** a warning when the ship takes damage (rate-limited, so Ashlands water doesn't spam you), and a loud one at 50% and 25% hull.

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
| F7 | Toggle spawn view |
| F8 | Toggle build info |
| F9 | Toggle removal lock |
| Left Shift + E at a cartography table | Open the marker manager |
| Left Alt + scroll | Fine rotation while building |
| Left Alt + T | Match or reset rotation |

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
Delete the `BepInEx/plugins/NorseSense` folder, and optionally `BepInEx/config/juissi.norsesense.cfg`. Nothing is saved into your world or character, so it's safe to remove at any time.

### Troubleshooting
- **Nothing happens when pressing F7:** check `BepInEx/LogOutput.log` for `Loading [Norse Sense`. If it's missing, BepInEx isn't installed correctly or the DLL is in the wrong folder.
- **A key does nothing or clashes with another mod:** change it in the config file or with Configuration Manager (F1).
- **Something broke:** report it with your `BepInEx/LogOutput.log` attached.

## Configuration
Settings are in `BepInEx/config/juissi.norsesense.cfg`. With [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) installed, press **F1** to change them in game. Changes take effect immediately.

## Compatibility
Norse Sense only adds to the game's code and never replaces it, so other mods' changes to the same code still run. When you load into a world, it checks for mods that overlap with its features:

- **Gizmo** (or any mod that handles build rotation): fine rotation turns itself off.
- **Valheim Plus**: build distance and pickup range are left to Valheim Plus.
- **CartographyAdmin**: Norse Sense's marker manager and marker updates turn off.
- **Spawn That, Custom Raids, Expand World** or any mod that changes spawn or raid rules: the legend warns that the overlay may not be exact.

Each adjustment is shown once in game and written to the BepInEx log. Turn **Compatibility → Auto-adjust for other mods** off if you want both mods' features running.

Tested alongside MyLittleUI, Quick Stack Store and CameraMod.

## Building from source
Requires the .NET SDK. The project references the game's DLLs from your Valheim install:

```
dotnet build NorseSense/NorseSense.csproj -c Release -p:ValheimDir="D:\Games\Steam\steamapps\common\Valheim"
```

Every build copies the DLL into `BepInEx/plugins`. Release builds also write upload-ready zips for Thunderstore and Nexus to `dist/`. The version is set once in `NorseSense.csproj` (`<Version>`).
