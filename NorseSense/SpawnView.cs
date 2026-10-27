using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace NorseSense
{
    // Shows Valheim's natural spawn rules on the ground around the player (see SpawnRules),
    // plus base coverage rings, nests/spawners, raid status and a legend.
    internal class SpawnView : MonoBehaviour
    {
        const float GroundOffset = 0.15f;
        const float GridRefreshInterval = 2f;
        const float MarkerRefreshInterval = 1.5f;

        static readonly Color BaseRingColor = Ui.Alpha(Ui.GoodColor, 0.85f);
        static readonly Color NestTriggerColor = Ui.Alpha(Ui.WarnColor, 0.8f);
        static readonly Color NestSpawnColor = Ui.Alpha(Ui.BadColor, 0.9f);
        static readonly Color SpawnerActiveColor = Ui.Alpha(Ui.BadColor, 0.8f);
        static readonly Color SpawnerBlockedColor = Ui.Alpha(Ui.GoodColor, 0.8f);

        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        static Material _material;

        GameObject _viewRoot;
        Mesh _mesh;
        Vector3[] _verts;
        Color[] _colors;
        SpawnCell[] _cells;
        Vector3[] _cellPoints;
        float[] _heights;
        int _n;
        float _gridTimer;
        float _colorTimer;
        float _markerTimer;
        Vector3 _gridCenter;
        int _protected, _exposed, _steep, _preview, _exposedLeft, _water, _raidOnly;
        bool _noBase; // nothing protects any ground in view: the ground isn't painted, the legend says so

        LineRenderer _ghostRing;
        LineRenderer _raidRing;
        RandomEvent _raidRingEvent;
        // The outer edge of everything your base pieces protect, as arcs: one ring per fire or workbench turned
        // a village into a tangle of circles.
        readonly List<LineRenderer> _outline = new List<LineRenderer>();
        readonly List<Vector3> _sphereCenters = new List<Vector3>();
        readonly List<float> _sphereRadii = new List<float>();
        readonly List<Vector3> _arc = new List<Vector3>();
        int _outlineKey;
        readonly Dictionary<SpawnArea, GameObject> _nestMarkers = new Dictionary<SpawnArea, GameObject>();
        readonly Dictionary<CreatureSpawner, LineRenderer> _spawnerMarkers = new Dictionary<CreatureSpawner, LineRenderer>();
        readonly List<Object> _toRemove = new List<Object>();

        GameObject _ghost;
        EffectArea _ghostArea;
        bool _ghostActive;
        Vector3 _ghostCenter;
        float _ghostRadius;
        Vector3 _ghostRingDrawnAt;

        int _baseValue;
        int _unlockedRaids;

        readonly BaseView.Legend _legend = new BaseView.Legend();

        void Awake()
        {
            _viewRoot = new GameObject("View");
            _viewRoot.transform.SetParent(transform, false);
            _viewRoot.SetActive(false);

            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(_viewRoot.transform, false);
            _mesh = new Mesh { name = "NorseSense_SpawnGrid", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            gridObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = gridObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _raidRing = CreateLine(_viewRoot.transform, "RaidRing", SpawnRules.RaidColor, 0.4f);
            _raidRing.gameObject.SetActive(false);
            _ghostRing = CreateLine(transform, "GhostRing", SpawnRules.PreviewColor, 0.25f);
            _ghostRing.gameObject.SetActive(false);
        }

        // A disabled object stops its coroutines without running their finally blocks.
        void OnDisable() => _rebuilding = false;

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Update()
        {
            var player = Player.m_localPlayer;
            if (player == null || ZoneSystem.instance == null)
            {
                _viewRoot.SetActive(false);
                _ghostRing.gameObject.SetActive(false);
                ClearMarkers();
                return;
            }

            UpdateGhost(player);
            bool view = BaseView.Mode == ViewMode.Spawns;

            bool showGhostRing = _ghostActive && (view || Plugin.PreviewWhileBuilding.Value);
            _ghostRing.gameObject.SetActive(showGhostRing);
            if (showGhostRing && (_ghostCenter - _ghostRingDrawnAt).sqrMagnitude > 0.0025f)
            {
                DrawRing(_ghostRing, _ghostCenter, _ghostRadius);
                _ghostRingDrawnAt = _ghostCenter;
            }

            if (!view)
            {
                if (_viewRoot.activeSelf)
                {
                    _viewRoot.SetActive(false);
                    ClearMarkers();
                }
                return;
            }

            if (!_viewRoot.activeSelf)
            {
                _viewRoot.SetActive(true);
                _gridTimer = _markerTimer = _colorTimer = 0f;
            }

            Vector3 pos = player.transform.position;
            float dt = Time.deltaTime;

            _gridTimer -= dt;
            if (!_rebuilding && (_gridTimer <= 0f || Utils.DistanceXZ(pos, _gridCenter) > Plugin.OverlayCellSize.Value * 2f))
            {
                _gridTimer = GridRefreshInterval;
                StartCoroutine(RebuildGrid(pos));
            }

            _colorTimer -= dt;
            if (_colorTimer <= 0f)
            {
                _colorTimer = 0.1f;
                UpdateColors();
            }

            _markerTimer -= dt;
            if (_markerTimer <= 0f)
            {
                _markerTimer = MarkerRefreshInterval;
                RefreshBaseRings(pos);
                RefreshSpawners(pos);
                RefreshRaid(player);
            }
        }

        // ---------- Placement ghost ----------

        void UpdateGhost(Player player)
        {
            GameObject ghost = player.InPlaceMode() ? PlacementGhost(player) : null;
            if (ghost != _ghost)
            {
                _ghost = ghost;
                _ghostArea = null;
                _ghostRingDrawnAt = Vector3.positiveInfinity;
                if (ghost != null)
                {
                    foreach (var area in ghost.GetComponentsInChildren<EffectArea>(true))
                    {
                        if ((area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
                        _ghostArea = area;
                        break;
                    }
                }
            }

            _ghostActive = _ghostArea != null && _ghost.activeInHierarchy &&
                           TryGetSphere(_ghostArea, out _ghostCenter, out _ghostRadius);
        }

        static bool TryGetSphere(EffectArea area, out Vector3 center, out float radius)
        {
            var col = area.GetComponent<Collider>();
            if (col is SphereCollider sphere)
            {
                Vector3 s = sphere.transform.lossyScale;
                radius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                center = sphere.transform.TransformPoint(sphere.center);
                return true;
            }
            if (col != null && col.enabled)
            {
                center = col.bounds.center;
                radius = col.bounds.extents.x;
                return true;
            }
            center = Vector3.zero;
            radius = 0f;
            return false;
        }

        // ---------- Ground grid ----------

        // Built over several frames: a big grid is thousands of ground checks, which in one frame would hitch
        // every couple of seconds. The finished grid replaces the old one in one go.
        const int CellsPerFrame = 1500;
        bool _rebuilding;

        IEnumerator RebuildGrid(Vector3 playerPos)
        {
            _rebuilding = true;
            try
            {
                // The water check needs to know which raids you've unlocked before it starts.
                var raids = SpawnRules.UnlockedRaids(Player.m_localPlayer);
                var zs = ZoneSystem.instance;
                float cell = Plugin.OverlayCellSize.Value;
                float radius = Plugin.OverlayRadius.Value;
                int n = Mathf.Clamp(Mathf.CeilToInt(radius * 2f / cell), 4, 128);
                _gridCenter = playerPos;

                // Snap to the cell grid so squares don't swim as you walk.
                float originX = Mathf.Floor(playerPos.x / cell) * cell - n / 2 * cell;
                float originZ = Mathf.Floor(playerPos.z / cell) * cell - n / 2 * cell;
                float water = zs.m_waterLevel;

                var heights = new float[(n + 1) * (n + 1)];
                var cells = new SpawnCell[n * n];
                var points = new Vector3[n * n];
                int work = 0;

                for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    var p = new Vector3(originX + x * cell, playerPos.y, originZ + z * cell);
                    float h = zs.GetGroundHeight(p, out float ground) ? ground : playerPos.y;
                    heights[z * (n + 1) + x] = Mathf.Max(h, water) + GroundOffset;
                    if (++work % (CellsPerFrame * 3) == 0) yield return null;
                }

                for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    if (ZoneSystem.instance == null) yield break;
                    int c = z * n + x;
                    var p = new Vector3(originX + x * cell + cell * 0.5f, playerPos.y, originZ + z * cell + cell * 0.5f);
                    cells[c] = Utils.DistanceXZ(p, playerPos) > radius ? SpawnCell.Unknown : SpawnRules.Classify(ref p, raids);
                    points[c] = p;
                    if (++work % CellsPerFrame == 0) yield return null;
                }

                if (n != _n)
                {
                    _n = n;
                    _verts = new Vector3[n * n * 4];
                    _colors = new Color[n * n * 4];
                    var tris = new int[n * n * 6];
                    for (int c = 0; c < n * n; c++)
                    {
                        int v = c * 4, tr = c * 6;
                        tris[tr] = v; tris[tr + 1] = v + 1; tris[tr + 2] = v + 2;
                        tris[tr + 3] = v; tris[tr + 4] = v + 2; tris[tr + 5] = v + 3;
                    }
                    _mesh.Clear();
                    _mesh.vertices = _verts;
                    _mesh.colors = _colors;
                    _mesh.triangles = tris;
                }
                _heights = heights;
                _cells = cells;
                _cellPoints = points;

                for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int v = (z * n + x) * 4;
                    float x0 = originX + x * cell, z0 = originZ + z * cell;
                    _verts[v] = new Vector3(x0, heights[z * (n + 1) + x], z0);
                    _verts[v + 1] = new Vector3(x0, heights[(z + 1) * (n + 1) + x], z0 + cell);
                    _verts[v + 2] = new Vector3(x0 + cell, heights[(z + 1) * (n + 1) + x + 1], z0 + cell);
                    _verts[v + 3] = new Vector3(x0 + cell, heights[z * (n + 1) + x + 1], z0);
                }

                _mesh.vertices = _verts;
                _mesh.RecalculateBounds();
                UpdateColors();
            }
            finally
            {
                _rebuilding = false;
            }
        }

        void UpdateColors()
        {
            if (_cells == null) return;

            float radiusSqr = _ghostRadius * _ghostRadius;
            Color preview = SpawnRules.PreviewColor;
            preview.a *= 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            _protected = _exposed = _steep = _preview = _exposedLeft = _water = _raidOnly = 0;

            // Out in the open with no base, painting every patch of meadow red says nothing new.
            _noBase = !_ghostActive;
            for (int c = 0; c < _cells.Length && _noBase; c++)
                if (_cells[c] == SpawnCell.Protected) _noBase = false;

            for (int c = 0; c < _cells.Length; c++)
            {
                Color col;
                switch (_cells[c])
                {
                    case SpawnCell.Protected: col = SpawnRules.ProtectedColor; _protected++; break;
                    case SpawnCell.Steep: col = SpawnRules.SteepColor; _steep++; break;
                    case SpawnCell.Exposed: col = SpawnRules.ExposedColor; _exposed++; break;
                    case SpawnCell.RaidOnly: col = SpawnRules.RaidOnlyColor; _exposed++; _raidOnly++; break;
                    case SpawnCell.Water: col = SpawnRules.WaterColor; _water++; break;
                    default: col = Color.clear; break;
                }

                bool exposed = _cells[c] == SpawnCell.Exposed || _cells[c] == SpawnCell.RaidOnly;
                if (_ghostActive && (exposed || _cells[c] == SpawnCell.Steep) &&
                    (_cellPoints[c] - _ghostCenter).sqrMagnitude <= radiusSqr)
                {
                    col = preview;
                    _preview++;
                }
                else if (exposed)
                {
                    _exposedLeft++;
                }

                if (_noBase) col = Color.clear;
                int v = c * 4;
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = col;
            }
            _mesh.colors = _colors;
        }

        // ---------- Rings and markers ----------

        void RefreshBaseRings(Vector3 playerPos)
        {
            _sphereCenters.Clear();
            _sphereRadii.Clear();
            if (Plugin.ShowBaseRings.Value)
            {
                float maxDist = Plugin.MaxDisplayDistance.Value;
                foreach (var area in EffectArea.GetAllAreas())
                {
                    if (area == null || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
                    var col = area.GetComponent<Collider>();
                    if (col == null || !col.enabled || !area.gameObject.activeInHierarchy) continue; // skips build ghosts
                    if (Utils.DistanceXZ(area.transform.position, playerPos) > maxDist) continue;
                    if (!TryGetSphere(area, out Vector3 center, out float radius)) continue;
                    _sphereCenters.Add(center);
                    _sphereRadii.Add(radius);
                }
            }

            // Only redraw when the set of areas changed.
            int key = _sphereCenters.Count;
            for (int i = 0; i < _sphereCenters.Count; i++)
                key = key * 31 + _sphereCenters[i].GetHashCode() * 7 + _sphereRadii[i].GetHashCode();
            if (key == _outlineKey) return;
            _outlineKey = key;

            int used = 0;
            for (int i = 0; i < _sphereCenters.Count; i++)
            {
                Vector3 c = _sphereCenters[i];
                float r = _sphereRadii[i];
                int segments = Mathf.Clamp(Mathf.CeilToInt(r * 2f), 24, 200);
                // Start the walk at a hidden point if there is one, so a visible arc is never split at the seam.
                int startAt = 0;
                for (int s = 0; s < segments; s++)
                    if (!OnOutline(i, Point(c, r, s, segments))) { startAt = s; break; }

                _arc.Clear();
                for (int s = 0; s <= segments; s++)
                {
                    Vector3 p = Point(c, r, (startAt + s) % segments, segments);
                    if (OnOutline(i, p)) _arc.Add(Grounded(p));
                    else used = FlushArc(used);
                }
                used = FlushArc(used);
            }
            for (int i = used; i < _outline.Count; i++)
                if (_outline[i].gameObject.activeSelf) _outline[i].gameObject.SetActive(false);
        }

        static Vector3 Point(Vector3 c, float r, int s, int segments)
        {
            float a = s * Mathf.PI * 2f / segments;
            return new Vector3(c.x + Mathf.Cos(a) * r, c.y, c.z + Mathf.Sin(a) * r);
        }

        // Not inside any other protected area.
        bool OnOutline(int self, Vector3 p)
        {
            for (int j = 0; j < _sphereCenters.Count; j++)
                if (j != self && Utils.DistanceXZ(p, _sphereCenters[j]) < _sphereRadii[j] - 0.05f) return false;
            return true;
        }

        static Vector3 Grounded(Vector3 p)
        {
            var zs = ZoneSystem.instance;
            if (zs != null && zs.GetGroundHeight(p, out float h)) p.y = Mathf.Max(h, zs.m_waterLevel);
            p.y += 0.3f;
            return p;
        }

        int FlushArc(int used)
        {
            if (_arc.Count < 2)
            {
                _arc.Clear();
                return used;
            }
            if (used >= _outline.Count) _outline.Add(CreateLine(_viewRoot.transform, "BaseOutline", BaseRingColor, 0.12f));
            var lr = _outline[used];
            lr.loop = false;
            lr.positionCount = _arc.Count;
            lr.SetPositions(_arc.ToArray());
            lr.gameObject.SetActive(true);
            _arc.Clear();
            return used + 1;
        }

        void RefreshSpawners(Vector3 playerPos)
        {
            float maxDist = Plugin.MaxDisplayDistance.Value;
            bool show = Plugin.ShowSpawners.Value;

            if (show)
            {
                foreach (var nest in FindObjectsByType<SpawnArea>(FindObjectsSortMode.None))
                {
                    bool inRange = Utils.DistanceXZ(nest.transform.position, playerPos) <= maxDist;
                    _nestMarkers.TryGetValue(nest, out var marker);
                    if (marker == null)
                    {
                        if (!inRange) continue;
                        marker = new GameObject("Nest");
                        marker.transform.SetParent(_viewRoot.transform, false);
                        Vector3 c = nest.transform.position;
                        DrawRing(CreateLine(marker.transform, "Trigger", NestTriggerColor, 0.15f), c, nest.m_triggerDistance);
                        DrawRing(CreateLine(marker.transform, "Spawn", NestSpawnColor, 0.2f), c, nest.m_spawnRadius);
                        _nestMarkers[nest] = marker;
                    }
                    marker.SetActive(inRange);
                }

                foreach (var spawner in FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None))
                {
                    Vector3 c = spawner.transform.position;
                    bool inRange = Utils.DistanceXZ(c, playerPos) <= maxDist;
                    _spawnerMarkers.TryGetValue(spawner, out var ring);
                    if (ring == null)
                    {
                        if (!inRange) continue;
                        ring = CreateLine(_viewRoot.transform, "Spawner", SpawnerActiveColor, 0.2f);
                        DrawRing(ring, c, 1.5f, false);
                        _spawnerMarkers[spawner] = ring;
                    }
                    ring.gameObject.SetActive(inRange);
                    if (!inRange) continue;

                    // Same check as CreatureSpawner.UpdateSpawner.
                    bool blocked = !spawner.m_spawnInPlayerBase &&
                                   EffectArea.IsPointInsideArea(c, EffectArea.Type.PlayerBase);
                    ring.startColor = ring.endColor = blocked ? SpawnerBlockedColor : SpawnerActiveColor;
                }
            }

            _toRemove.Clear();
            foreach (var kv in _nestMarkers)
                if (kv.Key == null || !show) _toRemove.Add(kv.Key);
            foreach (var key in _toRemove)
            {
                var nest = (SpawnArea)key;
                if (_nestMarkers[nest] != null) Destroy(_nestMarkers[nest]);
                _nestMarkers.Remove(nest);
            }

            _toRemove.Clear();
            foreach (var kv in _spawnerMarkers)
                if (kv.Key == null || !show) _toRemove.Add(kv.Key);
            foreach (var key in _toRemove)
            {
                var spawner = (CreatureSpawner)key;
                if (_spawnerMarkers[spawner] != null) Destroy(_spawnerMarkers[spawner].gameObject);
                _spawnerMarkers.Remove(spawner);
            }
        }

        void RefreshRaid(Player player)
        {
            _baseValue = player.GetBaseValue();
            _unlockedRaids = SpawnRules.CountUnlockedRaids(player);

            var raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            _raidRing.gameObject.SetActive(raid != null);
            if (raid != null && raid != _raidRingEvent)
                DrawRing(_raidRing, raid.m_pos, raid.m_eventRange);
            _raidRingEvent = raid;
        }

        void ClearMarkers()
        {
            foreach (var line in _outline) if (line != null) Destroy(line.gameObject);
            _outline.Clear();
            _outlineKey = 0;
            foreach (var marker in _nestMarkers.Values) if (marker != null) Destroy(marker);
            foreach (var ring in _spawnerMarkers.Values) if (ring != null) Destroy(ring.gameObject);
            _nestMarkers.Clear();
            _spawnerMarkers.Clear();
            _raidRingEvent = null;
        }

        // ---------- Drawing helpers ----------

        internal static Material GetMaterial()
        {
            if (_material != null) return _material;
            Shader shader = null;
            foreach (var name in new[] { "Sprites/Default", "Hidden/Internal-Colored", "Unlit/Color" })
            {
                shader = Shader.Find(name);
                if (shader != null) break;
            }
            _material = new Material(shader) { renderQueue = 3100 };
            return _material;
        }

        internal static LineRenderer CreateLine(Transform parent, string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = GetMaterial();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        // Ring that follows the terrain, so it isn't buried on slopes.
        internal static void DrawRing(LineRenderer lr, Vector3 center, float radius, bool followGround = true)
        {
            var zs = ZoneSystem.instance;
            int segments = Mathf.Clamp(Mathf.CeilToInt(radius * 2f), 24, 200);
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(center.x + Mathf.Cos(a) * radius, center.y, center.z + Mathf.Sin(a) * radius);
                if (followGround && zs != null && zs.GetGroundHeight(p, out float h))
                    p.y = Mathf.Max(h, zs.m_waterLevel);
                p.y += 0.3f;
                lr.SetPosition(i, p);
            }
        }

        // ---------- Legend ----------

        void BuildLegend()
        {
            _legend.Clear();
            void Add(Color c, string text) => _legend.Add(c, text);

            // Only the colours that are on the ground right now.
            if (_noBase)
            {
                _legend.BarFraction = -1f;
                _legend.Add($"<color={Ui.Muted}>No base here. A workbench, fire or bed protects the ground around it.</color>");
            }
            else
            {
                if (_protected > 0) Add(SpawnRules.ProtectedColor, "Safe: nothing spawns");
                if (_exposedLeft > 0) Add(SpawnRules.ExposedColor, "Monsters can spawn");
                if (_raidOnly > 0) Add(SpawnRules.RaidOnlyColor, "Only raids can spawn");
                if (_steep > 0) Add(SpawnRules.SteepColor, "Too steep for most monsters");
                if (_water > 0) Add(SpawnRules.WaterColor, "Water, only serpents");
                if (_ghostActive) Add(SpawnRules.PreviewColor, "Safe once placed");
            }
            if (Plugin.ShowSpawners.Value && _nestMarkers.Count > 0) Add(NestSpawnColor, "Nest: workbenches don't stop it");

            // Coverage bar: share of spawnable ground around you that's protected.
            // Nothing spawnable at all (only open water, say) isn't "100% protected": it says so instead.
            int spawnable = _protected + _exposed + _steep;
            if (_noBase) { }
            else if (spawnable == 0)
            {
                _legend.BarFraction = 0f;
                _legend.BarText = $"<color={Ui.Muted}>No ground to protect in view</color>";
                _legend.BarColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
            else
            {
                int pct = Mathf.RoundToInt(100f * (_protected + _preview) / spawnable);
                float coverage = pct / 100f;
                _legend.BarFraction = coverage;
                _legend.BarText = $"<b>{pct}%</b> safe{(_preview > 0 ? $" <size=12><color={Ui.Muted}>once placed</color></size>" : "")}";
                Color barColor = coverage >= 0.999f ? SpawnRules.ProtectedColor : Color.Lerp(SpawnRules.ExposedColor, SpawnRules.ProtectedColor, coverage);
                barColor.a = 0.95f;
                _legend.BarColor = barColor;
            }

            // Raids: RandEventSystem.CheckBase needs base value >= 3 (base pieces within 20 m of you). Said only once
            // a raid could happen at all, in plain words.
            if (_unlockedRaids > 0 && !_noBase)
                _legend.Add(_baseValue >= SpawnRules.RaidBaseValue
                    ? $"<color={Ui.Warn}>Raids can come here</color>"
                    : $"Raids won't come here <color={Ui.Muted}>(needs {SpawnRules.RaidBaseValue}+ base pieces like fires and beds)</color>");

            if (Compat.SpawnRulesChangedBy != null)
                _legend.Add($"<color={Ui.Warn}>Spawn rules changed by {Compat.SpawnRulesChangedBy}: may be off</color>");
            if (Compat.RaidRulesChangedBy != null)
                _legend.Add($"<color={Ui.Warn}>Raid rules changed by {Compat.RaidRulesChangedBy}: may be off</color>");

            var raid = _raidRingEvent;
            if (raid != null)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(raid.m_duration - raid.m_time));
                Add(SpawnRules.RaidColor, $"Raid active: {raid.GetHudText()} ({left / 60}:{left % 60:00})");
            }
        }

        void OnGUI()
        {
            if (BaseView.Mode != ViewMode.Spawns || !BaseView.LegendVisible) return;
            if (Event.current.type == EventType.Layout) BuildLegend();
            BaseView.DrawLegend(_legend);
        }
    }
}
