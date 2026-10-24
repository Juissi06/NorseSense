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
        const string Guid = "juissi.norsesense";

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

        // Farming
        internal static ConfigEntry<bool> ShowPlantingPreview;
        internal static ConfigEntry<bool> ShowPlantSpacing;

        // Portals
        internal static ConfigEntry<bool> ShowTeleportBlockers;

        // Ships
        internal static ConfigEntry<bool> ShowHullReadout;
        internal static ConfigEntry<bool> HullWarnings;
        internal static ConfigEntry<float> HullWarnLevel;
        internal static ConfigEntry<float> HullCriticalLevel;
        internal static ConfigEntry<float> HullReadoutY;

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
            const string spawn = "Spawn Display";
            ShowSpawnRadius = Config.Bind(spawn, "Enabled", false,
                "Show the spawn view: a ground overlay of where mobs can spawn, base coverage rings, and nests/spawners.");
            ToggleKey = Config.Bind(spawn, "Toggle key", new KeyboardShortcut(KeyCode.F7),
                "Key that toggles the spawn view.");
            MaxDisplayDistance = Config.Bind(spawn, "Max display distance", 100f,
                new ConfigDescription("Show base rings, nests and spawners this close to you.",
                    new AcceptableValueRange<float>(20f, 500f)));
            OverlayRadius = Config.Bind(spawn, "Overlay radius", 32f,
                new ConfigDescription("Radius in meters of the coloured ground overlay around you. Larger costs more performance.",
                    new AcceptableValueRange<float>(10f, 64f)));
            OverlayCellSize = Config.Bind(spawn, "Overlay cell size", 2f,
                new ConfigDescription("Size in meters of each overlay square. Smaller is more precise but costs more performance.",
                    new AcceptableValueRange<float>(1f, 4f)));
            ShowBaseRings = Config.Bind(spawn, "Show base coverage rings", true,
                "Draw a ring around every workbench, fire and other piece that stops mobs from spawning.");
            ShowSpawners = Config.Bind(spawn, "Show nests and spawners", true,
                "Mark greydwarf nests, bone piles and fixed creature spawners.");
            PreviewWhileBuilding = Config.Bind(spawn, "Preview while building", true,
                "While placing a workbench, fire or other base piece, show the area it will protect, even when the spawn view is off.");
            ShowLegend = Config.Bind(spawn, "Show legend", true,
                "Show the colour legend and coverage stats while the spawn view is on.");
            LegendX = Config.Bind(spawn, "Legend position X", 1f,
                new ConfigDescription("Horizontal legend position. 0 = left edge, 1 = right edge.",
                    new AcceptableValueRange<float>(0f, 1f)));
            LegendY = Config.Bind(spawn, "Legend position Y", 0.6f,
                new ConfigDescription("Vertical legend position. 0 = top edge, 1 = bottom edge.",
                    new AcceptableValueRange<float>(0f, 1f)));
            LegendScale = Config.Bind(spawn, "Legend scale", 1f,
                new ConfigDescription("Size of the legend and build info text.", new AcceptableValueRange<float>(0.5f, 2f)));
            ShowOnMap = Config.Bind(spawn, "Show on map", true,
                "Also paint protected/exposed ground on the minimap and the large map while the spawn view is on.");
            MapOverlayRadius = Config.Bind(spawn, "Map overlay radius", 96f,
                new ConfigDescription("Radius in meters scanned for the map overlay. Only loaded terrain (roughly 100 m around you) can be checked.",
                    new AcceptableValueRange<float>(32f, 160f)));

            const string build = "Building";
            FineRotationModifier = Config.Bind(build, "Fine rotation modifier", KeyCode.LeftAlt,
                "Hold this while scrolling (or rotating on a gamepad) to rotate pieces in fine steps. None = disabled.");
            FineRotationStep = Config.Bind(build, "Fine rotation step", 5f,
                new ConfigDescription("Degrees per fine rotation step. Vanilla steps are 22.5.",
                    new AcceptableValueRange<float>(0.5f, 22.5f)));
            MatchRotationKey = Config.Bind(build, "Match rotation key", new KeyboardShortcut(KeyCode.T, KeyCode.LeftAlt),
                "While building: copy the exact rotation of the piece you're looking at, or reset fine rotation when looking at nothing.");
            ShowComfortPreview = Config.Bind(build, "Comfort preview", true,
                "When placing a comfort piece (chair, banner, fire...), show the comfort level you'd have here after placing it.");
            ShowBuildInfo = Config.Bind(build, "Show build info", true,
                "Show the rotation angle and comfort preview while building.");
            BuildInfoToggleKey = Config.Bind(build, "Build info toggle key", new KeyboardShortcut(KeyCode.F8),
                "Key that shows/hides the build info box.");
            BuildInfoY = Config.Bind(build, "Build info position Y", 0.78f,
                new ConfigDescription("Vertical position of the build info. 0 = top edge, 1 = bottom edge.",
                    new AcceptableValueRange<float>(0f, 1f)));

            RemovalLockKey = Config.Bind(build, "Removal lock toggle key", new KeyboardShortcut(KeyCode.F9),
                "Toggles a lock that stops the hammer from removing pieces, so you can't delete your build by accident. Always starts unlocked.");

            const string farming = "Farming";
            ShowPlantingPreview = Config.Bind(farming, "Planting preview", true,
                "While planting with the cultivator, show whether the seed will grow where you're aiming, and if not, why " +
                "(too close to another plant or a rock, no open sky, wrong biome, too cold...).");
            ShowPlantSpacing = Config.Bind(farming, "Show plant spacing", true,
                "While planting, draw the grow space of nearby plants. Plants that aren't growing are drawn red.");

            ShowTeleportBlockers = Config.Bind("Portals", "Show teleport blockers", true,
                "When looking at a portal, list the carried items (ores, metals...) that stop you from teleporting.");

            const string ships = "Ships";
            ShowHullReadout = Config.Bind(ships, "Show hull readout", true,
                "While aboard a ship, show its hull health.");
            HullWarnings = Config.Bind(ships, "Hull warnings", true,
                "Warn when the ship you're on takes damage, and loudly when it drops below the warning and critical levels.");
            HullWarnLevel = Config.Bind(ships, "Warning level", 0.5f,
                new ConfigDescription("Hull health (0-1) for the 'hull damaged' warning.", new AcceptableValueRange<float>(0.1f, 0.95f)));
            HullCriticalLevel = Config.Bind(ships, "Critical level", 0.25f,
                new ConfigDescription("Hull health (0-1) for the 'hull critical' warning.", new AcceptableValueRange<float>(0.05f, 0.9f)));
            HullReadoutY = Config.Bind(ships, "Hull readout position Y", 0.12f,
                new ConfigDescription("Vertical position of the hull readout. 0 = top edge, 1 = bottom edge.",
                    new AcceptableValueRange<float>(0f, 1f)));

            const string carto = "Cartography Table";
            MarkerImportMode = Config.Bind(carto, "Import markers", MarkerImport.Filtered,
                "Which shared markers to take when reading a cartography table. All = vanilla. Filtered = apply the filters below. " +
                "None = only the explored map, no markers. Your own markers are never touched.");
            SkipCrossedOutMarkers = Config.Bind(carto, "Skip crossed-out markers", true,
                "Filtered mode: don't import markers others have crossed out (cleared dungeons, emptied spots...).");
            DuplicateMergeDistance = Config.Bind(carto, "Merge duplicates within", 20f,
                new ConfigDescription("Filtered mode: skip a marker if one with the same icon is already this close, in meters. 0 = off.",
                    new AcceptableValueRange<float>(0f, 200f)));
            SkipMarkerTypes = Config.Bind(carto, "Skip marker types", "",
                "Filtered mode: comma-separated marker icons to never import: fire, house, hammer, dot, rune, or game names like Boss, Hildir1.");
            ShowImportSummary = Config.Bind(carto, "Show import summary", true,
                "After reading a table, show how many markers were skipped or updated.");
            UpdateSharedMarkers = Config.Bind(carto, "Update existing markers", true,
                "When reading a table, update shared markers you already have if their name, icon or crossed-out state changed. " +
                "Vanilla never updates a shared marker once you have it. Your own markers are never changed.");
            EnableMarkerManager = Config.Bind(carto, "Marker manager", true,
                "Hold the modifier and press Use at a cartography table to view, edit, cross out, merge and delete the markers stored on it.");
            ManagerModifier = Config.Bind(carto, "Marker manager modifier", KeyCode.LeftShift,
                "Hold this while pressing Use at a table to open the marker manager instead of reading or writing.");
            ManagerPermission = Config.Bind(carto, "Edit permission", EditPermission.BuilderOrWard,
                "Who can edit a table's markers in the manager. BuilderOrWard: whoever built the table, players permitted on a ward covering it, " +
                "and admins/host. AdminsOnly: admins/host only. Everyone else can still open it to look.");

            BuildStaminaMultiplier = Config.Bind("Stamina", "Building multiplier", 1f,
                new ConfigDescription("Stamina cost multiplier for placing, removing and repairing with the hammer. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));
            SowStaminaMultiplier = Config.Bind("Stamina", "Sowing multiplier", 1f,
                new ConfigDescription("Stamina cost multiplier for planting with the cultivator. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));

            BuildDistance = Config.Bind("Player", "Build distance", 0f,
                new ConfigDescription("How far away you can place and remove pieces, in meters. 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 20f)));
            PickupRange = Config.Bind("Player", "Auto pickup range", 0f,
                new ConfigDescription("Radius in meters for automatically picking up items. 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 10f)));

            AutoCompat = Config.Bind("Compatibility", "Auto-adjust for other mods", true,
                "Turn off Norse Sense features that another installed mod already handles (e.g. fine rotation when Gizmo is installed).");
            ShowCompatWarnings = Config.Bind("Compatibility", "Show warnings in game", true,
                "Show a message when joining a world if another mod overlaps with Norse Sense. Details are always written to the BepInEx log.");

            BuildDistance.SettingChanged += (s, e) => PlayerTweaks.Apply();
            PickupRange.SettingChanged += (s, e) => PlayerTweaks.Apply();

            _spawnViewObject = new GameObject("NorseSense_SpawnView");
            DontDestroyOnLoad(_spawnViewObject);
            _spawnViewObject.AddComponent<SpawnView>();
            _spawnViewObject.AddComponent<MapOverlay>();
            _spawnViewObject.AddComponent<BuildHelpers>();
            _spawnViewObject.AddComponent<PlantPreview>();
            _spawnViewObject.AddComponent<HullWarning>();
            _spawnViewObject.AddComponent<MarkerManager>();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"Loaded. Building stamina x{BuildStaminaMultiplier.Value}, sowing stamina x{SowStaminaMultiplier.Value}, spawn view key: {ToggleKey.Value}");
        }

        void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_spawnViewObject != null) Destroy(_spawnViewObject);
        }

        void Update()
        {
            // Every plugin has loaded and patched by the time a player exists.
            if (!Compat.Checked && Player.m_localPlayer != null) Compat.Run(Guid, Logger);

            PlayerTweaks.Update();
            if (Player.m_localPlayer == null || IsTyping()) return;

            if (ToggleKey.Value.IsDown())
            {
                ShowSpawnRadius.Value = !ShowSpawnRadius.Value;
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"Spawn view: {(ShowSpawnRadius.Value ? "ON" : "OFF")}");
            }
            if (BuildInfoToggleKey.Value.IsDown())
            {
                ShowBuildInfo.Value = !ShowBuildInfo.Value;
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"Build info: {(ShowBuildInfo.Value ? "ON" : "OFF")}");
            }
            if (RemovalLockKey.Value.IsDown())
            {
                RemovalLockPatch.Locked = !RemovalLockPatch.Locked;
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"Piece removal: {(RemovalLockPatch.Locked ? "LOCKED" : "unlocked")}");
            }
        }

        internal static bool IsTyping() =>
            (Chat.instance != null && Chat.instance.HasFocus()) ||
            Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible();
    }
}
