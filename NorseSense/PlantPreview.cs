using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // While planting with the cultivator: whether the seed would grow where the ghost stands and why not,
    // plus grow-space rings around the ghost and nearby plants. Same checks as Plant.UpdateHealth.
    internal class PlantPreview : MonoBehaviour
    {
        const float NeighbourRange = 8f;
        const int MaxNeighbourRings = 60;
        const float RefreshInterval = 0.2f;

        static readonly Color GrowColor = new Color(0.45f, 1f, 0.45f, 0.9f);
        static readonly Color BlockedColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        static readonly Color HealthyPlantColor = new Color(0.45f, 1f, 0.45f, 0.45f);
        static readonly Color UnhealthyPlantColor = new Color(1f, 0.4f, 0.2f, 0.8f);

        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // Shown in the build info box; null when not planting.
        internal static string Line;

        static int _spaceMask, _roofMask;
        static readonly Collider[] Hits = new Collider[64];

        LineRenderer _ghostRing;
        readonly List<LineRenderer> _rings = new List<LineRenderer>();
        readonly List<Plant> _neighbours = new List<Plant>();
        float _timer;

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
            if (plant == null)
            {
                Hide();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            Vector3 pos = ghost.transform.position;
            string problem = WhyWontGrow(plant, pos);
            string caveat = Compat.PlantRulesChangedBy != null ? $" <size=12>(may not be exact: {Compat.PlantRulesChangedBy})</size>" : "";
            Line = (problem == null
                ? $"{Localization.instance.Localize(plant.m_name)}: <color=#80ff80>will grow here</color>"
                : $"{Localization.instance.Localize(plant.m_name)}: <color=#ff7050>won't grow, {problem}</color>") + caveat;

            _ghostRing.gameObject.SetActive(true);
            _ghostRing.startColor = _ghostRing.endColor = problem == null ? GrowColor : BlockedColor;
            SpawnView.DrawRing(_ghostRing, pos, plant.m_growRadius);

            UpdateNeighbourRings(plant, pos, Plugin.ShowPlantSpacing.Value);
        }

        void Hide()
        {
            Line = null;
            if (_ghostRing.gameObject.activeSelf) _ghostRing.gameObject.SetActive(false);
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].gameObject.activeSelf) _rings[i].gameObject.SetActive(false);
        }

        // Rings around nearby saplings and seedlings, red when they aren't growing.
        void UpdateNeighbourRings(Plant ghostPlant, Vector3 pos, bool show)
        {
            _neighbours.Clear();
            if (show)
            {
                foreach (var obj in SlowUpdate.GetAllInstaces())
                {
                    if (obj is Plant p && p != ghostPlant && Utils.DistanceXZ(p.transform.position, pos) < NeighbourRange)
                    {
                        _neighbours.Add(p);
                        if (_neighbours.Count >= MaxNeighbourRings) break;
                    }
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
        static string WhyWontGrow(Plant plant, Vector3 pos)
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
            if (plant.m_growRadiusVines > 0f)
            {
                n = Physics.OverlapSphereNonAlloc(pos, plant.m_growRadiusVines, Hits, _spaceMask);
                for (int i = 0; i < n; i++)
                    if (Hits[i].GetComponentInParent<Vine>() != null) return "too close to a vine";
            }
            if (plant.m_attachDistance > 0f && plant.GetClosestAttachObject(pos) == null) return "nothing to climb on";
            return null;
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
