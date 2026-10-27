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

        // Building
        internal static ConfigEntry<KeyCode> FineRotationModifier;
        internal static ConfigEntry<float> FineRotationStep;
        internal static ConfigEntry<KeyboardShortcut> MatchRotationKey;
        internal static ConfigEntry<bool> ShowComfortPreview;
        internal static ConfigEntry<bool> ShowBuildInfo;
        internal static ConfigEntry<KeyboardShortcut> BuildInfoToggleKey;
        internal static ConfigEntry<float> BuildInfoY;
        internal static ConfigEntry<KeyboardShortcut> RemovalLockKey;
        internal static ConfigEntry<bool> ShowShelterInfo;
        internal static ConfigEntry<bool> ShowTerrainLimits;
        internal static ConfigEntry<bool> ShowSupportInfo;
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
            ShowKeyTips = Bind(general, "Key tips", true, "Lists the mod's keys for a few seconds the first time you build each session.", "Building");
            ShowComfortBreakdown = Bind(general, "Comfort breakdown", true,
                "While sitting, a small card shows what adds to your comfort and how long Rested lasts.", "Resting");
            PickupRange = Bind(general, "Auto pickup range", 0f, "Item pickup radius (m). 0 = vanilla.", "Player",
                new AcceptableValueRange<float>(0f, 10f));
            AutoCompat = Bind(general, "Auto-adjust for other mods", true,
                "Turns off features another mod already does (like fine rotation with Gizmo).", "Compatibility");
            ShowCompatWarnings = Bind(general, "Show warnings in game", true,
                "Message in game when another mod overlaps. Always written to the log.", "Compatibility", advanced: true);

            ToggleKey = Bind(view, "Toggle key", new KeyboardShortcut(KeyCode.F7), "Steps through the base view modes, then off.", "Base View");
            BaseViewModes = Bind(view, "Modes in cycle", ViewModes.All, "Which modes the toggle key steps through before switching off.", "Base View");
            BaseViewMode = Bind(view, "Mode", ViewMode.Off,
                "What the base view shows. Spawns = where monsters can spawn, Repairs = damaged and weathering pieces, Comfort = comfort on the floor around you.",
                "Base View");
            ShowLegend = Bind(view, "Show legend", true, "Colour key and stats while the base view is on.", "Base View");
            MaxDisplayDistance = Bind(view, "Max display distance", 100f, "How far away rings, nests and damaged pieces show up (m).", "Base View",
                new AcceptableValueRange<float>(20f, 500f), true);
            ShowBaseRings = Bind(view, "Show base coverage rings", true, "Spawns: rings around workbenches, fires and other pieces that stop spawns.", "Spawn Display");
            ShowSpawners = Bind(view, "Show nests and spawners", true, "Spawns: marks greydwarf nests, bone piles and other spawners.", "Spawn Display");
            PreviewWhileBuilding = Bind(view, "Preview while building", true, "Spawns: the area a workbench or fire will protect, while you place it.", "Spawn Display");
            ShowOnMap = Bind(view, "Show on map", true, "Spawns: safe and unsafe ground on the minimap and map too.", "Spawn Display");
            OverlayRadius = Bind(view, "Overlay radius", 32f, "Size of the ground overlay around you (m). Bigger costs more FPS.", "Spawn Display",
                new AcceptableValueRange<float>(10f, 64f), true);
            OverlayCellSize = Bind(view, "Overlay cell size", 2f, "Size of each overlay square (m). Smaller is more precise but costs more FPS.", "Spawn Display",
                new AcceptableValueRange<float>(1f, 4f), true);
            MapOverlayRadius = Bind(view, "Map overlay radius", 96f, "How far around you the map overlay checks (m). Only loaded ground works, about 100 m.",
                "Spawn Display", new AcceptableValueRange<float>(32f, 160f), true);
            LegendX = Bind(view, "Legend position X", 1f, "0 = left, 1 = right.", "Base View", new AcceptableValueRange<float>(0f, 1f), true);
            LegendY = Bind(view, "Legend position Y", 0.6f, "0 = top, 1 = bottom.", "Base View", new AcceptableValueRange<float>(0f, 1f), true);

            ShowBuildInfo = Bind(build, "Show build info", true, "The info box while building: rotation, comfort, shelter, support and more.", "Building");
            BuildInfoToggleKey = Bind(build, "Build info toggle key", new KeyboardShortcut(KeyCode.F8), "Shows or hides the build info.", "Building");
            FineRotationModifier = Bind(build, "Fine rotation modifier", KeyCode.LeftAlt, "Hold while scrolling to rotate in small steps. None = off.", "Building");
            FineRotationStep = Bind(build, "Fine rotation step", 5f, "Degrees per small step. Vanilla is 22.5.", "Building",
                new AcceptableValueRange<float>(0.5f, 22.5f), true);
            MatchRotationKey = Bind(build, "Match rotation key", new KeyboardShortcut(KeyCode.T, KeyCode.LeftAlt),
                "Copies the rotation of the piece you look at. Look at nothing to reset.", "Building");
            RemovalLockKey = Bind(build, "Removal lock toggle key", new KeyboardShortcut(KeyCode.F6), "Locks the hammer so it can't remove pieces. Starts unlocked.", "Building");
            ShowComfortPreview = Bind(build, "Comfort preview", true, "The comfort you'd get after placing a comfort piece.", "Building");
            ShowShelterInfo = Bind(build, "Shelter readout", true, "Whether your spot counts as shelter, and if not, which sides are open.", "Building");
            ShowSupportInfo = Bind(build, "Support readout", true, "The exact support of the piece you look at and when it breaks.", "Building");
            ShowStationInfo = Bind(build, "Station readout", true, "A crafting station's level, which extensions count and why others don't.", "Building");
            ShowPlacementReasons = Bind(build, "Placement reasons", true, "Why a piece can't be placed, e.g. what it overlaps.", "Building");
            ShowTerrainLimits = Bind(build, "Terrain limits", true, "How much more you can dig or raise the ground (the limit is 8 m).", "Building");
            ShowFireStatus = Bind(build, "Fire status", true, "Why a fire won't burn or burns low.", "Fires", key: "Show fire status");
            BuildStaminaMultiplier = Bind(build, "Stamina multiplier", 1f, "Stamina cost of building, removing and repairing. 0 = free, 1 = vanilla.",
                "Stamina", new AcceptableValueRange<float>(0f, 1f), key: "Building multiplier");
            BuildDistance = Bind(build, "Build distance", 0f, "How far you can build (m). 0 = vanilla.", "Player", new AcceptableValueRange<float>(0f, 20f));
            BuildInfoY = Bind(build, "Build info position Y", 0.78f, "0 = top, 1 = bottom.", "Building", new AcceptableValueRange<float>(0f, 1f), true);

            FreecamKey = Bind(freecam, "Toggle key", new KeyboardShortcut(KeyCode.F10),
                "In build mode, detaches the camera so you can fly around your build and place from there.", "Freecam");
            FreecamSpeed = Bind(freecam, "Speed", 8f, "Flying speed (m/s). Hold run for 3x.", "Freecam", new AcceptableValueRange<float>(2f, 30f));
            FreecamRange = Bind(freecam, "Max distance", 25f, "How far the camera can fly from you (m).", "Freecam", new AcceptableValueRange<float>(5f, 40f));
            FreecamReach = Bind(freecam, "Reach", 8f, "How far from the camera you can place and remove pieces (m).", "Freecam",
                new AcceptableValueRange<float>(3f, 15f), true);

            ShowPlantingPreview = Bind(farming, "Planting preview", true, "Whether a seed will grow where you aim, and why not.", "Farming");
            ShowPlantSpacing = Bind(farming, "Show plant spacing", true, "The grow space of nearby plants. Red = not growing.", "Farming");
            AreaToolsEnabled = Bind(farming, "Area tools", true,
                "Plant rows and grids of seeds, or hoe a bigger area, in one click. Each seed or tile costs the same as placing it yourself.",
                "Area Tools", key: "Enabled");
            AreaModifier = Bind(farming, "Area size modifier", KeyCode.LeftControl,
                "Hold while scrolling with the cultivator or hoe to change the area size. None = off.", "Area Tools", key: "Size modifier");
            AreaTerrain = Bind(farming, "Bigger hoe and cultivator", true,
                "Lets level, path, paving and cultivate cover more tiles at once. Raise ground always stays single.", "Area Tools");
            SowStaminaMultiplier = Bind(farming, "Stamina multiplier", 1f, "Stamina cost of planting. 0 = free, 1 = vanilla.", "Stamina",
                new AcceptableValueRange<float>(0f, 1f), key: "Sowing multiplier");
            MaxPlantGrid = Bind(farming, "Max planting size", 5, "Largest row or grid side for planting.", "Area Tools",
                new AcceptableValueRange<int>(1, 5), true);
            MaxHoeSize = Bind(farming, "Max hoe size", 4, "Largest hoe and cultivator area, in tiles per side (each tile adds about one hoe radius).",
                "Area Tools", new AcceptableValueRange<int>(1, 4), true);

            ShowPortalPairing = Bind(map, "Portal pairing", true, "Where a portal leads, or why it isn't connected.", "Portals", key: "Show pairing");
            ShowTeleportBlockers = Bind(map, "Teleport blockers", true, "The items that stop you from using a portal.", "Portals", key: "Show teleport blockers");
            EnableMarkerManager = Bind(map, "Marker manager", true, "Hold the modifier and use a cartography table to edit its markers.", "Cartography Table");
            ManagerModifier = Bind(map, "Marker manager modifier", KeyCode.LeftShift, "Hold this when using a table to open the marker manager.", "Cartography Table");
            MarkerImportMode = Bind(map, "Import markers", MarkerImport.Filtered,
                "Which shared markers you take from a table. All = vanilla, Filtered = use the filters below, None = map only.", "Cartography Table");
            SkipCrossedOutMarkers = Bind(map, "Skip crossed-out markers", true, "Filtered: skips crossed-out markers.", "Cartography Table");
            UpdateSharedMarkers = Bind(map, "Update existing markers", true,
                "Updates shared markers you already have when they change. Your own markers are never touched.", "Cartography Table");
            ShowImportSummary = Bind(map, "Show import summary", true, "How many markers were skipped or updated.", "Cartography Table");
            DuplicateMergeDistance = Bind(map, "Merge duplicates within", 20f, "Filtered: skips a marker if one with the same icon is this close (m). 0 = off.",
                "Cartography Table", new AcceptableValueRange<float>(0f, 200f), true);
            SkipMarkerTypes = Bind(map, "Skip marker types", "", "Filtered: icons to never take, comma-separated (fire, house, hammer, dot, rune, Boss...).",
                "Cartography Table", advanced: true);
            ManagerPermission = Bind(map, "Edit permission", EditPermission.BuilderOrWard,
                "Who can edit a table's markers. BuilderOrWard = the builder, ward members and admins. Others can still look.", "Cartography Table", advanced: true);

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
            _spawnViewObject.AddComponent<RepairView>();
            _spawnViewObject.AddComponent<ComfortView>();
            _spawnViewObject.AddComponent<BuildHelpers>();
            _spawnViewObject.AddComponent<PlantPreview>();
            _spawnViewObject.AddComponent<TerrainInfo>();
            _spawnViewObject.AddComponent<PlacementInfo>();
            _spawnViewObject.AddComponent<ComfortInfo>();
            _spawnViewObject.AddComponent<Freecam>();
            _spawnViewObject.AddComponent<AreaTools>();
            _spawnViewObject.AddComponent<MarkerManager>();

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
        internal static void Notify(string what, string state, bool on) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                $"<color={Ui.Gold}>{what}</color>: <color={(on ? Ui.Good : Ui.Muted)}>{state}</color>");

        internal static bool IsTyping() =>
            (Chat.instance != null && Chat.instance.HasFocus()) ||
            Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible();
    }
}
