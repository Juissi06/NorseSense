using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Holding the fine-rotation key means "placing carefully", so it also shows the precision helpers:
    // the snap points of the ghost and of the pieces around it, with the pair that snaps highlighted,
    // and how high the ghost floats above the ground or the piece below.
    internal class PrecisionInfo : MonoBehaviour
    {
        const float RefreshInterval = 0.1f;
        const float SnapRange = 4f;       // pieces whose snap points are shown, around the ghost
        const int MaxPoints = 120;
        const float SnappedDistance = 0.02f;
        const float DotSize = 8f, SnappedDotSize = 13f;

        static readonly Color GhostDotColor = Ui.Alpha(Ui.GoldColor, 0.95f);
        static readonly Color PieceDotColor = new Color(1f, 1f, 1f, 0.75f);
        static readonly Color SnappedColor = Ui.GoodColor;

        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // Shown in the build info box while the key is held; null otherwise.
        internal static InfoRow HeightLine, SnapLine;

        static int _belowMask;
        static Texture2D _dot;

        readonly List<Transform> _ghostPoints = new List<Transform>();
        readonly List<Transform> _piecePoints = new List<Transform>();
        readonly List<Piece> _pieces = new List<Piece>();
        Vector3 _snapA, _snapB;
        bool _snapped, _active;
        float _timer;

        void Update()
        {
            var player = Player.m_localPlayer;
            var ghost = player != null && player.InPlaceMode() && BuildHelpers.FineModifierHeld() ? PlacementGhost(player) : null;
            var piece = ghost != null && ghost.activeInHierarchy ? ghost.GetComponent<Piece>() : null;
            // Seeds and terrain tools have nothing to snap and their own readouts.
            if (piece == null || ghost.GetComponent<Plant>() != null || ghost.GetComponent<TerrainOp>() != null)
            {
                _active = false;
                HeightLine = SnapLine = null;
                return;
            }
            _active = true;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            UpdateSnapPoints(piece);
            HeightLine = Height(ghost);
        }

        void UpdateSnapPoints(Piece ghostPiece)
        {
            _ghostPoints.Clear();
            _piecePoints.Clear();
            _pieces.Clear();
            ghostPiece.GetSnapPoints(_ghostPoints);
            Piece.GetSnapPoints(ghostPiece.transform.position, SnapRange, _piecePoints, _pieces);
            _piecePoints.RemoveAll(t => t == null || t.IsChildOf(ghostPiece.transform));

            // The game snaps the closest pair within 0.5 m together, so a snapped pair sits on the same spot.
            _snapped = false;
            float best = SnappedDistance;
            foreach (var a in _ghostPoints)
            foreach (var b in _piecePoints)
            {
                float d = Vector3.Distance(a.position, b.position);
                if (d > best) continue;
                best = d;
                _snapped = true;
                _snapA = a.position;
                _snapB = b.position;
            }

            if (_ghostPoints.Count == 0) SnapLine = null;
            else if (_snapped)
                SnapLine = new InfoRow("Snap", "Snapped", $"{_ghostPoints.Count} snap points on this piece", Tone.Good);
            else
                SnapLine = new InfoRow("Snap", "Free", _piecePoints.Count > 0 ? "move a gold point onto a white one" : "no snap points nearby");
        }

        // Height of the ghost's lowest point above whatever is under it.
        static InfoRow Height(GameObject ghost)
        {
            if (_belowMask == 0) _belowMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            Bounds bounds = default;
            bool any = false;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return null;

            var from = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 50f, _belowMask))
                return new InfoRow("Height", "Over 50 m up", null);
            float h = bounds.min.y - hit.point.y;
            string below = hit.collider.GetComponentInParent<Heightmap>() != null ? "the ground" : NameBelow(hit.collider);
            if (h < 0.05f) return new InfoRow("Height", $"On {below}", null);
            return new InfoRow("Height", $"<b>{h:0.00} m</b> above {below}", null);
        }

        static string NameBelow(Collider collider)
        {
            var piece = collider.GetComponentInParent<Piece>();
            if (piece != null) return Localization.instance.Localize(piece.m_name);
            var hoverable = collider.GetComponentInParent<Hoverable>();
            string name = hoverable?.GetHoverName();
            return !string.IsNullOrEmpty(name) ? Localization.instance.Localize(name) : "something";
        }

        void OnGUI()
        {
            if (!_active || Event.current.type != EventType.Repaint || !Plugin.ShowBuildInfo.Value) return;
            if (Hud.IsUserHidden() || Hud.IsPieceSelectionVisible() || Menu.IsVisible()) return;
            var cam = Utils.GetMainCamera();
            if (cam == null) return;
            if (_dot == null) _dot = MakeDot();

            // Sized for 1080p and grown with the screen, so they stay visible at 1440p and 4K.
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            float dot = DotSize * scale, snapped = SnappedDotSize * scale;
            int shown = 0;
            foreach (var t in _piecePoints)
            {
                if (t == null || ++shown > MaxPoints) break;
                Dot(cam, t.position, dot, PieceDotColor);
            }
            foreach (var t in _ghostPoints)
                if (t != null) Dot(cam, t.position, dot, GhostDotColor);
            if (_snapped)
            {
                Dot(cam, _snapA, snapped, SnappedColor);
                Dot(cam, _snapB, snapped, SnappedColor);
            }
        }

        static void Dot(Camera cam, Vector3 world, float size, Color color)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z <= 0f) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(s.x - size * 0.5f, Screen.height - s.y - size * 0.5f, size, size), _dot);
            GUI.color = old;
        }

        // A white disc with a dark rim, so it reads on light and dark surfaces alike.
        static Texture2D MakeDot()
        {
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "NorseSense_SnapDot" };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(n * 0.5f - d);
                float shade = d > n * 0.5f - 2.5f ? 0.15f : 1f;
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
            tex.Apply(false);
            return tex;
        }

        void OnDestroy()
        {
            if (_dot != null) Destroy(_dot);
        }
    }
}
