using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace NorseSense
{
    // Finds other mods that touch the same game code and adjusts or warns, once all plugins have loaded.
    // Two sources: Harmony patch owners on the methods each feature relies on (works for any mod),
    // and a short list of well-known mods matched by GUID/name.
    internal static class Compat
    {
        internal static bool Checked;
        internal static bool FineRotationDisabled;
        internal static bool PlayerTweaksDisabled;
        internal static bool CartographyManagerDisabled;
        internal static bool FreecamDisabled;
        internal static string FreecamDisabledBy;
        internal static string SpawnRulesChangedBy;
        internal static string RaidRulesChangedBy;
        internal static string PlantRulesChangedBy;
        internal static string TerrainRulesChangedBy;

        static readonly string[] RotationMods = { "gizmo" };
        static readonly string[] PlayerTweakMods = { "valheim_plus", "valheimplus", "valheim plus" };
        static readonly string[] SpawnMods = { "spawn_that", "spawnthat", "spawn that", "expand_world_spawns", "expand world spawns" };
        static readonly string[] TableManagerMods = { "cartographyadmin", "cartography admin" };
        static readonly string[] CameraBuildMods = { "buildcamera", "build camera", "build_camera", "freecam", "free cam", "freebuildcam" };
        static readonly string[] RaidMods = { "custom_raids", "customraids", "custom raids", "expand_world_events", "expand world events" };

        internal static void Run(string ownHarmonyId, ManualLogSource log)
        {
            Checked = true;
            bool auto = Plugin.AutoCompat.Value;
            var notes = new List<string>();

            string rotationMod = FindPlugin(RotationMods);
            if (rotationMod != null && auto)
            {
                FineRotationDisabled = true;
                notes.Add($"Fine rotation is off because {rotationMod} handles piece rotation.");
            }

            string tableMod = FindPlugin(TableManagerMods);
            if (tableMod != null && auto)
            {
                CartographyManagerDisabled = true;
                notes.Add($"The table marker manager is off because {tableMod} provides one.");
            }

            string cameraMod = FindPlugin(CameraBuildMods);
            if (cameraMod != null && auto)
            {
                FreecamDisabled = true;
                FreecamDisabledBy = cameraMod;
                notes.Add($"Freecam building is off because {cameraMod} provides a build camera.");
            }

            string tweakMod = FindPlugin(PlayerTweakMods);
            if (tweakMod != null)
            {
                if (auto) PlayerTweaksDisabled = true;
                notes.Add(auto
                    ? $"Build distance and pickup range are left to {tweakMod}."
                    : $"{tweakMod} may also change build distance and pickup range.");
                if (Plugin.BuildStaminaMultiplier.Value < 1f || Plugin.SowStaminaMultiplier.Value < 1f)
                    notes.Add($"{tweakMod} may also change build stamina; the reductions will stack.");
            }

            SpawnRulesChangedBy = FindPlugin(SpawnMods) ?? PatchOwners(ownHarmonyId,
                Method(typeof(SpawnSystem), "IsSpawnPointGood"),
                Method(typeof(SpawnSystem), "FindBaseSpawnPoint"),
                Method(typeof(EffectArea), nameof(EffectArea.IsPointInsideArea)),
                Method(typeof(ZoneSystem), nameof(ZoneSystem.IsBlocked)));
            if (SpawnRulesChangedBy != null)
                notes.Add($"{SpawnRulesChangedBy} changes spawn rules, so the spawn view may not be exact.");

            RaidRulesChangedBy = FindPlugin(RaidMods) ?? PatchOwners(ownHarmonyId,
                Method(typeof(RandEventSystem), "CheckBase"),
                Method(typeof(RandEventSystem), "GetValidEventPoints"),
                Method(typeof(EffectArea), nameof(EffectArea.GetBaseValue)));
            if (RaidRulesChangedBy != null)
                notes.Add($"{RaidRulesChangedBy} changes raid rules, so the raid info may not be exact.");

            PlantRulesChangedBy = PatchOwners(ownHarmonyId,
                Method(typeof(Plant), nameof(Plant.UpdateHealth)),
                Method(typeof(Plant), "HaveGrowSpace"),
                Method(typeof(Plant), "HaveRoof"));
            if (PlantRulesChangedBy != null)
                notes.Add($"{PlantRulesChangedBy} changes how plants grow, so the planting preview may not be exact.");

            TerrainRulesChangedBy = PatchOwners(ownHarmonyId,
                Method(typeof(TerrainComp), nameof(TerrainComp.ApplyToHeightmap)),
                Method(typeof(TerrainComp), "RaiseTerrain"),
                Method(typeof(TerrainComp), "LevelTerrain"));
            if (TerrainRulesChangedBy != null)
                notes.Add($"{TerrainRulesChangedBy} changes terrain editing, so the terrain limits may not be exact.");

            // Informational only: other mods often patch these for unrelated reasons.
            string ghostPatchers = PatchOwners(ownHarmonyId, Method(typeof(Player), "UpdatePlacementGhost"));
            if (ghostPatchers != null && !FineRotationDisabled)
                log.LogInfo($"Also patching build placement: {ghostPatchers}. If fine rotation misbehaves, set the fine rotation modifier to None.");
            string staminaPatchers = PatchOwners(ownHarmonyId,
                Method(typeof(Player), "GetBuildStamina"),
                Method(typeof(Player), nameof(Player.HaveStamina)));
            if (staminaPatchers != null)
                log.LogInfo($"Also patching stamina: {staminaPatchers}. Stamina multipliers will combine with theirs.");

            string mapPatchers = PatchOwners(ownHarmonyId, Method(typeof(Minimap), nameof(Minimap.AddSharedMapData)));
            if (mapPatchers != null)
                log.LogInfo($"Also patching cartography table reading: {mapPatchers}. Marker filters run alongside it.");

            foreach (var note in notes) log.LogWarning(note);
            if (notes.Count == 0) log.LogInfo("No overlapping mods found.");

            if (Plugin.ShowCompatWarnings.Value && Player.m_localPlayer != null)
                foreach (var note in notes)
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Norse Sense: " + note);
        }

        static MethodBase Method(Type type, string name) => AccessTools.Method(type, name);

        static string FindPlugin(string[] keys)
        {
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                string guid = info.Metadata.GUID.ToLowerInvariant();
                string name = info.Metadata.Name.ToLowerInvariant();
                if (keys.Any(k => guid.Contains(k) || name.Contains(k))) return info.Metadata.Name;
            }
            return null;
        }

        // Names of other mods with Harmony patches on any of these methods, or null if none.
        static string PatchOwners(string ownHarmonyId, params MethodBase[] methods)
        {
            var owners = new HashSet<string>();
            foreach (var method in methods)
            {
                if (method == null) continue;
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                foreach (var owner in info.Owners)
                    if (owner != ownHarmonyId) owners.Add(PluginName(owner));
            }
            return owners.Count > 0 ? string.Join(", ", owners) : null;
        }

        // Harmony ids are usually the plugin GUID; fall back to the raw id.
        static string PluginName(string harmonyId) =>
            Chainloader.PluginInfos.TryGetValue(harmonyId, out var info) ? info.Metadata.Name : harmonyId;
    }
}
