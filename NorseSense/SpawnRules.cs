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

        static readonly System.Reflection.FieldInfo SpawnSystemsField = AccessTools.Field(typeof(SpawnSystem), "m_instances");

        static SpawnSystem _builtFor;
        static readonly List<SpawnSystem.SpawnData> Candidates = new List<SpawnSystem.SpawnData>();
        static readonly Dictionary<int, List<SpawnSystem.SpawnData>> ByBiome = new Dictionary<int, List<SpawnSystem.SpawnData>>();

        // Underwater ground where nothing but fish and sea serpents could spawn: every other entry in the world's
        // spawn lists and in the raids is put through the same altitude (height above the water line) and ocean
        // depth checks as SpawnSystem.IsSpawnPointGood. Read from the game's own lists, so modded spawns count too.
        // Leeches and other swamp-water mobs count as a threat, since they're right by your base.
        static bool OnlySeaCreatures(Vector3 p, Heightmap.Biome biome, Heightmap.BiomeArea area, Heightmap hmap)
        {
            var list = CandidatesFor(biome, area);
            if (list == null) return false; // can't tell: leave it as it was
            float altitude = p.y - ZoneSystem.instance.m_waterLevel;
            float depth = -1f;
            foreach (var spawn in list)
            {
                if (altitude < spawn.m_minAltitude || altitude > spawn.m_maxAltitude) continue;
                if (spawn.m_minOceanDepth != spawn.m_maxOceanDepth)
                {
                    if (depth < 0f) depth = hmap.GetOceanDepth(p);
                    if (depth < spawn.m_minOceanDepth || depth > spawn.m_maxOceanDepth) continue;
                }
                return false; // something other than fish or a serpent could spawn here
            }
            return true;
        }

        // The entries that matter for one biome and area, worked out once per world: thousands of water cells are
        // checked every grid rebuild.
        static List<SpawnSystem.SpawnData> CandidatesFor(Heightmap.Biome biome, Heightmap.BiomeArea area)
        {
            var systems = SpawnSystemsField != null ? SpawnSystemsField.GetValue(null) as List<SpawnSystem> : null;
            var system = systems != null && systems.Count > 0 ? systems[0] : null;
            if (system == null) return null;
            if (system != _builtFor)
            {
                _builtFor = system;
                Candidates.Clear();
                ByBiome.Clear();
                foreach (var spawnList in system.m_spawnLists) Add(spawnList.m_spawners);
                // Raids spawn through the same check, with their own entries.
                if (RandEventSystem.instance != null)
                    foreach (var ev in RandEventSystem.instance.m_events) Add(ev.m_spawn);
            }
            int key = (int)biome * 8 + (int)area;
            if (!ByBiome.TryGetValue(key, out var list))
            {
                list = new List<SpawnSystem.SpawnData>();
                foreach (var spawn in Candidates)
                    if ((spawn.m_biome & biome) != 0 && (spawn.m_biomeArea & area) != 0) list.Add(spawn);
                ByBiome[key] = list;
            }
            return list;
        }

        static void Add(List<SpawnSystem.SpawnData> spawns)
        {
            if (spawns == null) return;
            foreach (var spawn in spawns)
            {
                if (spawn == null || !spawn.m_enabled || spawn.m_prefab == null) continue;
                if (spawn.m_prefab.GetComponent<Fish>() != null) continue;
                // Serpents: out at sea only, always underwater.
                if (spawn.m_biome == Heightmap.Biome.Ocean && spawn.m_maxAltitude < 0f) continue;
                Candidates.Add(spawn);
            }
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
