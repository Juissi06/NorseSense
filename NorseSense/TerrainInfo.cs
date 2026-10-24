using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // How far the ground can still be raised or lowered where you aim the hoe or pickaxe.
    // The game keeps modified terrain within 8 m of its original height (TerrainComp.ApplyToHeightmap).
    internal class TerrainInfo : MonoBehaviour
    {
        const float MaxOffset = 8f;
        const float PickaxeReach = 5f;
        const float RefreshInterval = 0.2f;

        delegate bool BaseHeightFn(Heightmap heightmap, Vector3 worldPos, out float height);

        // Null if a game update renamed it; the feature then stays quiet.
        static readonly BaseHeightFn GetWorldBaseHeight = CreateBaseHeight();
        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // Shown in the build info box; null when not aiming a terrain tool.
        internal static string Line;

        static int _terrainMask, _terrainLayer;
        float _timer;
        bool _pickaxe;
        readonly List<string> _lines = new List<string>(1);

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            Line = null;
            _pickaxe = false;
            var player = Player.m_localPlayer;
            if (player == null || !Plugin.ShowTerrainLimits.Value || GetWorldBaseHeight == null) return;

            if (player.InPlaceMode())
            {
                // Read the selected prefab rather than the ghost, which the game strips of terrain components.
                var ghost = PlacementGhost(player);
                var piece = player.GetSelectedPiece();
                if (ghost == null || !ghost.activeInHierarchy || piece == null) return;
                var op = piece.GetComponent<TerrainOp>();
                if (op != null && ChangesHeight(op.m_settings))
                    Line = Describe(ghost.transform.position);
                return;
            }

            var weapon = player.GetCurrentWeapon();
            var dig = weapon?.m_shared.m_spawnOnHitTerrain;
            var digOp = dig != null ? dig.GetComponent<TerrainOp>() : null;
            if (digOp == null || !ChangesHeight(digOp.m_settings) || GameCamera.instance == null) return;

            if (_terrainMask == 0)
            {
                _terrainLayer = LayerMask.NameToLayer("terrain");
                _terrainMask = LayerMask.GetMask("terrain", "Default", "static_solid", "piece");
            }
            var cam = GameCamera.instance.transform;
            // Only when the pickaxe would hit the ground, not a rock or a building.
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 50f, _terrainMask) &&
                hit.collider.gameObject.layer == _terrainLayer &&
                Vector3.Distance(hit.point, player.transform.position) < PickaxeReach)
            {
                Line = Describe(hit.point);
                _pickaxe = Line != null;
            }
        }

        static BaseHeightFn CreateBaseHeight()
        {
            try
            {
                return AccessTools.MethodDelegate<BaseHeightFn>(AccessTools.Method(typeof(Heightmap), "GetWorldBaseHeight"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Norse Sense] Terrain limits unavailable: " + e.Message);
                return null;
            }
        }

        static bool ChangesHeight(TerrainOp.Settings s) => s.m_level || s.m_raise || s.m_smooth;

        static string Describe(Vector3 pos)
        {
            var heightmap = Heightmap.FindHeightmap(pos);
            if (heightmap == null || !heightmap.GetWorldHeight(pos, out float now)) return null;
            float original;
            try
            {
                if (!GetWorldBaseHeight(heightmap, pos, out original)) return null;
            }
            catch (System.NullReferenceException)
            {
                return null; // heightmap not built yet
            }

            float offset = now - original;
            float down = Mathf.Max(0f, offset + MaxOffset);
            float up = Mathf.Max(0f, MaxOffset - offset);
            string where = Mathf.Abs(offset) < 0.05f ? "untouched"
                : offset > 0f ? $"{offset:0.0} m above original" : $"{-offset:0.0} m below original";

            if (down < 0.05f) return $"Ground: {where}, <color=#ff6f55>at the dig limit</color>  <size=12>(±{MaxOffset:0} m)</size>";
            if (up < 0.05f) return $"Ground: {where}, <color=#ff6f55>at the raise limit</color>  <size=12>(±{MaxOffset:0} m)</size>";
            string downText = down < 1f ? $"<color=#f0b04a>{down:0.0} m</color>" : $"{down:0.0} m";
            string upText = up < 1f ? $"<color=#f0b04a>{up:0.0} m</color>" : $"{up:0.0} m";
            return $"Ground: {where}. Can dig {downText} deeper, raise {upText} higher";
        }

        // Outside build mode (pickaxe), draw the line on its own; in build mode BuildHelpers includes it.
        void OnGUI()
        {
            if (!_pickaxe || Line == null || !Plugin.ShowBuildInfo.Value) return;
            if (Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;
            _lines.Clear();
            _lines.Add(Line);
            BuildHelpers.DrawInfoBox(_lines);
        }
    }
}
