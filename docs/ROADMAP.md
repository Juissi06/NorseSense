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

## Maybe later
- Translations: about 130 in-game strings would move into one table. Worth it once people ask for a language.
- Grid planting with the cultivator (opt-in, conflicts with Plant Easily).
- Server-controlled settings (ServerSync) for the balance options.

## Not doing
- Sailing overhaul: belongs in its own mod.
- Pickup filters (QuickStackStore), plant/tame hover info (MyLittleUI), weather warning (MyLittleUI).
