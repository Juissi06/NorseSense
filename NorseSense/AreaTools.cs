using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Area tools: plant a row or grid of seeds, or hoe and cultivate a bigger area, in one click.
    // Every extra seed or tile is an ordinary vanilla placement of the same piece, paid for like a click of
    // its own (seed, stamina, durability, skill), so other players and vanilla servers see exactly what
    // they'd see if you clicked each one. The hoe isn't given a bigger radius because the game sends only
    // the tool's id and whoever owns the terrain applies their own radius; tiles work for everyone.
    internal class AreaTools : MonoBehaviour
    {
        const float ValidateInterval = 0.1f; // positions follow the ghost every frame; growth checks a bit less often
        const float ScrollCooldown = 0.08f;
        // Tiles one radius apart: no gaps (circles cover a square grid up to r * sqrt(2)), and each size step adds
        // about one radius on each side, so the area grows in even steps instead of jumping.
        const float TileSpacing = 1f;

        internal enum Kind { None, Plant, Terrain }

        // Shapes the size key steps through: columns across, rows forward.
        static readonly int[,] PlantShapes = { { 1, 1 }, { 1, 3 }, { 1, 5 }, { 3, 3 }, { 5, 5 } };
        // 1 × 3 is a strip along where you look, for paths and roads.
        static readonly int[,] TerrainShapes = { { 1, 1 }, { 1, 3 }, { 2, 2 }, { 3, 3 }, { 4, 4 } };

        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
        static readonly AccessTools.FieldRef<Player, PieceTable> BuildPieces =
            AccessTools.FieldRefAccess<Player, PieceTable>("m_buildPieces");
        static readonly AccessTools.FieldRef<Player, int> BuildRemoveDebt = AccessTools.FieldRefAccess<Player, int>("m_buildRemoveDebt");
        static readonly Func<Player, float> GetBuildStamina =
            AccessTools.MethodDelegate<Func<Player, float>>(AccessTools.Method(typeof(Player), "GetBuildStamina"));
        static readonly Func<Player, ItemDrop.ItemData, float> GetPlaceDurability =
            AccessTools.MethodDelegate<Func<Player, ItemDrop.ItemData, float>>(AccessTools.Method(typeof(Player), "GetPlaceDurability"));

        static readonly Color GoodColor = new Color(0.45f, 1f, 0.45f, 0.9f);
        static readonly Color BadColor = new Color(1f, 0.3f, 0.2f, 0.9f);
        static readonly Color AreaColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        static int _plantShape, _terrainShape;

        // What the ghost would cover right now; also used for the info box row.
        internal static Kind Current;
        internal static int Columns = 1, Rows = 1, Valid;
        static readonly List<Vector3> Cells = new List<Vector3>(25);
        static readonly List<bool> CellOk = new List<bool>(25);
        static Quaternion _gridRotation;
        static int _centerIndex;
        static float _spacing, _tileRadius, _growRadius, _stableYaw = float.NaN, _scrollReadyAt;

        // A click that placed the centre piece; the extras follow once vanilla has paid for it.
        static Piece _pending;
        static readonly List<Vector3> PendingCells = new List<Vector3>(25);
        static Quaternion _pendingRotation;

        readonly List<LineRenderer> _rings = new List<LineRenderer>();
        readonly List<Vector3> _ringDrawnAt = new List<Vector3>();
        LineRenderer _outline;
        float _timer;

        internal static bool ModifierHeld() =>
            Plugin.AreaToolsEnabled.Value && Plugin.AreaModifier.Value != KeyCode.None &&
            BepInEx.UnityInput.Current.GetKey(Plugin.AreaModifier.Value);

        void Awake()
        {
            _outline = SpawnView.CreateLine(transform, "AreaOutline", AreaColor, 0.06f);
            _outline.gameObject.SetActive(false);
        }

        void Update()
        {
            HandleScroll();
            _timer -= Time.deltaTime;
            bool validate = _timer <= 0f;
            if (validate) _timer = ValidateInterval;
            Refresh(Player.m_localPlayer, validate);
            DrawPreview();
        }

        // Read the wheel directly: hoe pieces can't rotate, so for them the game zooms the camera instead of
        // turning the piece, and there is no rotation change to react to.
        static void HandleScroll()
        {
            if (!ScrollCaptured() || Plugin.IsTyping() || Hud.IsPieceSelectionVisible() || Time.time < _scrollReadyAt) return;
            float scroll = ZInput.GetMouseScrollWheel();
            if (Mathf.Abs(scroll) < 0.001f) return;
            _scrollReadyAt = Time.time + ScrollCooldown;
            StepSize(scroll > 0f ? 1 : -1);
        }

        // While true the wheel belongs to the area size: no piece rotation, no camera zoom.
        internal static bool ScrollCaptured() => Current != Kind.None && ModifierHeld();

        void OnDisable()
        {
            Current = Kind.None;
            Cells.Clear();
            DrawPreview();
        }

        static Kind KindOf(Player player, out Piece piece, out GameObject ghost)
        {
            piece = null;
            ghost = player != null && player.InPlaceMode() ? PlacementGhost(player) : null;
            if (!Plugin.AreaToolsEnabled.Value || ghost == null || !ghost.activeInHierarchy) return Kind.None;
            piece = player.GetSelectedPiece();
            if (piece == null) return Kind.None;
            if (piece.GetComponent<Plant>() != null)
                return Compat.AreaPlantingDisabledBy == null ? Kind.Plant : Kind.None;
            // Read the prefab: the game strips terrain components from the ghost.
            var op = piece.GetComponent<TerrainOp>();
            if (op != null && Plugin.AreaTerrain.Value && Compat.AreaTerrainDisabledBy == null && !op.m_settings.m_raise)
                return Kind.Terrain;
            return Kind.None;
        }

        static void Refresh(Player player, bool validate)
        {
            int previous = Cells.Count;
            Cells.Clear();
            if (validate || previous == 0) CellOk.Clear();
            Valid = 0;
            Current = KindOf(player, out var piece, out var ghost);
            if (Current == Kind.None) return;

            int[,] shapes = Current == Kind.Plant ? PlantShapes : TerrainShapes;
            int index = Mathf.Clamp(Current == Kind.Plant ? _plantShape : _terrainShape, 0, shapes.GetLength(0) - 1);
            Columns = shapes[index, 0];
            Rows = shapes[index, 1];

            Vector3 center = ghost.transform.position;
            if (Current == Kind.Plant)
            {
                var plant = ghost.GetComponent<Plant>();
                _growRadius = plant.m_growRadius;
                // Just outside each other's grow space, with a little room so rounding never puts two in reach.
                _spacing = plant.m_growRadius + PlantPreview.FootprintRadius(ghost) + 0.1f;
                // Rows follow the ghost's rotation, so scroll, fine rotation and match rotation line them up with a
                // fence. Pieces that re-roll their rotation each placement follow the view instead, but only once
                // you turn well away, so the grid doesn't twitch as you look around.
                _gridRotation = piece.m_randomInitBuildRotation ? StableViewYaw() : Quaternion.Euler(0f, ghost.transform.eulerAngles.y, 0f);
            }
            else
            {
                _tileRadius = CoverRadius(piece.GetComponent<TerrainOp>().m_settings);
                _spacing = _tileRadius * TileSpacing;
                // Hoe pieces don't rotate, so a strip follows the view instead (steadied like a seed row).
                _gridRotation = Columns == 1 && Rows > 1 ? StableViewYaw() : Quaternion.Euler(0f, ghost.transform.eulerAngles.y, 0f);
            }

            bool level = Current == Kind.Terrain && piece.GetComponent<TerrainOp>().m_settings.m_level;
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                // The aimed spot is always one of the cells; even sizes grow forward and to the right of it.
                var offset = new Vector3((c - (Columns - 1) / 2) * _spacing, 0f, (r - (Rows - 1) / 2) * _spacing);
                if (offset == Vector3.zero) _centerIndex = Cells.Count;
                Vector3 p = center + _gridRotation * offset;
                // Levelling flattens every tile to the ghost's height; everything else sits on the ground there.
                if (!level && ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out float h)) p.y = h;
                else if (level) p.y = center.y;
                int cell = Cells.Count;
                Cells.Add(p);
                if (cell >= CellOk.Count) CellOk.Add(offset == Vector3.zero || CellAllowed(Current, ghost, p));
                if (CellOk[cell]) Valid++;
            }
        }

        // The part of the op that has to tile without gaps: for levelling that's the level radius, not the wider
        // smoothing around it, or strips between tiles would stay unlevelled.
        static float CoverRadius(TerrainOp.Settings s)
        {
            if (s.m_level) return s.m_levelRadius;
            if (s.m_paintCleared) return s.m_paintRadius;
            if (s.m_smooth) return s.m_smoothRadius;
            return s.GetRadius();
        }

        static bool CellAllowed(Kind kind, GameObject ghost, Vector3 p)
        {
            if (Location.IsInsideNoBuildLocation(p) || !PrivateArea.CheckAccess(p, 0f, false, false)) return false;
            return kind != Kind.Plant || PlantPreview.WhyWontGrow(ghost.GetComponent<Plant>(), p) == null;
        }

        static Quaternion StableViewYaw()
        {
            float yaw = GameCamera.instance != null ? GameCamera.instance.transform.eulerAngles.y : 0f;
            if (float.IsNaN(_stableYaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, _stableYaw)) > 35f)
                _stableYaw = Mathf.Round(yaw / 45f) * 45f;
            return Quaternion.Euler(0f, _stableYaw, 0f);
        }

        void DrawPreview()
        {
            bool show = Current != Kind.None && Cells.Count > 1;
            int rings = show && Current == Kind.Plant ? Cells.Count : 0;
            while (_rings.Count < rings)
            {
                _rings.Add(SpawnView.CreateLine(transform, "AreaCell", GoodColor, 0.04f));
                _ringDrawnAt.Add(Vector3.positiveInfinity);
            }
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                bool on = i < rings && i != _centerIndex; // the aimed spot is the planting preview's own ring
                if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
                if (!on)
                {
                    _ringDrawnAt[i] = Vector3.positiveInfinity;
                    continue;
                }
                ring.startColor = ring.endColor = CellOk[i] ? GoodColor : BadColor;
                if ((Cells[i] - _ringDrawnAt[i]).sqrMagnitude < 0.0001f) continue;
                DrawFlatRing(ring, Cells[i], _growRadius);
                _ringDrawnAt[i] = Cells[i];
            }

            bool outline = show && Current == Kind.Terrain;
            if (_outline.gameObject.activeSelf != outline) _outline.gameObject.SetActive(outline);
            if (outline)
                DrawSquare(_outline, AreaCenter(), (Columns - 1) * 0.5f * _spacing + _tileRadius, (Rows - 1) * 0.5f * _spacing + _tileRadius, _gridRotation);
        }

        // Middle of the whole area, which for even sizes isn't the aimed spot.
        static Vector3 AreaCenter()
        {
            Vector3 sum = Vector3.zero;
            foreach (var cell in Cells) sum += cell;
            Vector3 mid = sum / Cells.Count;
            mid.y = Cells[_centerIndex].y;
            return mid;
        }

        // Flat ring at the seed's height: cheap enough to move every frame with the ghost.
        static void DrawFlatRing(LineRenderer lr, Vector3 center, float radius)
        {
            const int segments = 20;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + 0.08f, center.z + Mathf.Sin(a) * radius));
            }
        }

        // Square outline that follows the ground, for the area a hoe click will cover.
        static void DrawSquare(LineRenderer lr, Vector3 center, float halfX, float halfZ, Quaternion rotation)
        {
            const int perSide = 12;
            lr.positionCount = perSide * 4;
            Vector3[] corners =
            {
                new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ), new Vector3(halfX, 0f, -halfZ)
            };
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < perSide; i++)
            {
                Vector3 local = Vector3.Lerp(corners[side], corners[(side + 1) % 4], i / (float)perSide);
                Vector3 p = center + rotation * local;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out float h)) p.y = h;
                p.y += 0.3f;
                lr.SetPosition(side * perSide + i, p);
            }
        }

        // ---------- Size key ----------

        internal static void StepSize(int direction)
        {
            if (Current == Kind.Plant)
                _plantShape = Mathf.Clamp(_plantShape + direction, 0, MaxShape(PlantShapes, Plugin.MaxPlantGrid.Value));
            else if (Current == Kind.Terrain)
                _terrainShape = Mathf.Clamp(_terrainShape + direction, 0, MaxShape(TerrainShapes, Plugin.MaxHoeSize.Value));
        }

        // The largest shape whose sides fit the size setting ("Max planting size" or "Max hoe size").
        static int MaxShape(int[,] shapes, int maxSide)
        {
            int max = 0;
            for (int i = 0; i < shapes.GetLength(0); i++)
                if (shapes[i, 0] <= maxSide && shapes[i, 1] <= maxSide) max = i;
            return max;
        }

        // ---------- Info box ----------

        internal static void AddRows(InfoLines lines)
        {
            if (Current == Kind.None || Cells.Count <= 1) return;
            string shape = Columns == 1 ? $"Row of {Rows}" : $"{Columns} × {Rows}";
            if (Current == Kind.Plant)
                lines.Add(null, "Area", $"{shape}, {Valid} of {Cells.Count} can grow", "one seed and stamina each",
                    Valid == Cells.Count ? Tone.Good : Tone.Warn);
            else
                lines.Add(null, "Area", $"{shape}, {Cells.Count} tiles", "stamina and durability per tile",
                    Valid == Cells.Count ? Tone.None : Tone.Warn);
        }

        // Footer hint when nothing else is using the footer.
        internal static void AddHint(InfoLines lines)
        {
            if (Current != Kind.None && lines.HintKeys.Count == 0 && Plugin.AreaModifier.Value != KeyCode.None)
                lines.Hint($"{Ui.KeyName(Plugin.AreaModifier.Value)} + Scroll", "Area size");
        }

        // ---------- Placing ----------

        // Called when vanilla placed the centre piece: remember the other cells while the ghost is still there.
        internal static void OnPlaced(Player player, Piece piece)
        {
            _pending = null;
            Refresh(player, true);
            if (Current == Kind.None || Cells.Count <= 1) return;
            PendingCells.Clear();
            Vector3 center = Cells[_centerIndex];
            for (int i = 0; i < Cells.Count; i++)
                if (Cells[i] != center) PendingCells.Add(Cells[i]);
            // Nearest first, so running out of seeds or stamina leaves a tidy patch.
            PendingCells.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
            _pending = piece;
            _pendingRotation = PlacementGhost(player).transform.rotation;
        }

        // After vanilla paid for the centre: place each extra cell that is still allowed and affordable.
        internal static void PlacePending(Player player)
        {
            var piece = _pending;
            _pending = null;
            if (piece == null) return;

            var ghost = PlacementGhost(player);
            // The hammer, hoe or cultivator: GetCurrentWeapon would give bare hands, since tools aren't weapons.
            var tool = StaminaPatches.RightItem(player);
            var table = BuildPieces(player);
            Kind kind = piece.GetComponent<Plant>() != null ? Kind.Plant : Kind.Terrain;
            int placed = 0;
            string stopped = null;
            foreach (var cell in PendingCells)
            {
                bool free = player.PlacementCostDisabled;
                if (!free && !player.HaveRequirements(piece, Player.RequirementMode.CanBuild)) { stopped = "out of materials"; break; }
                // GetBuildStamina already has the stamina setting in it; HaveStamina would apply it again here,
                // since this runs inside UpdatePlacement.
                float stamina = GetBuildStamina(player);
                if (player.GetStamina() < stamina) { stopped = "out of stamina"; break; }
                if (tool != null && tool.m_shared.m_useDurability && tool.m_durability <= 0f) { stopped = "tool broken"; break; }
                if (ghost == null || !CellAllowed(kind, ghost, cell)) continue;

                Quaternion rotation = kind == Kind.Plant ? Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) : _pendingRotation;
                player.PlacePiece(piece, cell, rotation, doAttack: false);
                placed++;

                // The same costs vanilla charges for a click (Player.UpdatePlacement).
                if (!free && !ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())) player.ConsumeResources(piece.m_resources, 0);
                player.UseStamina(stamina);
                // As vanilla: removing pieces runs up a debt that placing pays off before any skill is gained.
                if (table != null && table.m_skill != 0)
                {
                    if (BuildRemoveDebt(player) > 0) BuildRemoveDebt(player)--;
                    else player.RaiseSkill(table.m_skill);
                }
                if (tool != null && tool.m_shared.m_useDurability)
                    tool.m_durability -= GetPlaceDurability(player, tool) * Game.m_durabilityRate;
            }
            if (stopped != null)
                Plugin.Tell("Area", $"placed {placed + 1} of {PendingCells.Count + 1}, {stopped}", Tone.Warn);
        }
    }

    // L Ctrl + scroll changes the area size instead of rotating the piece; the extras are placed after a click.
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    static class AreaToolsPlacementPatch
    {
        static void Prefix(Player __instance, out int __state) => __state = BuildHelpers.PlaceRotation(__instance);

        static void Postfix(Player __instance, int __state)
        {
            if (__instance != Player.m_localPlayer) return;
            // The wheel is the area size right now (AreaTools.HandleScroll), so undo the piece rotation it caused.
            if (AreaTools.ScrollCaptured()) BuildHelpers.PlaceRotation(__instance) = __state;
            AreaTools.PlacePending(__instance);
        }
    }

    // Pieces that can't rotate (the hoe's) make the wheel zoom the camera; hold the zoom while it sizes the area.
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    static class AreaToolsZoomPatch
    {
        static readonly AccessTools.FieldRef<GameCamera, float> Distance = AccessTools.FieldRefAccess<GameCamera, float>("m_distance");

        static void Prefix(GameCamera __instance, out float __state) => __state = Distance(__instance);

        static void Postfix(GameCamera __instance, float __state)
        {
            if (AreaTools.ScrollCaptured()) Distance(__instance) = __state;
        }
    }

    // Crouch is a toggle in Valheim, and the default size key is Left Ctrl (Crouch): without this every size
    // change would switch sneaking on or off.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    static class AreaToolsCrouchPatch
    {
        static void Prefix(Player __instance, ref bool crouch)
        {
            if (crouch && __instance == Player.m_localPlayer && AreaTools.Current != AreaTools.Kind.None && AreaTools.ModifierHeld())
                crouch = false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    static class AreaToolsClickPatch
    {
        static void Postfix(Player __instance, Piece piece, bool __result)
        {
            if (__result && __instance == Player.m_localPlayer) AreaTools.OnPlaced(__instance, piece);
        }
    }
}
