using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    internal enum SpawnCell : byte { Unknown, Blocked, Protected, Steep, Exposed, Water, RaidOnly }

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
        internal static readonly Color RaidOnlyColor = new Color(1f, 0.6f, 0.1f, 0.4f); // orange: exposed, but only to raids
        internal static readonly Color WaterColor = new Color(0.8f, 0.8f, 0.8f, 0.16f); // neutral grey: not blue, so it never reads as protected

        // Unknown = terrain not loaded here, so the game itself can't evaluate the point either.
        // raids: the ones that could start where you are (UnlockedRaids), taken once per scan so a scan that runs
        // over several frames judges every cell the same way.
        internal static SpawnCell Classify(ref Vector3 p, List<RandomEvent> raids)
        {
            var zs = ZoneSystem.instance;
            zs.GetGroundData(ref p, out Vector3 normal, out Heightmap.Biome biome, out Heightmap.BiomeArea area, out Heightmap hmap);
            if (hmap == null) return SpawnCell.Unknown;
            if (zs.IsBlocked(p)) return SpawnCell.Blocked;
            if (EffectArea.IsPointInsideArea(p, EffectArea.Type.PlayerBase)) return SpawnCell.Protected;
            if (p.y < zs.m_waterLevel)
            {
                var water = InWater(p, normal, biome, area, hmap, raids);
                if (water != SpawnCell.Exposed) return water;
            }
            if (normal.y < MinNormalY) return SpawnCell.Steep;
            return SpawnCell.Exposed;
        }

        // ---------- Water ----------

        static readonly System.Reflection.FieldInfo SpawnSystemsField = AccessTools.Field(typeof(SpawnSystem), "m_instances");

        static SpawnSystem _builtFor;
        static readonly List<SpawnSystem.SpawnData> Candidates = new List<SpawnSystem.SpawnData>();
        static readonly Dictionary<int, List<SpawnSystem.SpawnData>> ByBiome = new Dictionary<int, List<SpawnSystem.SpawnData>>();

        // Underwater ground where no monster but a sea serpent could spawn: every other monster in the world's
        // spawn lists, and in the raids you've unlocked, is put through the same altitude (height above the water
        // line) and ocean depth checks as SpawnSystem.IsSpawnPointGood. Read from the game's own lists, so modded
        // spawns count too. Leeches and necks count, since they're right by your base; fish, seagulls and other
        // animals that never attack don't.
        // Exposed when a monster could spawn here normally, RaidOnly when only a raid could bring one, Water when
        // nothing but serpents could.
        static SpawnCell InWater(Vector3 p, Vector3 normal, Heightmap.Biome biome, Heightmap.BiomeArea area, Heightmap hmap, List<RandomEvent> raids)
        {
            var list = CandidatesFor(biome, area);
            if (list == null) return SpawnCell.Exposed; // can't tell: leave it as it was
            float altitude = p.y - ZoneSystem.instance.m_waterLevel;
            float depth = -1f;
            foreach (var spawn in list)
                if (CouldSpawn(spawn, p, normal, altitude, hmap, ref depth)) return SpawnCell.Exposed; // a monster other than a serpent
            // Zones that border another biome also spawn from that biome's list (SpawnSystem.UpdateSpawning).
            try
            {
                foreach (var alt in hmap.m_cornerAltBiomes)
                foreach (var spawn in alt.m_spawn)
                    if (Counts(spawn) && (spawn.m_biome & biome) != 0 && (spawn.m_biomeArea & area) != 0 && CouldSpawn(spawn, p, normal, altitude, hmap, ref depth))
                        return SpawnCell.Exposed;
            }
            catch (Exception)
            {
                return SpawnCell.Exposed; // a game update changed these: can't tell, leave it as it was
            }
            // Raids spawn through the same check with their own lists. Whether one can start depends on the biome
            // where you are, already settled in UnlockedRaids; here only its spawn entries' own biome counts.
            if (raids != null)
            foreach (var ev in raids)
            {
                if (ev.m_spawn == null) continue;
                foreach (var spawn in ev.m_spawn)
                    if (Counts(spawn) && (spawn.m_biome & biome) != 0 && (spawn.m_biomeArea & area) != 0 &&
                        CouldSpawn(spawn, p, normal, altitude, hmap, ref depth))
                        return SpawnCell.RaidOnly;
            }
            return SpawnCell.Water;
        }

        // SpawnSystem.IsSpawnPointGood's own checks for one entry, minus the ones that change from moment to moment
        // (players nearby) and the ward check, which Classify has already made.
        static bool CouldSpawn(SpawnSystem.SpawnData spawn, Vector3 p, Vector3 normal, float altitude, Heightmap hmap, ref float depth)
        {
            if (altitude < spawn.m_minAltitude || altitude > spawn.m_maxAltitude) return false;
            if (normal.y < Mathf.Cos(Mathf.Deg2Rad * spawn.m_maxTilt) || normal.y > Mathf.Cos(Mathf.Deg2Rad * spawn.m_minTilt)) return false;
            if (spawn.m_minDistanceFromCenter > 0f || spawn.m_maxDistanceFromCenter > 0f)
            {
                float fromCenter = Utils.LengthXZ(p);
                if (spawn.m_minDistanceFromCenter > 0f && fromCenter < spawn.m_minDistanceFromCenter) return false;
                if (spawn.m_maxDistanceFromCenter > 0f && fromCenter > spawn.m_maxDistanceFromCenter) return false;
            }
            if (!spawn.m_inForest || !spawn.m_outsideForest)
            {
                bool forest = WorldGenerator.InForest(p);
                if (!spawn.m_inForest && forest || !spawn.m_outsideForest && !forest) return false;
            }
            if (!spawn.m_inLava || !spawn.m_outsideLava)
            {
                if (!spawn.m_inLava && ZoneSystem.instance.IsLava(p, true) || !spawn.m_outsideLava && !ZoneSystem.instance.IsLava(p)) return false;
            }
            if (spawn.m_minOceanDepth == spawn.m_maxOceanDepth) return true;
            if (depth < 0f) depth = hmap.GetOceanDepth(p);
            return depth >= spawn.m_minOceanDepth && depth <= spawn.m_maxOceanDepth;
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
                if (Counts(spawn)) Candidates.Add(spawn);
        }

        // Monsters, but not serpents (out at sea only, always underwater). Fish have no AI, and seagulls, deer and
        // the like have the animal AI that only runs away.
        static bool Counts(SpawnSystem.SpawnData spawn) =>
            spawn != null && spawn.m_enabled && spawn.m_prefab != null && spawn.m_prefab.GetComponent<MonsterAI>() != null &&
            !(spawn.m_biome == Heightmap.Biome.Ocean && spawn.m_maxAltitude < 0f);

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

        // The raids that could start where the player is: unlocked (world keys, or the player's own with player
        // events on) and in a biome they can start in (RandEventSystem.InValidBiome). Base value isn't part of it.
        internal static List<RandomEvent> UnlockedRaids(Player player)
        {
            var raids = new List<RandomEvent>();
            var events = RandEventSystem.instance;
            if (events == null || ZoneSystem.instance == null || player == null) return raids;

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
            foreach (var ev in events.m_events)
            {
                if (!ev.m_enabled || !ev.m_random || !ev.m_nearBaseOnly) continue;
                if (!HaveGlobalKeys(events, ev, TempPlayers)) continue;
                if (playerEvents && !data.possibleEvents.Contains(ev.m_name)) continue;
                if (!InValidBiome(events, ev, data.position)) continue;
                raids.Add(ev);
            }
            return raids;
        }

        // Number of random raids that could pick this player at their current spot, ignoring base value.
        internal static int CountUnlockedRaids(Player player) => UnlockedRaids(player).Count;
    }
}
