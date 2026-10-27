using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // While planting with the cultivator: whether the seed would grow where the ghost stands and why not,
    // plus grow-space rings around the ghost and nearby plants. Same checks as Plant.UpdateHealth.
    // Farm check: with the cultivator out, every crop around you gets its ring, and the ones that aren't
    // growing say why.
    internal class PlantPreview : MonoBehaviour
    {
        const float NeighbourRange = 8f;
        const float FarmRange = 20f;
        const int MaxNeighbourRings = 60;
        const int MaxFarmRings = 150;
        const int MaxReasons = 30;
        const float RefreshInterval = 0.2f;
        const float NeighbourReach = 3f; // widest grow space of any vanilla seed plus a seed's footprint

        static readonly Color GrowColor = Ui.Alpha(Ui.GoodColor, 0.9f);
        static readonly Color BlockedColor = Ui.Alpha(Ui.BadColor, 0.95f);
        static readonly Color HealthyPlantColor = Ui.Alpha(Ui.GoodColor, 0.45f);
        static readonly Color UnhealthyPlantColor = Ui.Alpha(Ui.BadColor, 0.8f);

        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // Shown in the build info box; null when not planting.
        internal static InfoRow Line;

        static int _spaceMask, _roofMask;
        static readonly Collider[] Hits = new Collider[64];

        LineRenderer _ghostRing;
        readonly List<LineRenderer> _rings = new List<LineRenderer>();
        readonly List<Plant> _neighbours = new List<Plant>();
        float _timer;
        bool _farm;

        // Cultivator piece tables, by whether they hold any seed.
        static readonly Dictionary<PieceTable, bool> PlantTables = new Dictionary<PieceTable, bool>();

        void Awake()
        {
            _ghostRing = SpawnView.CreateLine(transform, "PlantGhostRing", GrowColor, 0.06f);
            _ghostRing.gameObject.SetActive(false);
        }

        void Update()
        {
            var player = Player.m_localPlayer;
            var ghost = player != null && player.InPlaceMode() ? PlacementGhost(player) : null;
            var plant = ghost != null && ghost.activeInHierarchy && Plugin.ShowPlantingPreview.Value ? ghost.GetComponent<Plant>() : null;
            _farm = Plugin.ShowFarmCheck.Value && player != null && player.InPlaceMode() && HoldsCultivator(player);
            if (plant == null)
            {
                Line = null;
                if (_ghostRing.gameObject.activeSelf) _ghostRing.gameObject.SetActive(false);
                if (!_farm)
                {
                    Hide();
                    return;
                }
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            if (plant == null)
            {
                UpdateNeighbourRings(null, player.transform.position, true);
                return;
            }

            Vector3 pos = ghost.transform.position;
            string problem = WhyWontGrow(plant, pos);
            string caveat = Compat.PlantRulesChangedBy != null ? $"may not be exact: {Compat.PlantRulesChangedBy}" : null;
            string name = Localization.instance.Localize(plant.m_name);
            Line = problem == null
                ? new InfoRow("Planting", $"{name} will grow here", caveat, Tone.Good)
                : new InfoRow("Planting", $"Won't grow, {problem}", caveat, Tone.Bad);

            _ghostRing.gameObject.SetActive(true);
            _ghostRing.startColor = _ghostRing.endColor = problem == null ? GrowColor : BlockedColor;
            SpawnView.DrawRing(_ghostRing, pos, plant.m_growRadius);

            UpdateNeighbourRings(plant, _farm ? player.transform.position : pos, _farm || Plugin.ShowPlantSpacing.Value);
        }

        // A build tool whose pieces include a seed: the cultivator, or a modded one like it.
        static bool HoldsCultivator(Player player)
        {
            var table = StaminaPatches.RightItem(player)?.m_shared.m_buildPieces;
            if (table == null) return false;
            if (!PlantTables.TryGetValue(table, out bool plants))
            {
                plants = false;
                foreach (var piece in table.m_pieces)
                    if (piece != null && piece.GetComponent<Plant>() != null) { plants = true; break; }
                PlantTables[table] = plants;
            }
            return plants;
        }

        // Farm check: why each plant near the middle of the screen isn't growing, under it.
        void OnGUI()
        {
            if (!_farm || Event.current.type != EventType.Repaint) return;
            var cam = Utils.GetMainCamera();
            if (cam == null) return;
            int shown = 0;
            foreach (var p in _neighbours)
            {
                if (p == null) continue;
                var status = p.GetStatus();
                if (status == Plant.Status.Healthy) continue;
                Vector3 s = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 0.3f);
                if (s.z <= 0f) continue;
                var r = new Rect(s.x - 80f, Screen.height - s.y, 160f, 20f);
                Ui.Label(r, $"<color={Ui.Bad}>{Reason(status)}</color>", Ui.SmallCenter);
                if (++shown >= MaxReasons) break;
            }
        }

        static string Reason(Plant.Status status)
        {
            switch (status)
            {
                case Plant.Status.NoSun: return "no open sky";
                case Plant.Status.NoSpace: return "too close";
                case Plant.Status.WrongBiome: return "wrong biome";
                case Plant.Status.NotCultivated: return "not cultivated";
                case Plant.Status.NoAttachPiece: return "nothing to climb";
                case Plant.Status.TooHot: return "too hot";
                case Plant.Status.TooCold: return "too cold";
                default: return status.ToString();
            }
        }

        void Hide()
        {
            Line = null;
            if (_ghostRing.gameObject.activeSelf) _ghostRing.gameObject.SetActive(false);
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].gameObject.activeSelf) _rings[i].gameObject.SetActive(false);
        }

        // Rings around nearby saplings and seedlings, red when they aren't growing. With the farm check, every
        // plant around you, nearest first.
        void UpdateNeighbourRings(Plant ghostPlant, Vector3 pos, bool show)
        {
            _neighbours.Clear();
            if (show)
            {
                float range = _farm ? FarmRange : NeighbourRange;
                foreach (var obj in SlowUpdate.GetAllInstaces())
                    if (obj is Plant p && p != ghostPlant && Utils.DistanceXZ(p.transform.position, pos) < range)
                        _neighbours.Add(p);
                int max = _farm ? MaxFarmRings : MaxNeighbourRings;
                if (_neighbours.Count > max)
                {
                    _neighbours.Sort((a, b) => Utils.DistanceXZ(a.transform.position, pos).CompareTo(Utils.DistanceXZ(b.transform.position, pos)));
                    _neighbours.RemoveRange(max, _neighbours.Count - max);
                }
            }

            while (_rings.Count < _neighbours.Count)
                _rings.Add(SpawnView.CreateLine(transform, "PlantRing", HealthyPlantColor, 0.04f));
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (i >= _neighbours.Count)
                {
                    if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                    continue;
                }
                var p = _neighbours[i];
                ring.gameObject.SetActive(true);
                ring.startColor = ring.endColor = p.GetStatus() == Plant.Status.Healthy ? HealthyPlantColor : UnhealthyPlantColor;
                SpawnView.DrawRing(ring, p.transform.position, p.m_growRadius);
            }
        }

        // Plant.UpdateHealth for a spot that has no plant yet; null when it would grow.
        internal static string WhyWontGrow(Plant plant, Vector3 pos)
        {
            Heightmap heightmap = Heightmap.FindHeightmap(pos);
            if (heightmap != null)
            {
                Heightmap.Biome biome = heightmap.GetBiome(pos);
                if ((biome & plant.m_biome) == 0) return "wrong biome";
                if (plant.m_needCultivatedGround && !heightmap.IsCultivated(pos)) return "ground isn't cultivated";
                bool shielded = ShieldGenerator.IsInsideShield(pos);
                if (!plant.m_tolerateHeat && biome == Heightmap.Biome.AshLands && !shielded) return "too hot";
                if (!plant.m_tolerateCold && (biome == Heightmap.Biome.DeepNorth || biome == Heightmap.Biome.Mountain) && !shielded)
                    return "too cold";
            }

            if (_roofMask == 0)
            {
                _roofMask = LayerMask.GetMask("Default", "static_solid", "piece");
                _spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
            }
            if (Physics.Raycast(pos, Vector3.up, out RaycastHit roof, 100f, _roofMask))
                return $"no open sky ({NameOf(roof.collider)} above)";

            int n = Physics.OverlapSphereNonAlloc(pos, plant.m_growRadius, Hits, _spaceMask);
            for (int i = 0; i < n; i++)
            {
                var other = Hits[i].GetComponent<Plant>();
                if (other == null) return $"too close to {NameOf(Hits[i])}";
                if (other.GetStatus() == Plant.Status.Healthy)
                    return $"too close to {Localization.instance.Localize(other.m_name)}";
            }
            // The game only checks the new seed's own grow space, so a seed may be placed inside a bigger
            // neighbour's and stop that one growing. Count the neighbour's space too.
            float footprint = FootprintRadius(plant.gameObject);
            n = Physics.OverlapSphereNonAlloc(pos, NeighbourReach, Hits, _spaceMask);
            for (int i = 0; i < n; i++)
            {
                var other = Hits[i].GetComponentInParent<Plant>();
                if (other == null || other == plant || other.GetStatus() != Plant.Status.Healthy) continue;
                if (Utils.DistanceXZ(other.transform.position, pos) < other.m_growRadius + footprint)
                    return $"{Localization.instance.Localize(other.m_name)} would stop growing";
            }
            if (plant.m_growRadiusVines > 0f)
            {
                n = Physics.OverlapSphereNonAlloc(pos, plant.m_growRadiusVines, Hits, _spaceMask);
                for (int i = 0; i < n; i++)
                    if (Hits[i].GetComponentInParent<Vine>() != null) return "too close to a vine";
            }
            if (plant.m_attachDistance > 0f && plant.GetClosestAttachObject(pos) == null) return "nothing to climb on";
            return null;
        }

        // Horizontal size of a seed's own collider: what a neighbour's grow check bumps into.
        internal static float FootprintRadius(GameObject seed)
        {
            float radius = 0f;
            foreach (var col in seed.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                Vector3 e = col.bounds.extents;
                radius = Mathf.Max(radius, Mathf.Max(e.x, e.z));
            }
            return Mathf.Min(radius, 0.5f);
        }

        static string NameOf(Collider collider)
        {
            var hoverable = collider.GetComponentInParent<Hoverable>();
            string name = hoverable?.GetHoverName();
            if (!string.IsNullOrEmpty(name)) return Localization.instance.Localize(name);
            var piece = collider.GetComponentInParent<Piece>();
            if (piece != null) return Localization.instance.Localize(piece.m_name);
            return Utils.GetPrefabName(collider.transform.root.gameObject).Replace('_', ' ');
        }
    }
}
