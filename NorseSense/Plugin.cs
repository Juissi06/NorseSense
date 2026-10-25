using System.IO;
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

        // Spawn view
        internal static ConfigEntry<bool> ShowSpawnRadius;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
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

        // Farming
        internal static ConfigEntry<bool> ShowPlantingPreview;
        internal static ConfigEntry<bool> ShowPlantSpacing;

        // Portals
        internal static ConfigEntry<bool> ShowTeleportBlockers;

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

            ModEnabled = Config.Bind("General", "Enabled", true,
                "Turns the whole mod on or off, no restart needed.");

            const string spawn = "Spawn Display";
            ShowSpawnRadius = Config.Bind(spawn, "Enabled", false,
                "Shows where mobs can spawn around you, plus base rings and nests.");
            ToggleKey = Config.Bind(spawn, "Toggle key", new KeyboardShortcut(KeyCode.F7),
                "Turns the spawn view on and off.");
            MaxDisplayDistance = Config.Bind(spawn, "Max display distance", 100f,
                new ConfigDescription("How far away base rings, nests and spawners show up (m).",
                    new AcceptableValueRange<float>(20f, 500f)));
            OverlayRadius = Config.Bind(spawn, "Overlay radius", 32f,
                new ConfigDescription("Size of the ground overlay around you (m). Bigger costs more FPS.",
                    new AcceptableValueRange<float>(10f, 64f)));
            OverlayCellSize = Config.Bind(spawn, "Overlay cell size", 2f,
                new ConfigDescription("Size of each overlay square (m). Smaller is more precise but costs more FPS.",
                    new AcceptableValueRange<float>(1f, 4f)));
            ShowBaseRings = Config.Bind(spawn, "Show base coverage rings", true,
                "Rings around workbenches, fires and other pieces that stop spawns.");
            ShowSpawners = Config.Bind(spawn, "Show nests and spawners", true,
                "Marks greydwarf nests, bone piles and other spawners.");
            PreviewWhileBuilding = Config.Bind(spawn, "Preview while building", true,
                "Shows the area a workbench or fire will protect while you place it.");
            ShowLegend = Config.Bind(spawn, "Show legend", true,
                "Colour legend and coverage stats while the spawn view is on.");
            LegendX = Config.Bind(spawn, "Legend position X", 1f,
                new ConfigDescription("0 = left, 1 = right.",
                    new AcceptableValueRange<float>(0f, 1f)));
            LegendY = Config.Bind(spawn, "Legend position Y", 0.6f,
                new ConfigDescription("0 = top, 1 = bottom.",
                    new AcceptableValueRange<float>(0f, 1f)));
            LegendScale = Config.Bind(spawn, "Legend scale", 1f,
                new ConfigDescription("Size of the legend and build info.", new AcceptableValueRange<float>(0.5f, 2f)));
            ShowOnMap = Config.Bind(spawn, "Show on map", true,
                "Also shows safe and unsafe ground on the minimap and map.");
            MapOverlayRadius = Config.Bind(spawn, "Map overlay radius", 96f,
                new ConfigDescription("How far around you the map overlay checks (m). Only loaded ground works, about 100 m.",
                    new AcceptableValueRange<float>(32f, 160f)));

            const string build = "Building";
            FineRotationModifier = Config.Bind(build, "Fine rotation modifier", KeyCode.LeftAlt,
                "Hold while scrolling to rotate in small steps. None = off.");
            FineRotationStep = Config.Bind(build, "Fine rotation step", 5f,
                new ConfigDescription("Degrees per small step. Vanilla is 22.5.",
                    new AcceptableValueRange<float>(0.5f, 22.5f)));
            MatchRotationKey = Config.Bind(build, "Match rotation key", new KeyboardShortcut(KeyCode.T, KeyCode.LeftAlt),
                "Copies the rotation of the piece you look at. Look at nothing to reset.");
            ShowComfortPreview = Config.Bind(build, "Comfort preview", true,
                "Shows the comfort you'd get after placing a comfort piece.");
            ShowShelterInfo = Config.Bind(build, "Shelter readout", true,
                "Shows if your spot counts as shelter, and if not, which sides are open.");
            ShowTerrainLimits = Config.Bind(build, "Terrain limits", true,
                "Shows how much more you can dig or raise the ground (limit is 8 m).");
            ShowSupportInfo = Config.Bind(build, "Support readout", true,
                "Shows the exact support of the piece you look at and when it breaks.");
            ShowBuildInfo = Config.Bind(build, "Show build info", true,
                "Shows the rotation angle and comfort while building.");
            BuildInfoToggleKey = Config.Bind(build, "Build info toggle key", new KeyboardShortcut(KeyCode.F8),
                "Shows or hides the build info.");
            BuildInfoY = Config.Bind(build, "Build info position Y", 0.78f,
                new ConfigDescription("0 = top, 1 = bottom.",
                    new AcceptableValueRange<float>(0f, 1f)));

            RemovalLockKey = Config.Bind(build, "Removal lock toggle key", new KeyboardShortcut(KeyCode.F9),
                "Locks the hammer so it can't remove pieces. Starts unlocked.");

            const string farming = "Farming";
            ShowPlantingPreview = Config.Bind(farming, "Planting preview", true,
                "Shows if a seed will grow where you aim, and why not.");
            ShowPlantSpacing = Config.Bind(farming, "Show plant spacing", true,
                "Shows the grow space of nearby plants. Red = not growing.");

            ShowTeleportBlockers = Config.Bind("Portals", "Show teleport blockers", true,
                "Lists the items that stop you from using a portal.");

            ShowFireStatus = Config.Bind("Fires", "Show fire status", true,
                "Tells you why a fire won't burn or burns low.");

            const string carto = "Cartography Table";
            MarkerImportMode = Config.Bind(carto, "Import markers", MarkerImport.Filtered,
                "Which shared markers you take from a table. All = vanilla, Filtered = use the filters below, None = map only.");
            SkipCrossedOutMarkers = Config.Bind(carto, "Skip crossed-out markers", true,
                "Filtered: skips crossed-out markers.");
            DuplicateMergeDistance = Config.Bind(carto, "Merge duplicates within", 20f,
                new ConfigDescription("Filtered: skips a marker if one with the same icon is this close (m). 0 = off.",
                    new AcceptableValueRange<float>(0f, 200f)));
            SkipMarkerTypes = Config.Bind(carto, "Skip marker types", "",
                "Filtered: icons to never take, comma-separated (fire, house, hammer, dot, rune, Boss...).");
            ShowImportSummary = Config.Bind(carto, "Show import summary", true,
                "Shows how many markers were skipped or updated.");
            UpdateSharedMarkers = Config.Bind(carto, "Update existing markers", true,
                "Updates shared markers you already have when they change. Your own markers are never touched.");
            EnableMarkerManager = Config.Bind(carto, "Marker manager", true,
                "Hold the modifier and use a cartography table to edit its markers.");
            ManagerModifier = Config.Bind(carto, "Marker manager modifier", KeyCode.LeftShift,
                "Hold this when using a table to open the marker manager.");
            ManagerPermission = Config.Bind(carto, "Edit permission", EditPermission.BuilderOrWard,
                "Who can edit a table's markers. BuilderOrWard = the builder, ward members and admins. Others can still look.");

            BuildStaminaMultiplier = Config.Bind("Stamina", "Building multiplier", 1f,
                new ConfigDescription("Stamina cost of building, removing and repairing. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));
            SowStaminaMultiplier = Config.Bind("Stamina", "Sowing multiplier", 1f,
                new ConfigDescription("Stamina cost of planting. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));

            BuildDistance = Config.Bind("Player", "Build distance", 0f,
                new ConfigDescription("How far you can build (m). 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 20f)));
            PickupRange = Config.Bind("Player", "Auto pickup range", 0f,
                new ConfigDescription("Item pickup radius (m). 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 10f)));

            AutoCompat = Config.Bind("Compatibility", "Auto-adjust for other mods", true,
                "Turns off features another mod already does (like fine rotation with Gizmo).");
            ShowCompatWarnings = Config.Bind("Compatibility", "Show warnings in game", true,
                "Message in game when another mod overlaps. Always written to the log.");

            BuildDistance.SettingChanged += (s, e) => PlayerTweaks.Apply();
            PickupRange.SettingChanged += (s, e) => PlayerTweaks.Apply();

            _spawnViewObject = new GameObject("NorseSense_SpawnView");
            DontDestroyOnLoad(_spawnViewObject);
            _spawnViewObject.AddComponent<SpawnView>();
            _spawnViewObject.AddComponent<MapOverlay>();
            _spawnViewObject.AddComponent<BuildHelpers>();
            _spawnViewObject.AddComponent<PlantPreview>();
            _spawnViewObject.AddComponent<TerrainInfo>();
            _spawnViewObject.AddComponent<MarkerManager>();

            _harmony = new Harmony(Guid);
            ModEnabled.SettingChanged += (s, e) => ApplyEnabled();
            ApplyEnabled();
            Logger.LogInfo($"Loaded. Building stamina x{BuildStaminaMultiplier.Value}, sowing stamina x{SowStaminaMultiplier.Value}, spawn view key: {ToggleKey.Value}");
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
                ShowSpawnRadius.Value = !ShowSpawnRadius.Value;
                Notify("Spawn view", ShowSpawnRadius.Value ? "on" : "off", ShowSpawnRadius.Value);
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
