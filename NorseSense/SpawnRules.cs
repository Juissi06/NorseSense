using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    internal enum SpawnCell : byte { Unknown, Blocked, Protected, Steep, Exposed, Water }

    // Mirrors the point checks in SpawnSystem.IsSpawnPointGood and raid eligibility in RandEventSystem.
    internal static class SpawnRules
    {
        internal const float PlayerNoSpawnRadius = 40f; // SpawnSystem default when m_spawnRadiusMin is 0
        internal const float DefaultMaxTilt = 35f;       // SpawnData.m_maxTilt default
        internal const int RaidBaseValue = 3;            // RandEventSystem.CheckBase

        static readonly float MinNormalY = Mathf.Cos(DefaultMaxTilt * Mathf.Deg2Rad);

        internal static readonly Color ProtectedColor = new Color(0.2f, 0.5f, 1f, 0.35f);
        internal static readonly Color ExposedColor = new Color(1f, 0.25f, 0.1f, 0.4f);
        internal static readonly Color SteepColor = new Color(1f, 0.85f, 0.2f, 0.22f);
        internal static readonly Color PreviewColor = new Color(0.3f, 1f, 0.85f, 0.5f);
        internal static readonly Color RaidColor = new Color(1f, 0.55f, 0f, 0.9f);
        internal static readonly Color WaterColor = new Color(0.8f, 0.8f, 0.8f, 0.16f); // neutral grey: not blue, so it never reads as protected

        // Unknown = terrain not loaded here, so the game itself can't evaluate the point either.
        internal static SpawnCell Classify(ref Vector3 p)
        {
            var zs = ZoneSystem.instance;
            zs.GetGroundData(ref p, out Vector3 normal, out Heightmap.Biome biome, out Heightmap.BiomeArea area, out Heightmap hmap);
            if (hmap == null) return SpawnCell.Unknown;
            if (zs.IsBlocked(p)) return SpawnCell.Blocked;
            if (EffectArea.IsPointInsideArea(p, EffectArea.Type.PlayerBase)) return SpawnCell.Protected;
            if (p.y < zs.m_waterLevel && OnlySeaCreatures(p, biome, area, hmap)) return SpawnCell.Water;
            if (normal.y < MinNormalY) return SpawnCell.Steep;
            return SpawnCell.Exposed;
        }

        // ---------- Water ----------

        static readonly AccessTools.FieldRef<List<SpawnSystem>> SpawnSystems =
            AccessTools.StaticFieldRefAccess<List<SpawnSystem>>(AccessTools.Field(typeof(SpawnSystem), "m_instances"));

        // Underwater ground where none of the world's spawn entries for that biome could put a land creature: the
        // same altitude (height above the water line, which is at 30) and ocean depth checks as
        // SpawnSystem.IsSpawnPointGood. Read from the game's own spawn lists, so modded spawns count too. Sea
        // creatures are the ones that only spawn underwater or in the deep (serpents) and fish.
        static bool OnlySeaCreatures(Vector3 p, Heightmap.Biome biome, Heightmap.BiomeArea area, Heightmap hmap)
        {
            var systems = SpawnSystems != null ? SpawnSystems() : null;
            if (systems == null || systems.Count == 0 || systems[0] == null) return false; // can't tell: leave it as it was
            float altitude = p.y - ZoneSystem.instance.m_waterLevel;
            float depth = -1f;
            foreach (var list in systems[0].m_spawnLists)
            foreach (var spawn in list.m_spawners)
            {
                if (!spawn.m_enabled || spawn.m_prefab == null || (spawn.m_biome & biome) == 0 || (spawn.m_biomeArea & area) == 0) continue;
                if (spawn.m_maxAltitude < 0f || spawn.m_minOceanDepth > 0f || spawn.m_prefab.GetComponent<Fish>() != null) continue;
                if (altitude < spawn.m_minAltitude || altitude > spawn.m_maxAltitude) continue;
                if (spawn.m_minOceanDepth != spawn.m_maxOceanDepth)
                {
                    if (depth < 0f) depth = hmap.GetOceanDepth(p);
                    if (depth < spawn.m_minOceanDepth || depth > spawn.m_maxOceanDepth) continue;
                }
                return false; // a land creature could spawn here
            }
            return true;
        }

        // ---------- Raids ----------

        static readonly Func<RandEventSystem, RandomEvent, Vector3, bool> InValidBiome =
            AccessTools.MethodDelegate<Func<RandEventSystem, RandomEvent, Vector3, bool>>(
                AccessTools.Method(typeof(RandEventSystem), "InValidBiome"));

        static readonly Func<RandEventSystem, RandomEvent, List<RandEventSystem.PlayerEventData>, bool> HaveGlobalKeys =
            AccessTools.MethodDelegate<Func<RandEventSystem, RandomEvent, List<RandEventSystem.PlayerEventData>, bool>>(
                AccessTools.Method(typeof(RandEventSystem), "HaveGlobalKeys"));

        static readonly AccessTools.FieldRef<Player, List<string>> ReadyEvents =
            AccessTools.FieldRefAccess<Player, List<string>>("m_readyEvents");

        static readonly List<RandEventSystem.PlayerEventData> TempPlayers = new List<RandEventSystem.PlayerEventData>();

        // Number of random raids that could pick this player at their current spot, ignoring base value.
        internal static int CountUnlockedRaids(Player player)
        {
            var events = RandEventSystem.instance;
            if (events == null || ZoneSystem.instance == null) return 0;

            var ready = ReadyEvents(player);
            var data = new RandEventSystem.PlayerEventData
            {
                position = player.transform.position,
                possibleEvents = new HashSet<string>(ready ?? new List<string>()),
                baseValue = player.GetBaseValue()
            };
            TempPlayers.Clear();
            TempPlayers.Add(data);

            bool playerEvents = ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            int count = 0;
            foreach (var ev in events.m_events)
            {
                if (!ev.m_enabled || !ev.m_random || !ev.m_nearBaseOnly) continue;
                if (!HaveGlobalKeys(events, ev, TempPlayers)) continue;
                if (!InValidBiome(events, ev, data.position)) continue;
                if (playerEvents && !data.possibleEvents.Contains(ev.m_name)) continue;
                count++;
            }
            return count;
        }
    }
}
