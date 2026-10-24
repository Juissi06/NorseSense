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

        static readonly Color BaseRingColor = new Color(0.35f, 0.65f, 1f, 0.9f);
        static readonly Color PlayerRingColor = new Color(1f, 1f, 1f, 0.5f);
        static readonly Color NestTriggerColor = new Color(1f, 0.9f, 0.2f, 0.8f);
        static readonly Color NestSpawnColor = new Color(1f, 0.2f, 0.1f, 0.9f);
        static readonly Color SpawnerActiveColor = new Color(1f, 0.45f, 0.1f, 0.9f);
        static readonly Color SpawnerBlockedColor = new Color(0.35f, 0.65f, 1f, 0.9f);

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
        int _protected, _exposed, _steep, _preview, _exposedLeft;

        LineRenderer _playerRing;
        LineRenderer _ghostRing;
        LineRenderer _raidRing;
        RandomEvent _raidRingEvent;
        readonly Dictionary<EffectArea, LineRenderer> _baseRings = new Dictionary<EffectArea, LineRenderer>();
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

        GUIStyle _labelStyle;
        GUIStyle _titleStyle;
        readonly List<KeyValuePair<Color, string>> _legendLines = new List<KeyValuePair<Color, string>>();

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

            _playerRing = CreateLine(_viewRoot.transform, "PlayerRing", PlayerRingColor, 0.15f);
            _raidRing = CreateLine(_viewRoot.transform, "RaidRing", SpawnRules.RaidColor, 0.4f);
            _raidRing.gameObject.SetActive(false);
            _ghostRing = CreateLine(transform, "GhostRing", SpawnRules.PreviewColor, 0.25f);
            _ghostRing.gameObject.SetActive(false);
        }

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
            bool view = Plugin.ShowSpawnRadius.Value;

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
            if (_gridTimer <= 0f || Utils.DistanceXZ(pos, _gridCenter) > Plugin.OverlayCellSize.Value * 2f)
            {
                _gridTimer = GridRefreshInterval;
                RebuildGrid(pos);
            }

            _colorTimer -= dt;
            if (_colorTimer <= 0f)
            {
                _colorTimer = 0.1f;
                UpdateColors();
                DrawRing(_playerRing, pos, SpawnRules.PlayerNoSpawnRadius);
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

        void RebuildGrid(Vector3 playerPos)
        {
            var zs = ZoneSystem.instance;
            float cell = Plugin.OverlayCellSize.Value;
            float radius = Plugin.OverlayRadius.Value;
            int n = Mathf.Clamp(Mathf.CeilToInt(radius * 2f / cell), 4, 128);
            _gridCenter = playerPos;

            if (n != _n)
            {
                _n = n;
                _verts = new Vector3[n * n * 4];
                _colors = new Color[n * n * 4];
                _cells = new SpawnCell[n * n];
                _cellPoints = new Vector3[n * n];
                _heights = new float[(n + 1) * (n + 1)];
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

            // Snap to the cell grid so squares don't swim as you walk.
            float originX = Mathf.Floor(playerPos.x / cell) * cell - n / 2 * cell;
            float originZ = Mathf.Floor(playerPos.z / cell) * cell - n / 2 * cell;
            float water = zs.m_waterLevel;

            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                var p = new Vector3(originX + x * cell, playerPos.y, originZ + z * cell);
                float h = zs.GetGroundHeight(p, out float ground) ? ground : playerPos.y;
                _heights[z * (n + 1) + x] = Mathf.Max(h, water) + GroundOffset;
            }

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int c = z * n + x;
                float x0 = originX + x * cell, z0 = originZ + z * cell;
                var p = new Vector3(x0 + cell * 0.5f, playerPos.y, z0 + cell * 0.5f);

                _cells[c] = Utils.DistanceXZ(p, playerPos) > radius ? SpawnCell.Unknown : SpawnRules.Classify(ref p);
                _cellPoints[c] = p;

                int v = c * 4;
                _verts[v] = new Vector3(x0, _heights[z * (n + 1) + x], z0);
                _verts[v + 1] = new Vector3(x0, _heights[(z + 1) * (n + 1) + x], z0 + cell);
                _verts[v + 2] = new Vector3(x0 + cell, _heights[(z + 1) * (n + 1) + x + 1], z0 + cell);
                _verts[v + 3] = new Vector3(x0 + cell, _heights[z * (n + 1) + x + 1], z0);
            }

            _mesh.vertices = _verts;
            _mesh.RecalculateBounds();
            UpdateColors();
        }

        void UpdateColors()
        {
            if (_cells == null) return;

            float radiusSqr = _ghostRadius * _ghostRadius;
            Color preview = SpawnRules.PreviewColor;
            preview.a *= 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            _protected = _exposed = _steep = _preview = _exposedLeft = 0;

            for (int c = 0; c < _cells.Length; c++)
            {
                Color col;
                switch (_cells[c])
                {
                    case SpawnCell.Protected: col = SpawnRules.ProtectedColor; _protected++; break;
                    case SpawnCell.Steep: col = SpawnRules.SteepColor; _steep++; break;
                    case SpawnCell.Exposed: col = SpawnRules.ExposedColor; _exposed++; break;
                    default: col = Color.clear; break;
                }

                if (_ghostActive && (_cells[c] == SpawnCell.Exposed || _cells[c] == SpawnCell.Steep) &&
                    (_cellPoints[c] - _ghostCenter).sqrMagnitude <= radiusSqr)
                {
                    col = preview;
                    _preview++;
                }
                else if (_cells[c] == SpawnCell.Exposed)
                {
                    _exposedLeft++;
                }

                int v = c * 4;
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = col;
            }
            _mesh.colors = _colors;
        }

        // ---------- Rings and markers ----------

        void RefreshBaseRings(Vector3 playerPos)
        {
            float maxDist = Plugin.MaxDisplayDistance.Value;
            if (Plugin.ShowBaseRings.Value)
            {
                foreach (var area in EffectArea.GetAllAreas())
                {
                    if (area == null || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
                    var col = area.GetComponent<Collider>();
                    if (col == null || !col.enabled || !area.gameObject.activeInHierarchy) continue; // skips build ghosts

                    bool inRange = Utils.DistanceXZ(area.transform.position, playerPos) <= maxDist;
                    _baseRings.TryGetValue(area, out var ring);
                    if (ring == null)
                    {
                        if (!inRange || !TryGetSphere(area, out Vector3 center, out float radius)) continue;
                        ring = CreateLine(_viewRoot.transform, "BaseRing", BaseRingColor, 0.12f);
                        DrawRing(ring, center, radius);
                        _baseRings[area] = ring;
                    }
                    ring.gameObject.SetActive(inRange);
                }
            }

            _toRemove.Clear();
            foreach (var kv in _baseRings)
                if (kv.Key == null || !Plugin.ShowBaseRings.Value) _toRemove.Add(kv.Key);
            foreach (var key in _toRemove)
            {
                var area = (EffectArea)key;
                if (_baseRings[area] != null) Destroy(_baseRings[area].gameObject);
                _baseRings.Remove(area);
            }
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
            foreach (var ring in _baseRings.Values) if (ring != null) Destroy(ring.gameObject);
            foreach (var marker in _nestMarkers.Values) if (marker != null) Destroy(marker);
            foreach (var ring in _spawnerMarkers.Values) if (ring != null) Destroy(ring.gameObject);
            _baseRings.Clear();
            _nestMarkers.Clear();
            _spawnerMarkers.Clear();
            _raidRingEvent = null;
        }

        // ---------- Drawing helpers ----------

        static Material GetMaterial()
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

        static LineRenderer CreateLine(Transform parent, string name, Color color, float width)
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
        static void DrawRing(LineRenderer lr, Vector3 center, float radius, bool followGround = true)
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
            _legendLines.Clear();
            void Add(Color c, string text) => _legendLines.Add(new KeyValuePair<Color, string>(c, text));

            Add(SpawnRules.ProtectedColor, "Protected: mobs can't spawn");
            Add(SpawnRules.ExposedColor, "Exposed: mobs can spawn");
            Add(SpawnRules.SteepColor, "Too steep for most mobs");
            if (_ghostActive) Add(SpawnRules.PreviewColor, "Protected once placed");
            Add(PlayerRingColor, "No spawns within 40 m of a player");
            if (Plugin.ShowSpawners.Value) Add(NestSpawnColor, "Nest: ignores workbenches, destroy it");

            int spawnable = _protected + _exposed + _steep;
            int pct = spawnable > 0 ? Mathf.RoundToInt(100f * (_protected + _preview) / spawnable) : 100;
            float cell = Plugin.OverlayCellSize.Value;
            Add(Color.clear, $"Coverage: <b>{pct}%</b>{(_preview > 0 ? " (with preview)" : "")}");
            Add(Color.clear, _exposedLeft > 0
                ? $"Exposed ground: <color=#ff7050>~{_exposedLeft * cell * cell:0} m²</color>"
                : "<color=#80c0ff>No exposed ground in view</color>");

            // Raids: RandEventSystem.CheckBase needs base value >= 3 (base pieces within 20 m of you).
            if (_unlockedRaids == 0)
                Add(Color.clear, "Raids: <color=#80c0ff>none unlocked yet</color>");
            else if (_baseValue >= SpawnRules.RaidBaseValue)
                Add(Color.clear, $"Raids: <color=#ffa030>can target you here</color> (base {_baseValue}/{SpawnRules.RaidBaseValue})");
            else
                Add(Color.clear, $"Raids: <color=#80c0ff>not here</color> (base {_baseValue}/{SpawnRules.RaidBaseValue})");

            if (Compat.SpawnRulesChangedBy != null)
                Add(Color.clear, $"<color=#ffa030>Spawn rules changed by {Compat.SpawnRulesChangedBy}: may be off</color>");
            if (Compat.RaidRulesChangedBy != null)
                Add(Color.clear, $"<color=#ffa030>Raid rules changed by {Compat.RaidRulesChangedBy}: may be off</color>");

            var raid = _raidRingEvent;
            if (raid != null)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(raid.m_duration - raid.m_time));
                Add(SpawnRules.RaidColor, $"Raid active: {raid.GetHudText()} ({left / 60}:{left % 60:00})");
            }
        }

        void OnGUI()
        {
            if (!Plugin.ShowSpawnRadius.Value || !Plugin.ShowLegend.Value || Player.m_localPlayer == null) return;
            if (Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false, richText = true };
                _labelStyle.normal.textColor = Color.white;
                _titleStyle = new GUIStyle(_labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
            }
            if (Event.current.type == EventType.Layout) BuildLegend();

            float scale = Screen.height / 1080f * Plugin.LegendScale.Value;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float w = 330f, lineH = 22f, pad = 10f;
            float h = pad * 2 + 26f + _legendLines.Count * lineH;
            float x = Plugin.LegendX.Value * (Screen.width / scale - w - 20f) + 10f;
            float y = Plugin.LegendY.Value * (Screen.height / scale - h - 20f) + 10f;

            // Drawn twice: the default skin's box is too transparent to read over bright terrain.
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            float cx = x + pad, cy = y + pad;

            GUI.Label(new Rect(cx, cy, w - pad * 2, 24f), $"Spawn view  <size=12>({Plugin.ToggleKey.Value})</size>", _titleStyle);
            cy += 26f;

            foreach (var line in _legendLines)
            {
                float textX = cx;
                if (line.Key.a > 0f)
                {
                    Color old = GUI.color;
                    Color c = line.Key;
                    c.a = Mathf.Max(c.a, 0.8f);
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(cx, cy + 4f, 14f, 14f), Texture2D.whiteTexture);
                    GUI.color = old;
                    textX += 22f;
                }
                GUI.Label(new Rect(textX, cy, w - pad - (textX - x), lineH), line.Value, _labelStyle);
                cy += lineH;
            }

            GUI.matrix = oldMatrix;
        }
    }
}
