using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    internal enum SpawnCell : byte { Unknown, Blocked, Protected, Steep, Exposed }

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

        // Unknown = terrain not loaded here, so the game itself can't evaluate the point either.
        internal static SpawnCell Classify(ref Vector3 p)
        {
            var zs = ZoneSystem.instance;
            zs.GetGroundData(ref p, out Vector3 normal, out _, out _, out Heightmap hmap);
            if (hmap == null) return SpawnCell.Unknown;
            if (zs.IsBlocked(p)) return SpawnCell.Blocked;
            if (EffectArea.IsPointInsideArea(p, EffectArea.Type.PlayerBase)) return SpawnCell.Protected;
            if (normal.y < MinNormalY) return SpawnCell.Steep;
            return SpawnCell.Exposed;
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
