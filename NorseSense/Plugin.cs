using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    [BepInPlugin(Guid, "Norse Sense", ModInfo.Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        // Also names the config file: BepInEx/config/NorseSense.cfg.
        const string Guid = "NorseSense";
        const string OldGuid = "juissi.norsesense"; // 0.8.0 and earlier

        // General
        internal static ConfigEntry<bool> ModEnabled;

        // Base view
        internal static ConfigEntry<ViewMode> BaseViewMode;
        internal static ConfigEntry<ViewModes> BaseViewModes;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;

        // Spawn view
        internal static ConfigEntry<float> MaxDisplayDistance;
        internal static ConfigEntry<float> OverlayRadius;
        internal static ConfigEntry<float> OverlayCellSize;
        internal static ConfigEntry<bool> ShowBaseRings;
        internal static ConfigEntry<bool> ShowSpawners;
        internal static ConfigEntry<bool> PreviewWhileBuilding;
        internal static ConfigEntry<bool> ShowLegend;
        internal static ConfigEntry<float> LegendX;
        internal static ConfigEntry<float> LegendY;
        internal static ConfigEntry<float> LegendScale;
        internal static ConfigEntry<bool> ShowOnMap;
        internal static ConfigEntry<float> MapOverlayRadius;

        // Map layers
        internal static ConfigEntry<bool> MapWards;
        internal static ConfigEntry<bool> MapPortals;

        // Building
        internal static ConfigEntry<KeyCode> FineRotationModifier;
        internal static ConfigEntry<float> FineRotationStep;
        internal static ConfigEntry<KeyboardShortcut> MatchRotationKey;
        internal static ConfigEntry<bool> ShowComfortPreview;
        internal static ConfigEntry<bool> ShowBuildInfo;
        internal static ConfigEntry<KeyboardShortcut> BuildInfoToggleKey;
        internal static ConfigEntry<float> BuildInfoY, BuildInfoX;
        internal static ConfigEntry<float> ComfortX, ComfortY;
        internal static ConfigEntry<KeyboardShortcut> LayoutKey;
        internal static ConfigEntry<bool> WelcomeShown;
        internal static ConfigEntry<KeyboardShortcut> RemovalLockKey;
        internal static ConfigEntry<bool> ShowShelterInfo;
        internal static ConfigEntry<bool> ShowTerrainLimits;
        internal static ConfigEntry<bool> ShowSupportInfo;
        internal static ConfigEntry<bool> ShowRemovalPreview;
        internal static ConfigEntry<bool> ShowStationInfo;
        internal static ConfigEntry<bool> ShowPlacementReasons;
        internal static ConfigEntry<bool> ShowKeyTips;

        // Resting
        internal static ConfigEntry<bool> ShowComfortBreakdown;

        // Freecam
        internal static ConfigEntry<KeyboardShortcut> FreecamKey;
        internal static ConfigEntry<float> FreecamSpeed;
        internal static ConfigEntry<float> FreecamRange;
        internal static ConfigEntry<float> FreecamReach;

        // Area tools
        internal static ConfigEntry<bool> AreaToolsEnabled;
        internal static ConfigEntry<KeyCode> AreaModifier;
        internal static ConfigEntry<int> MaxPlantGrid;
        internal static ConfigEntry<bool> AreaTerrain;
        internal static ConfigEntry<int> MaxHoeSize;

        // Farming
        internal static ConfigEntry<bool> ShowPlantingPreview;
        internal static ConfigEntry<bool> ShowPlantSpacing;
        internal static ConfigEntry<bool> ShowFarmCheck;
        internal static ConfigEntry<bool> HarvestArea;
        internal static ConfigEntry<KeyCode> HarvestKey;
        internal static ConfigEntry<float> HarvestRadius;
        internal static ConfigEntry<bool> Replant;

        // Portals
        internal static ConfigEntry<bool> ShowTeleportBlockers;
        internal static ConfigEntry<bool> ShowPortalPairing;

        // Fires
        internal static ConfigEntry<bool> ShowFireStatus;


        // Cartography
        internal static ConfigEntry<MarkerImport> MarkerImportMode;
        internal static ConfigEntry<bool> SkipCrossedOutMarkers;
        internal static ConfigEntry<float> DuplicateMergeDistance;
        internal static ConfigEntry<string> SkipMarkerTypes;
        internal static ConfigEntry<bool> ShowImportSummary;
        internal static ConfigEntry<bool> UpdateSharedMarkers;
        internal static ConfigEntry<bool> EnableMarkerManager;
        internal static ConfigEntry<KeyCode> ManagerModifier;
        internal static ConfigEntry<EditPermission> ManagerPermission;

        // Stamina
        internal static ConfigEntry<float> BuildStaminaMultiplier;
        internal static ConfigEntry<float> SowStaminaMultiplier;

        // Player
        internal static ConfigEntry<float> BuildDistance;
        internal static ConfigEntry<float> PickupRange;

        // Compatibility
        internal static ConfigEntry<bool> AutoCompat;
        internal static ConfigEntry<bool> ShowCompatWarnings;

        // Server
        internal static ConfigEntry<bool> LimitPlayers;
        internal static ConfigEntry<bool> FreecamAllowed;

        Harmony _harmony;
        GameObject _spawnViewObject;

        void Awake()
        {
            MigrateOldConfig();

            // Six sections, numbered so ConfigurationManager lists them in this order; settings appear in the order
            // they're bound here. Fiddly ones are marked advanced, so they stay out of the way until asked for.
            // The last argument is where a setting lived before 0.12.0: its value there is carried over once.
            const string general = "1. General", view = "2. Base View", build = "3. Building", freecam = "4. Freecam",
                farming = "5. Farming", map = "6. Map and Portals";

            ModEnabled = Bind(general, "Enabled", true, "Turns the whole mod on or off, no restart needed.", "General");
            LegendScale = Bind(general, "UI scale", 1f, "Size of the legend, info box and other panels.", "General",
                new AcceptableValueRange<float>(0.5f, 2f));
            LayoutKey = Bind(general, "Layout key", new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl),
                "Move the panels: drag the legend, build info and comfort card where you want them. The key again or Esc when done.", "General");
            ShowKeyTips = Bind(general, "Key tips", true, "Lists the mod's keys for a few seconds the first time you build each session.", "Building", advanced: true);
            ShowComfortBreakdown = Bind(general, "Comfort breakdown", true,
                "While sitting, a small card shows what adds to your comfort and how long Rested lasts.", "Resting");
            PickupRange = Bind(general, "Auto pickup range", 0f, "Item pickup radius (m). 0 = vanilla.", "Player",
                new AcceptableValueRange<float>(0f, 10f), advanced: true);
            ComfortX = Bind(general, "Comfort card position X", -1f, "Set by layout mode. -1 = automatic, under the status effects.", "General",
                new AcceptableValueRange<float>(-1f, 1f), true);
            ComfortY = Bind(general, "Comfort card position Y", -1f, "Set by layout mode. -1 = automatic, under the status effects.", "General",
                new AcceptableValueRange<float>(-1f, 1f), true);
            AutoCompat = Bind(general, "Auto-adjust for other mods", true,
                "Turns off features another mod already does (like fine rotation with Gizmo).", "Compatibility");
            ShowCompatWarnings = Bind(general, "Show warnings in game", true,
                "Message in game when another mod overlaps. Always written to the log.", "Compatibility", advanced: true);

            LimitPlayers = Bind(general, "Server limits", false,
                "When you host or run a server: players with Norse Sense can't go past your freecam, area tool, stamina, build distance and pickup settings. " +
                "They can still pick less. No effect on players without the mod.", general);

            ToggleKey = Bind(view, "Toggle key", new KeyboardShortcut(KeyCode.F7), "Steps through the base view modes, then off.", "Base View");
            BaseViewModes = Bind(view, "Modes in cycle", ViewModes.All, "Which modes the toggle key steps through before switching off.", "Base View", advanced: true);
            BaseViewMode = Bind(view, "Mode", ViewMode.Off,
                "What the base view shows. Spawns = where monsters can spawn, Repairs = damaged and weathering pieces, Comfort = comfort on the floor around you.",
                "Base View", advanced: true);
            ShowLegend = Bind(view, "Show legend", true, "Colour key and stats while the base view is on.", "Base View");
            MaxDisplayDistance = Bind(view, "Max display distance", 100f, "How far away rings, nests and damaged pieces show up (m).", "Base View",
                new AcceptableValueRange<float>(20f, 500f), true);
            ShowBaseRings = Bind(view, "Show base coverage rings", true, "Spawns: rings around workbenches, fires and other pieces that stop spawns.", "Spawn Display", advanced: true);
            ShowSpawners = Bind(view, "Show nests and spawners", true, "Spawns: marks greydwarf nests, bone piles and other spawners.", "Spawn Display", advanced: true);
            PreviewWhileBuilding = Bind(view, "Preview while building", true, "Spawns: the area a workbench or fire will protect, while you place it.", "Spawn Display", advanced: true);
            ShowOnMap = Bind(view, "Show on map", true, "Spawns: safe and unsafe ground on the minimap and map too.", "Spawn Display", advanced: true);
            OverlayRadius = Bind(view, "Overlay radius", 32f, "Size of the ground overlay around you (m). Bigger costs more FPS.", "Spawn Display",
                new AcceptableValueRange<float>(10f, 64f), true);
            OverlayCellSize = Bind(view, "Overlay cell size", 2f, "Size of each overlay square (m). Smaller is more precise but costs more FPS.", "Spawn Display",
                new AcceptableValueRange<float>(1f, 4f), true);
            MapOverlayRadius = Bind(view, "Map overlay radius", 96f, "How far around you the map overlay checks (m). Only loaded ground works, about 100 m.",
                "Spawn Display", new AcceptableValueRange<float>(32f, 160f), true);
            LegendX = Bind(view, "Legend position X", 1f, "0 = left, 1 = right. Easiest set with layout mode.", "Base View", new AcceptableValueRange<float>(0f, 1f), true);
            LegendY = Bind(view, "Legend position Y", 0.6f, "0 = top, 1 = bottom. Easiest set with layout mode.", "Base View", new AcceptableValueRange<float>(0f, 1f), true);

            ShowBuildInfo = Bind(build, "Show build info", true, "The info box while building: rotation, comfort, shelter, support and more.", "Building");
            BuildInfoToggleKey = Bind(build, "Build info toggle key", new KeyboardShortcut(KeyCode.F8), "Shows or hides the build info.", "Building");
            FineRotationModifier = Bind(build, "Fine rotation modifier", KeyCode.LeftAlt, "Hold while scrolling to rotate in small steps. None = off.", "Building");
            FineRotationStep = Bind(build, "Fine rotation step", 5f, "Degrees per small step. Vanilla is 22.5.", "Building",
                new AcceptableValueRange<float>(0.5f, 22.5f), true);
            MatchRotationKey = Bind(build, "Match rotation key", new KeyboardShortcut(KeyCode.T, KeyCode.LeftAlt),
                "Copies the rotation of the piece you look at. Look at nothing to reset.", "Building");
            RemovalLockKey = Bind(build, "Removal lock toggle key", new KeyboardShortcut(KeyCode.F6), "Locks the hammer so it can't remove pieces. Starts unlocked.", "Building");
            ShowComfortPreview = Bind(build, "Comfort preview", true, "The comfort you'd get after placing a comfort piece.", "Building", advanced: true);
            ShowShelterInfo = Bind(build, "Shelter readout", true, "Whether your spot counts as shelter, and if not, which sides are open.", "Building", advanced: true);
            ShowSupportInfo = Bind(build, "Support readout", true, "The exact support of the piece you look at and when it breaks.", "Building", advanced: true);
            ShowRemovalPreview = Bind(build, "Removal preview", true,
                "Hold the fine-rotation key (or lock removal) and aim at a piece: the pieces that would fall if you removed it are tinted red.", build, advanced: true);
            ShowStationInfo = Bind(build, "Station readout", true, "A crafting station's level, which extensions count and why others don't.", "Building", advanced: true);
            ShowPlacementReasons = Bind(build, "Placement reasons", true, "Why a piece can't be placed, e.g. what it overlaps.", "Building", advanced: true);
            ShowTerrainLimits = Bind(build, "Terrain limits", true, "How much more you can dig or raise the ground (the limit is 8 m).", "Building", advanced: true);
            ShowFireStatus = Bind(build, "Fire status", true, "Why a fire won't burn or burns low.", "Fires", key: "Show fire status", advanced: true);
            BuildStaminaMultiplier = Bind(build, "Stamina multiplier", 1f, "Stamina cost of building, removing and repairing. 0 = free, 1 = vanilla.",
                "Stamina", new AcceptableValueRange<float>(0f, 1f), key: "Building multiplier");
            BuildDistance = Bind(build, "Build distance", 0f, "How far you can build (m). 0 = vanilla.", "Player", new AcceptableValueRange<float>(0f, 20f));
            BuildInfoX = Bind(build, "Build info position X", 0.5f, "0 = left, 1 = right. Easiest set with layout mode.", "Building",
                new AcceptableValueRange<float>(0f, 1f), true);
            BuildInfoY = Bind(build, "Build info position Y", 0.78f, "0 = top, 1 = bottom. Easiest set with layout mode.", "Building",
                new AcceptableValueRange<float>(0f, 1f), true);

            FreecamKey = Bind(freecam, "Toggle key", new KeyboardShortcut(KeyCode.F10),
                "In build mode, detaches the camera so you can fly around your build and place from there.", "Freecam");
            FreecamAllowed = Bind(freecam, "Enabled", true, "Freecam on or off. With server limits, off here turns it off for everyone.", freecam);
            FreecamSpeed = Bind(freecam, "Speed", 8f, "Flying speed (m/s). Hold run for 3x.", "Freecam", new AcceptableValueRange<float>(2f, 30f));
            FreecamRange = Bind(freecam, "Max distance", 25f, "How far the camera can fly from you (m).", "Freecam", new AcceptableValueRange<float>(5f, 40f));
            FreecamReach = Bind(freecam, "Reach", 8f, "How far from the camera you can place and remove pieces (m).", "Freecam",
                new AcceptableValueRange<float>(3f, 15f), true);

            ShowPlantingPreview = Bind(farming, "Planting preview", true, "Whether a seed will grow where you aim, and why not.", "Farming", advanced: true);
            ShowPlantSpacing = Bind(farming, "Show plant spacing", true, "The grow space of nearby plants. Red = not growing.", "Farming", advanced: true);
            HarvestArea = Bind(farming, "Harvest area", true,
                "Hold the harvest key while picking a crop and every ripe crop of the same kind around it is picked too.", farming);
            HarvestKey = Bind(farming, "Harvest key", KeyCode.LeftAlt, "Hold while picking to harvest an area. None = off.", farming);
            HarvestRadius = Bind(farming, "Harvest radius", 5f, "How far around the picked crop the area harvest reaches (m).", farming,
                new AcceptableValueRange<float>(1f, 10f), true);
            Replant = Bind(farming, "Replant", true,
                "After an area harvest, plant the same crop again where it was, if you carry a cultivator and seeds. Costs the same as planting.", farming);
            ShowFarmCheck = Bind(farming, "Farm check", true,
                "With the cultivator out, every crop around you gets its grow ring, and the ones that aren't growing say why.", farming, advanced: true);
            AreaToolsEnabled = Bind(farming, "Area tools", true,
                "Plant rows and grids of seeds, or hoe a bigger area, in one click. Each seed or tile costs the same as placing it yourself.",
                "Area Tools", key: "Enabled");
            AreaModifier = Bind(farming, "Area size modifier", KeyCode.LeftControl,
                "Hold while scrolling with the cultivator or hoe to change the area size. None = off.", "Area Tools", key: "Size modifier");
            AreaTerrain = Bind(farming, "Bigger hoe and cultivator", true,
                "Lets level, raise, path, paving and cultivate cover more tiles at once. A raised area is levelled flat afterwards.", "Area Tools");
            SowStaminaMultiplier = Bind(farming, "Stamina multiplier", 1f, "Stamina cost of planting. 0 = free, 1 = vanilla.", "Stamina",
                new AcceptableValueRange<float>(0f, 1f), key: "Sowing multiplier");
            MaxPlantGrid = Bind(farming, "Max planting size", 5, "Largest row or grid side for planting.", "Area Tools",
                new AcceptableValueRange<int>(1, 5), true);
            MaxHoeSize = Bind(farming, "Max hoe size", 4, "Largest hoe and cultivator area, in tiles per side (each tile adds about one hoe radius).",
                "Area Tools", new AcceptableValueRange<int>(1, 4), true);

            MapWards = Bind(map, "Map: wards and stations", true,
                "On the large map: ward areas (green = you may build, red = you may not, grey = off) and crafting stations' build range. Also a toggle on the map.",
                map, advanced: true);
            MapPortals = Bind(map, "Map: portals", true,
                "On the large map: a line between each pair of portals, unpaired ones in orange, tags when zoomed in. Also a toggle on the map.", map, advanced: true);
            ShowPortalPairing = Bind(map, "Portal pairing", true, "Where a portal leads, or why it isn't connected.", "Portals", key: "Show pairing", advanced: true);
            ShowTeleportBlockers = Bind(map, "Teleport blockers", true, "The items that stop you from using a portal.", "Portals", key: "Show teleport blockers", advanced: true);
            EnableMarkerManager = Bind(map, "Marker manager", true, "Hold the modifier and use a cartography table to edit its markers.", "Cartography Table");
            ManagerModifier = Bind(map, "Marker manager modifier", KeyCode.LeftShift, "Hold this when using a table to open the marker manager.", "Cartography Table", advanced: true);
            MarkerImportMode = Bind(map, "Import markers", MarkerImport.Filtered,
                "Which shared markers you take from a table. All = vanilla, Filtered = use the filters below, None = map only.", "Cartography Table");
            SkipCrossedOutMarkers = Bind(map, "Skip crossed-out markers", true, "Filtered: skips crossed-out markers.", "Cartography Table", advanced: true);
            UpdateSharedMarkers = Bind(map, "Update existing markers", true,
                "Updates shared markers you already have when they change. Your own markers are never touched.", "Cartography Table", advanced: true);
            ShowImportSummary = Bind(map, "Show import summary", true, "How many markers were skipped or updated.", "Cartography Table", advanced: true);
            DuplicateMergeDistance = Bind(map, "Merge duplicates within", 20f, "Filtered: skips a marker if one with the same icon is this close (m). 0 = off.",
                "Cartography Table", new AcceptableValueRange<float>(0f, 200f), true);
            SkipMarkerTypes = Bind(map, "Skip marker types", "", "Filtered: icons to never take, comma-separated (fire, house, hammer, dot, rune, Boss...).",
                "Cartography Table", advanced: true);
            ManagerPermission = Bind(map, "Edit permission", EditPermission.BuilderOrWard,
                "Who can edit a table's markers. BuilderOrWard = the builder, ward members and admins. Others can still look.", "Cartography Table", advanced: true);

            WelcomeShown = Config.Bind(general, "Welcome shown", false,
                new ConfigDescription("The one-time welcome with the main keys has been shown.", null,
                    new ConfigurationManagerAttributes { Browsable = false }));

            CarryOverMovedSettings();

            // Until 0.11.0 this defaulted to F9, which the game also uses to switch controller layout.
            if (RemovalLockKey.Value.MainKey == KeyCode.F9 && !RemovalLockKey.Value.Modifiers.Any())
            {
                RemovalLockKey.Value = new KeyboardShortcut(KeyCode.F6);
                Logger.LogInfo("Removal lock key moved from F9 to F6: the game uses F9 to switch controller layout.");
            }

            BuildDistance.SettingChanged += (s, e) => PlayerTweaks.Apply();
            PickupRange.SettingChanged += (s, e) => PlayerTweaks.Apply();

            _spawnViewObject = new GameObject("NorseSense_SpawnView");
            DontDestroyOnLoad(_spawnViewObject);
            _spawnViewObject.AddComponent<SpawnView>();
            _spawnViewObject.AddComponent<MapOverlay>();
            _spawnViewObject.AddComponent<MapLayers>();
            _spawnViewObject.AddComponent<RepairView>();
            _spawnViewObject.AddComponent<ComfortView>();
            _spawnViewObject.AddComponent<BuildHelpers>();
            _spawnViewObject.AddComponent<PrecisionInfo>();
            _spawnViewObject.AddComponent<RemovalPreview>();
            _spawnViewObject.AddComponent<PlantPreview>();
            _spawnViewObject.AddComponent<TerrainInfo>();
            _spawnViewObject.AddComponent<PlacementInfo>();
            _spawnViewObject.AddComponent<ComfortInfo>();
            _spawnViewObject.AddComponent<Freecam>();
            _spawnViewObject.AddComponent<AreaTools>();
            _spawnViewObject.AddComponent<MassFarming>();
            _spawnViewObject.AddComponent<MarkerManager>();
            _spawnViewObject.AddComponent<LayoutMode>();
            _spawnViewObject.AddComponent<ServerLimits>();

            _harmony = new Harmony(Guid);
            ModEnabled.SettingChanged += (s, e) => ApplyEnabled();
            ApplyEnabled();
            Logger.LogInfo($"Loaded. Building stamina x{BuildStaminaMultiplier.Value}, sowing stamina x{SowStaminaMultiplier.Value}, spawn view key: {ToggleKey.Value}");
        }

        // Where each setting used to live, for CarryOverMovedSettings.
        readonly List<KeyValuePair<ConfigDefinition, ConfigEntryBase>> _moved = new List<KeyValuePair<ConfigDefinition, ConfigEntryBase>>();
        int _order = 1000;

        ConfigEntry<T> Bind<T>(string section, string name, T value, string description, string oldSection,
            AcceptableValueBase range = null, bool advanced = false, string key = null)
        {
            var attributes = new ConfigurationManagerAttributes { Order = _order--, IsAdvanced = advanced ? true : (bool?)null };
            var entry = Config.Bind(section, name, value, new ConfigDescription(description, range, attributes));
            _moved.Add(new KeyValuePair<ConfigDefinition, ConfigEntryBase>(new ConfigDefinition(oldSection, key ?? name), entry));
            return entry;
        }

        // 0.11.0 and 0.12.0 moved settings. Values still in the file under their old place are carried over once.
        void CarryOverMovedSettings()
        {
            Dictionary<ConfigDefinition, string> orphans;
            try
            {
                orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config) as Dictionary<ConfigDefinition, string>;
            }
            catch (Exception)
            {
                return;
            }
            if (orphans == null || orphans.Count == 0) return;

            bool moved = false;
            void Carry(ConfigDefinition old, ConfigEntryBase entry)
            {
                if (!orphans.TryGetValue(old, out string value)) return;
                try { entry.SetSerializedValue(value); }
                catch (Exception) { /* unreadable old value: keep the default */ }
                orphans.Remove(old);
                moved = true;
            }

            // Before 0.11.0 the base view settings were under Spawn Display, with an on/off instead of a mode.
            const string spawn = "Spawn Display";
            var oldEnabled = new ConfigDefinition(spawn, "Enabled");
            if (orphans.TryGetValue(oldEnabled, out string enabled))
            {
                if (enabled.Trim().ToLowerInvariant() == "true") BaseViewMode.Value = ViewMode.Spawns;
                orphans.Remove(oldEnabled);
                moved = true;
            }
            Carry(new ConfigDefinition(spawn, "Toggle key"), ToggleKey);
            Carry(new ConfigDefinition(spawn, "Max display distance"), MaxDisplayDistance);
            Carry(new ConfigDefinition(spawn, "Show legend"), ShowLegend);
            Carry(new ConfigDefinition(spawn, "Legend position X"), LegendX);
            Carry(new ConfigDefinition(spawn, "Legend position Y"), LegendY);
            Carry(new ConfigDefinition(spawn, "Legend scale"), LegendScale);

            foreach (var m in _moved) Carry(m.Key, m.Value);

            // Ship settings from before the ship features moved to Seaworthy: nothing reads them any more.
            foreach (var old in new List<ConfigDefinition>(orphans.Keys))
                if (old.Section == "Ships")
                {
                    orphans.Remove(old);
                    moved = true;
                }

            if (!moved) return;
            Config.Save();
            Logger.LogInfo("Moved settings to their new sections.");
        }

        // Keep players' settings from the old juissi.norsesense.cfg the first time they start with the new name.
        void MigrateOldConfig()
        {
            string oldPath = Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
            if (!File.Exists(oldPath) || File.Exists(Config.ConfigFilePath)) return;
            try
            {
                File.Copy(oldPath, Config.ConfigFilePath);
                Config.Reload();
                Logger.LogInfo($"Moved settings from {OldGuid}.cfg to {Path.GetFileName(Config.ConfigFilePath)}. The old file can be deleted.");
            }
            catch (IOException e)
            {
                Logger.LogWarning("Couldn't copy the old settings file: " + e.Message);
            }
        }

        bool _patched;

        // Patches and components follow the master switch, so "off" really is vanilla.
        void ApplyEnabled()
        {
            bool on = ModEnabled.Value;
            if (on && !_patched) _harmony.PatchAll(typeof(Plugin).Assembly);
            else if (!on && _patched)
            {
                _harmony.UnpatchSelf();
                RemovalLockPatch.Locked = false;
                BuildHelpers.FineOffset = 0f;
            }
            _patched = on;
            _spawnViewObject.SetActive(on);
            PlayerTweaks.Apply();
            if (Player.m_localPlayer != null) Notify("Norse Sense", on ? "on" : "off", on);
            Logger.LogInfo(on ? "Enabled." : "Disabled: all patches removed.");
        }

        void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_spawnViewObject != null) Destroy(_spawnViewObject);
        }

        void Update()
        {
            // Every plugin has loaded and patched by the time a player exists.
            if (!Compat.Checked && ModEnabled.Value && Player.m_localPlayer != null) Compat.Run(Guid, Logger);

            PlayerTweaks.Update();
            if (!ModEnabled.Value || Player.m_localPlayer == null) return;
            Ui.PrepareArt(); // once: the game's UI sprites are loaded by the time a player exists
            Welcome();
            if (IsTyping()) return;

            if (ToggleKey.Value.IsDown())
            {
                BaseView.Cycle();
            }
            if (BuildInfoToggleKey.Value.IsDown())
            {
                ShowBuildInfo.Value = !ShowBuildInfo.Value;
                Notify("Build info", ShowBuildInfo.Value ? "shown" : "hidden", ShowBuildInfo.Value);
            }
            if (RemovalLockKey.Value.IsDown())
            {
                RemovalLockPatch.Locked = !RemovalLockPatch.Locked;
                Notify("Piece removal", RemovalLockPatch.Locked ? "locked" : "unlocked", RemovalLockPatch.Locked);
            }
        }

        // Toggle feedback in one consistent style: "Spawn view: on".
        float _inWorldSince = -1f;

        // Once per install, a few seconds after first arriving in a world: the three keys that open everything.
        void Welcome()
        {
            if (WelcomeShown.Value) return;
            // Not during the Valkyrie intro or a cutscene, nor with the HUD hidden: the clock starts once it can be seen.
            var player = Player.m_localPlayer;
            if (player.InIntro() || player.InCutscene() || Hud.IsUserHidden())
            {
                _inWorldSince = -1f;
                return;
            }
            if (_inWorldSince < 0f) _inWorldSince = Time.time;
            if (Time.time - _inWorldSince < 6f) return;
            WelcomeShown.Value = true;
            Tell("Norse Sense", $"{Ui.KeyName(ToggleKey.Value)} base view, {Ui.KeyName(BuildInfoToggleKey.Value)} build info, " +
                                $"{Ui.KeyName(FreecamKey.Value)} freecam, {Ui.KeyName(LayoutKey.Value)} move panels", Tone.Good);
        }

        // Every message the mod shows looks the same: the gold subject, then a short phrase, coloured by how much
        // it matters. In the corner, like the game's own pickup messages; Center only for a blocked action.
        internal static void Tell(string what, string text, Tone tone = Tone.None, bool center = false)
        {
            string colour = tone == Tone.Good ? Ui.Good : tone == Tone.Warn ? Ui.Warn : tone == Tone.Bad ? Ui.Bad : Ui.Muted;
            Player.m_localPlayer?.Message(center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft,
                $"<color={Ui.Gold}>{what}</color>: <color={colour}>{text}</color>");
        }

        // A switch turned on or off.
        internal static void Notify(string what, string state, bool on) => Tell(what, state, on ? Tone.Good : Tone.None);

        internal static bool IsTyping() =>
            (Chat.instance != null && Chat.instance.HasFocus()) ||
            Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible();
    }
}
