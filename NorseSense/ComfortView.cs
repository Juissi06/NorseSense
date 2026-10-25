using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NorseSense
{
    // Base view, Comfort mode: colours the floor around you by the comfort you'd get standing there
    // (SE_Rested.CalculateComfortLevel with the shelter check at that spot), and rings the best spot,
    // so you can see where the bed or chair should go.
    internal class ComfortView : MonoBehaviour
    {
        const float Radius = 12f;    // comfort pieces count within 10 m, so a bit more shows the falloff
        const float Cell = 1f;
        const float StandHeight = 1f; // shelter is checked at your centre, about a metre up
        const float NeedCover = 0.8f; // Player.InShelter
        const float FloorOffset = 0.06f;
        const float RefreshInterval = 3f;
        const int CellsPerFrame = 60;

        internal static readonly Color NoShelterColor = new Color(1f, 0.55f, 0.2f, 0.28f);
        internal static readonly Color LowColor = new Color(1f, 0.85f, 0.3f, 0.5f);
        internal static readonly Color HighColor = new Color(0.3f, 1f, 0.45f, 0.6f);

        static int _floorMask;

        GameObject _root;
        Mesh _mesh;
        LineRenderer _bestRing;
        Vector3[] _verts;
        Color[] _colors;
        int[] _levels;
        int _n;

        bool _active, _scanning;
        float _timer;
        Vector3 _scanCenter;
        int _best, _here, _sheltered;
        readonly BaseView.Legend _legend = new BaseView.Legend();

        void Awake()
        {
            _root = new GameObject("ComfortView");
            _root.transform.SetParent(transform, false);
            _root.SetActive(false);

            var grid = new GameObject("Grid");
            grid.transform.SetParent(_root.transform, false);
            _mesh = new Mesh { name = "NorseSense_ComfortGrid", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            grid.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = grid.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpawnView.GetMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _bestRing = SpawnView.CreateLine(_root.transform, "Best", HighColor, 0.08f);
            _bestRing.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            _scanning = false;
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        void Update()
        {
            var player = Player.m_localPlayer;
            bool active = BaseView.Mode == ViewMode.Comfort && player != null && ZoneSystem.instance != null;
            if (active != _active)
            {
                _active = active;
                _root.SetActive(active);
                if (!active)
                {
                    StopAllCoroutines();
                    _scanning = false;
                    _mesh.Clear();
                    _n = 0;
                }
                _timer = 0f;
            }
            if (!active) return;

            _here = player.GetComfortLevel();
            _timer -= Time.deltaTime;
            if (!_scanning && (_timer <= 0f || Utils.DistanceXZ(player.transform.position, _scanCenter) > 3f))
            {
                _timer = RefreshInterval;
                StartCoroutine(Scan(player.transform.position));
            }
        }

        IEnumerator Scan(Vector3 center)
        {
            _scanning = true;
            _scanCenter = center;
            if (_floorMask == 0) _floorMask = LayerMask.GetMask("piece", "terrain", "Default", "static_solid");

            int n = Mathf.CeilToInt(Radius * 2f / Cell);
            if (n != _n) Allocate(n);
            float originX = Mathf.Floor(center.x / Cell) * Cell - n / 2 * Cell;
            float originZ = Mathf.Floor(center.z / Cell) * Cell - n / 2 * Cell;

            int best = 0, sheltered = 0, bestCell = -1;
            float[] floors = new float[n * n];
            for (int c = 0; c < n * n; c++)
            {
                if (c > 0 && c % CellsPerFrame == 0)
                {
                    yield return null;
                    if (Player.m_localPlayer == null) { _scanning = false; yield break; }
                }
                int x = c % n, z = c / n;
                var p = new Vector3(originX + (x + 0.5f) * Cell, center.y, originZ + (z + 0.5f) * Cell);
                _levels[c] = -1;
                floors[c] = float.NaN;
                if (Utils.DistanceXZ(p, center) > Radius) continue;

                // The floor you'd stand on at about your height: piece floors and stairs, not the ground under the house.
                // Starting low keeps the ray under the floor above (a storey is 2 m).
                if (!Physics.Raycast(p + Vector3.up * 1.4f, Vector3.down, out var hit, 4.5f, _floorMask) || hit.normal.y < 0.7f) continue;
                floors[c] = hit.point.y;
                Vector3 stand = hit.point + Vector3.up * StandHeight;
                Cover.GetCoverForPoint(stand, out float cover, out bool underRoof);
                bool shelter = underRoof && cover >= NeedCover;
                int level = SE_Rested.CalculateComfortLevel(shelter, hit.point);
                _levels[c] = shelter ? level : 0;
                if (!shelter) continue;
                sheltered++;
                if (level > best)
                {
                    best = level;
                    bestCell = c;
                }
            }

            // Colour against the best level in view, so differences inside one room stay visible.
            int low = 2;
            for (int c = 0; c < n * n; c++)
            {
                int x = c % n, z = c / n, v = c * 4;
                float x0 = originX + x * Cell, z0 = originZ + z * Cell;
                Color col = Color.clear;
                float y = float.IsNaN(floors[c]) ? center.y - 100f : floors[c] + FloorOffset;
                if (_levels[c] == 0) col = NoShelterColor;
                else if (_levels[c] > 0) col = Color.Lerp(LowColor, HighColor, best > low ? (float)(_levels[c] - low) / (best - low) : 1f);
                _verts[v] = new Vector3(x0 + 0.05f, y, z0 + 0.05f);
                _verts[v + 1] = new Vector3(x0 + 0.05f, y, z0 + Cell - 0.05f);
                _verts[v + 2] = new Vector3(x0 + Cell - 0.05f, y, z0 + Cell - 0.05f);
                _verts[v + 3] = new Vector3(x0 + Cell - 0.05f, y, z0 + 0.05f);
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = col;
            }
            _mesh.vertices = _verts;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();

            _bestRing.gameObject.SetActive(bestCell >= 0);
            if (bestCell >= 0)
            {
                var c = new Vector3(originX + (bestCell % n + 0.5f) * Cell, floors[bestCell] + FloorOffset, originZ + (bestCell / n + 0.5f) * Cell);
                DrawFlatRing(_bestRing, c, 0.6f);
            }

            _best = best;
            _sheltered = sheltered;
            _scanning = false;
        }

        void Allocate(int n)
        {
            _n = n;
            _verts = new Vector3[n * n * 4];
            _colors = new Color[n * n * 4];
            _levels = new int[n * n];
            var tris = new int[n * n * 6];
            for (int c = 0; c < n * n; c++)
            {
                int v = c * 4, t = c * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            _mesh.Clear();
            _mesh.vertices = _verts;
            _mesh.colors = _colors;
            _mesh.triangles = tris;
        }

        // A ring at floor height (SpawnView.DrawRing follows the terrain, which is below indoor floors).
        static void DrawFlatRing(LineRenderer lr, Vector3 center, float radius)
        {
            const int segments = 24;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + 0.05f, center.z + Mathf.Sin(a) * radius));
            }
        }

        void OnGUI()
        {
            if (!_active || !BaseView.LegendVisible) return;
            if (Event.current.type == EventType.Layout) BuildLegend();
            BaseView.DrawLegend(_legend);
        }

        void BuildLegend()
        {
            _legend.Clear();
            if (_best > 0)
            {
                _legend.BarFraction = Mathf.Clamp01((float)_here / _best);
                _legend.BarText = $"Here <b>{_here}</b>, best <b>{_best}</b> <size=12><color={Ui.Muted}>(Rested {ComfortInfo.RestedMinutes(_best):0} min)</color></size>";
                _legend.BarColor = Color.Lerp(LowColor, HighColor, _legend.BarFraction);
            }
            _legend.Add(HighColor, "Best (ringed)");
            _legend.Add(LowColor, "Less comfort");
            _legend.Add(NoShelterColor, "Not sheltered");
            if (_sheltered == 0) _legend.Add($"<color={Ui.Warn}>No sheltered floor nearby</color>");
        }
    }
}
