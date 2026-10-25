using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Crafting station level, which extensions count and why the others don't, plus why it can't be used.
    // Same rules as StationExtension.FindExtensions and CraftingStation.CheckUsable.
    internal static class StationInfo
    {
        const float NearbyRange = 15f; // extensions this close but out of reach are reported as too far
        const float NeedCover = 0.7f;  // CraftingStation.CheckUsable
        const float CacheTime = 0.5f;

        internal static readonly AccessTools.FieldRef<List<StationExtension>> AllExtensions =
            AccessTools.StaticFieldRefAccess<List<StationExtension>>(AccessTools.Field(typeof(StationExtension), "m_allExtensions"));

        // Extension pieces that exist for each station, by station name. Built from the prefab list once per scene.
        static readonly Dictionary<string, List<Piece>> ExtensionTypes = new Dictionary<string, List<Piece>>();
        static ZNetScene _typesScene;

        static readonly List<StationExtension> Attached = new List<StationExtension>();
        static readonly List<CraftingStation> Stations = new List<CraftingStation>();
        static readonly List<string> Parts = new List<string>();

        static CraftingStation _cachedStation;
        static float _cachedAt;
        static string _cached;

        // Lines for the station's hover text; cached because hover text is asked for every frame.
        internal static string Describe(CraftingStation station)
        {
            if (station == _cachedStation && Time.time - _cachedAt < CacheTime) return _cached;
            _cachedStation = station;
            _cachedAt = Time.time;
            _cached = Build(station);
            return _cached;
        }

        static string Build(CraftingStation station)
        {
            var lines = new List<string>(6);
            Vector3 pos = station.transform.position;
            var types = TypesFor(station.m_name);

            Attached.Clear();
            StationExtension.FindExtensions(station, pos, Attached);
            if (types.Count > 0)
            {
                lines.Add($"Level <b>{1 + Attached.Count}</b> of {1 + types.Count}");

                Parts.Clear();
                foreach (var ext in Attached) Parts.Add(Name(ext));
                if (Parts.Count > 0) lines.Add($"<color={Ui.Good}>{string.Join(", ", Parts)}</color>");

                var player = Player.m_localPlayer;
                Parts.Clear();
                int undiscovered = 0;
                foreach (var type in types)
                {
                    if (HasAttached(type.m_name)) continue;
                    StationExtension near = Nearest(type.m_name, pos, out float dist);
                    if (near != null && dist < NearbyRange && !ServesOtherStation(near, station))
                        lines.Add($"<color={Ui.Warn}>{Name(near)}: {dist:0.0} m away, must be within {near.m_maxStationDistance:0.#} m</color>");
                    else if (player == null || player.IsRecipeKnown(type.m_name))
                        Parts.Add(Localization.instance.Localize(type.m_name));
                    else
                        undiscovered++;
                }
                if (undiscovered > 0) Parts.Add(undiscovered == 1 ? "1 not discovered yet" : $"{undiscovered} not discovered yet");
                if (Parts.Count > 0) lines.Add($"<color={Ui.Muted}>Not built: {string.Join(", ", Parts)}</color>");

                string extra = ExtraCopies(station, pos);
                if (extra != null) lines.Add($"<color={Ui.Muted}>Doesn't count: second {extra} (each kind counts once)</color>");
            }

            string blocked = WhyUnusable(station);
            if (blocked != null) lines.Add($"<color={Ui.Bad}>Can't craft: {blocked}</color>");
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        // Build info line while placing an extension: which station it joins and what it adds.
        internal static InfoRow DescribeGhost(StationExtension ghost, Vector3 pos)
        {
            if (ghost.m_craftingStation == null) return null;
            string stationName = Localization.instance.Localize(ghost.m_craftingStation.m_name);
            string pieceName = Localization.instance.Localize(ghost.GetComponent<Piece>()?.m_name ?? "");

            Stations.Clear();
            CraftingStation.FindStationsInRange(ghost.m_craftingStation.m_name, pos, ghost.m_maxStationDistance, Stations);
            if (Stations.Count == 0)
            {
                var nearest = CraftingStation.FindClosestStationInRange(ghost.m_craftingStation.m_name, pos, 50f);
                string howFar = nearest != null ? $"nearest is {Vector3.Distance(nearest.transform.position, pos):0.0} m away" : null;
                return new InfoRow(stationName, $"None within {ghost.m_maxStationDistance:0.#} m", howFar, Tone.Bad, ghost.m_craftingStation.m_icon);
            }

            int gaining = 0;
            CraftingStation closest = null;
            float closestDist = float.MaxValue;
            foreach (var station in Stations)
            {
                Attached.Clear();
                StationExtension.FindExtensions(station, station.transform.position, Attached);
                if (ghost.m_stack || !HasAttached(ghost.GetComponent<Piece>()?.m_name)) gaining++;
                float d = Vector3.Distance(station.transform.position, pos);
                if (d < closestDist)
                {
                    closest = station;
                    closestDist = d;
                }
            }

            Attached.Clear();
            StationExtension.FindExtensions(closest, closest.transform.position, Attached);
            int level = 1 + Attached.Count;
            bool gains = ghost.m_stack || !HasAttached(ghost.GetComponent<Piece>()?.m_name);
            string others = Stations.Count > 1 ? $"upgrades {gaining} of {Stations.Count} {stationName}s in reach" : null;
            return gains
                ? new InfoRow(stationName, $"Level {level} » <b>{level + 1}</b>", others, Tone.Good, ghost.m_craftingStation.m_icon)
                : new InfoRow(stationName, $"Already has a {pieceName}, stays level {level}", others, Tone.Warn, ghost.m_craftingStation.m_icon);
        }

        static string WhyUnusable(CraftingStation station)
        {
            if (station.m_craftRequireRoof && station.m_roofCheckPoint != null)
            {
                Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out float cover, out bool underRoof);
                if (!underRoof) return "no roof above";
                if (cover < NeedCover) return $"too exposed, cover {cover * 100f:0}% of {NeedCover * 100f:0}%";
            }
            if (station.m_craftRequireFire && !EffectArea.IsPointPlus025InsideBurningArea(station.transform.position))
                return "needs a fire nearby";
            return null;
        }

        static bool HasAttached(string pieceName)
        {
            foreach (var ext in Attached)
                if (ext.GetComponent<Piece>()?.m_name == pieceName) return true;
            return false;
        }

        static StationExtension Nearest(string pieceName, Vector3 pos, out float dist)
        {
            StationExtension best = null;
            dist = float.MaxValue;
            foreach (var ext in AllExtensions())
            {
                if (ext == null || ext.GetComponent<Piece>()?.m_name != pieceName) continue;
                float d = Vector3.Distance(ext.transform.position, pos);
                if (d < dist)
                {
                    best = ext;
                    dist = d;
                }
            }
            return best;
        }

        // An extension out of this station's reach that already serves another one isn't "too far", it belongs elsewhere.
        static bool ServesOtherStation(StationExtension ext, CraftingStation self)
        {
            Stations.Clear();
            CraftingStation.FindStationsInRange(ext.m_craftingStation.m_name, ext.transform.position, ext.m_maxStationDistance, Stations);
            foreach (var s in Stations)
                if (s != self) return true;
            return false;
        }

        // Second copies of an extension in reach: the game counts each kind once.
        static string ExtraCopies(CraftingStation station, Vector3 pos)
        {
            Parts.Clear();
            foreach (var ext in AllExtensions())
            {
                if (ext == null || ext.m_stack || Attached.Contains(ext) || ext.m_craftingStation == null) continue;
                if (ext.m_craftingStation.m_name != station.m_name) continue;
                if (Vector3.Distance(ext.transform.position, pos) >= ext.m_maxStationDistance) continue;
                string name = Name(ext);
                if (!Parts.Contains(name)) Parts.Add(name);
            }
            return Parts.Count > 0 ? string.Join(", ", Parts) : null;
        }

        static string Name(StationExtension ext) => Localization.instance.Localize(ext.GetComponent<Piece>()?.m_name ?? ext.name);

        static List<Piece> TypesFor(string stationName)
        {
            var scene = ZNetScene.instance;
            if (scene != _typesScene)
            {
                ExtensionTypes.Clear();
                _typesScene = scene;
                var buildable = BuildablePieces();
                if (scene != null)
                    foreach (var prefab in scene.m_prefabs)
                    {
                        if (prefab == null || !buildable.Contains(prefab.name)) continue;
                        var ext = prefab.GetComponent<StationExtension>();
                        var piece = ext != null ? prefab.GetComponent<Piece>() : null;
                        if (piece == null || ext.m_craftingStation == null) continue;
                        if (!ExtensionTypes.TryGetValue(ext.m_craftingStation.m_name, out var list))
                            ExtensionTypes[ext.m_craftingStation.m_name] = list = new List<Piece>();
                        if (!list.Exists(p => p.m_name == piece.m_name)) list.Add(piece);
                    }
            }
            return ExtensionTypes.TryGetValue(stationName, out var types) ? types : Empty;
        }

        static readonly List<Piece> Empty = new List<Piece>();

        // Unused or leftover extension prefabs would inflate "Level 3 of 6", so only count pieces a tool can build.
        static HashSet<string> BuildablePieces()
        {
            var names = new HashSet<string>();
            if (ObjectDB.instance == null) return names;
            foreach (var item in ObjectDB.instance.m_items)
            {
                var table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
                if (table == null) continue;
                foreach (var piece in table.m_pieces)
                    if (piece != null) names.Add(piece.name);
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetHoverText))]
    static class StationHoverPatch
    {
        static void Postfix(CraftingStation __instance, ref string __result)
        {
            if (!Plugin.ShowStationInfo.Value || Player.m_localPlayer == null) return;
            string info = StationInfo.Describe(__instance);
            if (info != null) __result += "\n" + info;
        }
    }
}
