# Norse Sense 1.0.0

What 1.0 means: everything the mod page promises works where players use it, which for Valheim is mostly multiplayer.
The features below go into 0.x versions first; 1.0 is the last of them with its bugs fixed. Ideas beyond 1.0 stay in [ROADMAP.md](ROADMAP.md).

## 1. Decide first
- [ ] **Craft from nearby containers**: in before 1.0 (as 0.14), or after it. On or off by default. It's the last big roadmap item, and the riskiest (multiplayer chest ownership, items must never be lost or doubled).
- [ ] **Quick stack** only if craft from containers goes in (they share the "chests I may use" part); off next to Quick Stack Store.

## 2. New features
- [ ] **Craft from nearby containers**, if the decision above says before 1.0.
- [ ] **Support mode** in the base view (F7): every piece tinted by its support at once, not one piece while hovering. Reuses the removal preview's support simulation.
- [ ] **Shelter mode** in the base view: the floor coloured by cover %, to find where "Sheltered" works before placing a bed.
- [ ] **Comfort planner** on the comfort card: which comfort groups are still missing nearby (banner, rug, throne...).
- [ ] **Repair in range**: hold the repair key with the hammer to repair every damaged piece around you, each paid like a click.
- [ ] **Ripe crops** in the farm check: crops ready to harvest get a gold ring.
- [ ] **Ward readout** when walking into a ward area: whose it is and whether you can build there.

## 3. Multiplayer test session (host + a friend, then a dedicated server)
- [ ] **Server limits**: install on a dedicated server, turn limits on, check that a client's freecam range, area sizes, stamina, harvest radius and fly-through-walls are capped, that changing a value mid-game reaches clients, and that a vanilla client can still join.
- [ ] **Dedicated server load**: the plugin loads headless without errors (no player, no camera, no UI).
- [ ] **Area harvest and replant** on crops another player planted, and while that player is near (ownership).
- [ ] **Area tools** (planting grid, hoe sizes, raise grid) seen correctly by the other player and a vanilla player.
- [ ] **Map layers as a client**: wards you're on / not on, someone else's workbench, portals filling in as you travel.
- [ ] **Removal preview** on a building someone else owns.
- [ ] **Marker manager** on a shared table: permissions (builder, ward member, admin), saving while another player reads.
- [ ] **Freecam** near other players' bases with fly-through-walls off and on.
- [ ] **Relog and world switching** without restarting the game (the map-layer bug in 0.13 came from this).

## 4. Polish
- [ ] Daylight screenshots of the new look for the pages (Spawns from above with green, red and the outline).
- [ ] Check the stronger red and yellow in daylight across biomes (Plains, Mistlands, snow).
- [ ] Look at every setting once in Configuration Manager: names, order, which are advanced.
- [ ] Performance in a big base (1000+ pieces): base view, removal preview, map layers on the host.
- [ ] Compatibility pass with the popular mods: MyLittleUI, Quick Stack Store, Gizmo, Plant Easily, Valheim Plus, AzuCraftyBoxes, Jötunn-based content mods.

## 5. Release
- [ ] One or two bugfix-only 0.13.x releases with no new reports.
- [ ] Remove "WIP" from the pages; changelog "1.0.0: what's stable now".
- [ ] Version 1.0.0, tag, zips, pages, then bump to 1.0.1 right away.
