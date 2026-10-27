using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NorseSense
{
    // Two layers on the large map, each with its own toggle next to the game's "public position" one:
    // - Wards and stations: ward areas coloured by whether you may build there, and crafting stations' build range.
    // - Portals: a line between each pair of portals, and unpaired ones marked, with their tags when zoomed in.
    // Read from the game's object list: the host knows the whole world, a client what it has loaded this session,
    // so on a dedicated server the map fills in as you travel.
    internal class MapLayers : MonoBehaviour
    {
        const float RescanInterval = 3f;
        const int SectorsPerFrame = 20000;
        const float LineWidth = 2f;
        const float DotSize = 10f;

        static readonly Color StationColor = new Color(Ui.GoldColor.r, Ui.GoldColor.g, Ui.GoldColor.b, 0.85f);
        static readonly Color WardYoursColor = Ui.Alpha(Ui.GoodColor, 0.9f);
        static readonly Color WardOtherColor = Ui.Alpha(Ui.BadColor, 0.9f);
        static readonly Color WardOffColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        static readonly Color PortalColor = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color UnpairedColor = Ui.Alpha(Ui.WarnColor, 0.95f);
        const float FillAlpha = 0.12f;

        static readonly AccessTools.FieldRef<ZDOMan, List<ZDO>[]> Sectors =
            AccessTools.FieldRefAccess<ZDOMan, List<ZDO>[]>("m_objectsBySector");
        static readonly AccessTools.FieldRef<Minimap, GameObject> PinNamePrefab =
            AccessTools.FieldRefAccess<Minimap, GameObject>("m_pinNamePrefab");

        enum AreaKind { Station, WardYours, WardOther, WardOff }

        struct Area
        {
            public Vector3 Pos;
            public float Radius;
            public AreaKind Kind;
        }

        struct Portal
        {
            public Vector3 Pos;
            public string Tag;
            public bool Paired;
            public bool TargetKnown; // the other end is in this client's object list
            public Vector3 Target;
            public bool DrawLine;  // one of the two ends draws the shared line
        }

        // Prefab hash -> ward radius or station base build range.
        readonly Dictionary<int, float> _wardPrefabs = new Dictionary<int, float>();
        readonly Dictionary<int, float> _stationPrefabs = new Dictionary<int, float>();
        bool _prefabsRead;

        List<Area> _areas = new List<Area>();
        List<Portal> _portals = new List<Portal>();
        bool _scanning;
        float _timer;

        Minimap _map;
        RectTransform _root;
        readonly List<RawImage> _rings = new List<RawImage>(), _fills = new List<RawImage>(), _lines = new List<RawImage>(), _dots = new List<RawImage>();
        readonly List<TMP_Text> _labels = new List<TMP_Text>();
        Toggle _wardToggle, _portalToggle;

        // Rings at a few sizes, each with a line about 2 px wide, so a ring keeps a thin line at every zoom.
        static readonly int[] RingSizes = { 32, 64, 128, 256, 512 };
        Texture2D[] _ringTextures;
        Texture2D _disc;

        void Update()
        {
            _map = Minimap.instance;
            bool open = _map != null && _map.m_mode == Minimap.MapMode.Large && Player.m_localPlayer != null && ZDOMan.instance != null;
            if (open) EnsureUi();
            bool any = open && (Plugin.MapWards.Value || Plugin.MapPortals.Value);
            if (_root != null) _root.gameObject.SetActive(any);
            if (!any) return;

            _timer -= Time.deltaTime;
            if (!_scanning && _timer <= 0f)
            {
                _timer = RescanInterval;
                StartCoroutine(Scan());
            }
        }

        // The map pans and zooms in its own Update, so place everything afterwards.
        void LateUpdate()
        {
            if (_root == null || !_root.gameObject.activeSelf || _map == null) return;
            SyncToggle(_wardToggle, Plugin.MapWards.Value);
            SyncToggle(_portalToggle, Plugin.MapPortals.Value);
            Draw();
        }

        void OnDisable()
        {
            // Mod switched off: everything lives on the game's map, so hide it explicitly.
            _scanning = false;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_wardToggle != null) _wardToggle.gameObject.SetActive(false);
            if (_portalToggle != null) _portalToggle.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_wardToggle != null) _wardToggle.gameObject.SetActive(true);
            if (_portalToggle != null) _portalToggle.gameObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
            if (_wardToggle != null) Destroy(_wardToggle.gameObject);
            if (_portalToggle != null) Destroy(_portalToggle.gameObject);
            if (_ringTextures != null) foreach (var t in _ringTextures) Destroy(t);
            if (_disc != null) Destroy(_disc);
        }

        // ---------- Data ----------

        void ReadPrefabs()
        {
            if (_prefabsRead || ZNetScene.instance == null) return;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                int hash = prefab.name.GetStableHashCode();
                var ward = prefab.GetComponent<PrivateArea>();
                // Faction wards (dvergr and the like) aren't player wards.
                if (ward != null && ward.m_ownerFaction == Character.Faction.Players) _wardPrefabs[hash] = ward.m_radius;
                var station = prefab.GetComponent<CraftingStation>();
                if (station != null && station.m_rangeBuild > 0f) _stationPrefabs[hash] = station.m_rangeBuild;
            }
            _prefabsRead = true;
        }

        IEnumerator Scan()
        {
            ReadPrefabs();
            if (!_prefabsRead) yield break;
            _scanning = true;
            var areas = new List<Area>();
            var portals = new List<Portal>();

            if (Plugin.MapWards.Value)
            {
                long me = Player.m_localPlayer.GetPlayerID();
                int i = 0;
                while (true)
                {
                    if (ZDOMan.instance == null || Player.m_localPlayer == null)
                    {
                        _scanning = false;
                        yield break;
                    }
                    var sectors = Sectors(ZDOMan.instance);
                    if (sectors == null || i >= sectors.Length) break;
                    int end = Mathf.Min(i + SectorsPerFrame, sectors.Length);
                    for (; i < end; i++)
                    {
                        var list = sectors[i];
                        if (list == null) continue;
                        foreach (ZDO zdo in list) AddArea(zdo, me, areas);
                    }
                    yield return null;
                }
            }

            if (Plugin.MapPortals.Value && ZDOMan.instance != null)
            {
                var seen = new HashSet<ZDOID>();
                foreach (ZDO zdo in ZDOMan.instance.GetPortalList())
                {
                    if (!zdo.IsValid()) continue;
                    var p = new Portal { Pos = zdo.GetPosition(), Tag = zdo.GetString(ZDOVars.s_tag) };
                    ZDOID target = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
                    if (!target.IsNone())
                    {
                        p.Paired = true;
                        ZDO other = ZDOMan.instance.GetZDO(target);
                        if (other != null)
                        {
                            p.TargetKnown = true;
                            p.Target = other.GetPosition();
                            p.DrawLine = !seen.Contains(target);
                        }
                    }
                    seen.Add(zdo.m_uid);
                    portals.Add(p);
                }
            }

            _areas = areas;
            _portals = portals;
            _scanning = false;
        }

        void AddArea(ZDO zdo, long me, List<Area> areas)
        {
            int prefab = zdo.GetPrefab();
            if (_wardPrefabs.TryGetValue(prefab, out float radius))
            {
                AreaKind kind;
                if (!zdo.GetBool(ZDOVars.s_enabled)) kind = AreaKind.WardOff;
                else kind = HasAccess(zdo, me) ? AreaKind.WardYours : AreaKind.WardOther;
                areas.Add(new Area { Pos = zdo.GetPosition(), Radius = radius, Kind = kind });
            }
            else if (_stationPrefabs.TryGetValue(prefab, out float range))
            {
                // Extensions widen the range; only a loaded station knows its extensions, the rest show the base range.
                var view = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
                var station = view != null ? view.GetComponent<CraftingStation>() : null;
                if (station != null) range = station.GetStationBuildRange();
                areas.Add(new Area { Pos = zdo.GetPosition(), Radius = range, Kind = AreaKind.Station });
            }
        }

        // Same rule as PrivateArea.HaveLocalAccess: the builder or someone on the ward's list.
        static bool HasAccess(ZDO zdo, long me)
        {
            if (zdo.GetLong(ZDOVars.s_creator) == me) return true;
            int count = zdo.GetInt(ZDOVars.s_permitted);
            for (int i = 0; i < count; i++)
                if (zdo.GetLong("pu_id" + i) == me) return true;
            return false;
        }

        // ---------- UI ----------

        void EnsureUi()
        {
            if (_root == null)
            {
                var mapImage = _map.m_mapImageLarge;
                // A masked container the size of the map, under the pins, so nothing draws past the map edge.
                var mask = new GameObject("NorseSense_MapLayers", typeof(RectTransform), typeof(RectMask2D));
                _root = (RectTransform)mask.transform;
                _root.SetParent(mapImage.transform, false);
                _root.anchorMin = Vector2.zero;
                _root.anchorMax = Vector2.one;
                _root.offsetMin = _root.offsetMax = Vector2.zero;
                MakeTextures();
            }
            if (_wardToggle == null) _wardToggle = MakeToggle("Wards and stations", 1, v => Plugin.MapWards.Value = v);
            if (_portalToggle == null) _portalToggle = MakeToggle("Portals", 2, v => Plugin.MapPortals.Value = v);
        }

        // A copy of the game's "public position" toggle, stacked above it. The copy gets a fresh event, so it never
        // calls the original's handler.
        Toggle MakeToggle(string label, int slot, UnityEngine.Events.UnityAction<bool> onChange)
        {
            var original = _map.m_publicPosition;
            if (original == null) return null;
            var go = Instantiate(original.gameObject, original.transform.parent);
            go.name = "NorseSense_MapToggle_" + slot;
            var toggle = go.GetComponent<Toggle>();
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(slot == 1 ? Plugin.MapWards.Value : Plugin.MapPortals.Value);
            toggle.onValueChanged.AddListener(onChange);

            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                // The game localizes the original's label; keep ours as set.
                foreach (var loc in text.GetComponents<MonoBehaviour>())
                    if (loc != null && loc.GetType().Name == "Localize") Destroy(loc);
                text.text = label;
            }

            var rt = (RectTransform)go.transform;
            var ort = (RectTransform)original.transform;
            rt.anchoredPosition = ort.anchoredPosition + new Vector2(0f, ort.rect.height * 1.15f * slot);
            return toggle;
        }

        static void SyncToggle(Toggle toggle, bool value)
        {
            if (toggle != null && toggle.isOn != value) toggle.SetIsOnWithoutNotify(value);
        }

        void MakeTextures()
        {
            _ringTextures = new Texture2D[RingSizes.Length];
            for (int i = 0; i < RingSizes.Length; i++) _ringTextures[i] = Circle(RingSizes[i], true);
            _disc = Circle(64, false);
        }

        // White ring (2 px line, soft edges) or filled disc; tinted by the RawImage colour.
        static Texture2D Circle(int size, bool ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "NorseSense_MapCircle"
            };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f, outer = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - (outer - 1f)) + 0.5f)
                    : Mathf.Clamp01(outer - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        // ---------- Drawing ----------

        void Draw()
        {
            RawImage mapImage = _map.m_mapImageLarge;
            Rect uv = mapImage.uvRect;
            Rect rect = _root.rect;
            float worldSize = _map.m_textureSize * _map.m_pixelSize;
            float pxPerMeter = rect.width / (uv.width * worldSize);
            bool names = _map.LargeZoom < _map.m_showNamesZoom;

            int ring = 0, fill = 0, line = 0, dot = 0, label = 0;

            if (Plugin.MapWards.Value)
            {
                foreach (var area in _areas)
                {
                    Vector2 p = ToLocal(area.Pos, uv, rect, worldSize);
                    float d = area.Radius * 2f * pxPerMeter;
                    if (d < 3f || !Visible(p, d * 0.5f, rect)) continue;
                    Color color = ColorOf(area.Kind);
                    if (area.Kind == AreaKind.WardYours || area.Kind == AreaKind.WardOther)
                        Place(Get(_fills, fill++, _disc), p, d, new Color(color.r, color.g, color.b, FillAlpha));
                    var r = Get(_rings, ring++, RingFor(d));
                    r.texture = RingFor(d);
                    Place(r, p, d, color);
                }
            }

            if (Plugin.MapPortals.Value)
            {
                foreach (var portal in _portals)
                {
                    Vector2 a = ToLocal(portal.Pos, uv, rect, worldSize);
                    if (portal.TargetKnown && portal.DrawLine)
                    {
                        Vector2 b = ToLocal(portal.Target, uv, rect, worldSize);
                        if (Visible(a, 0f, rect) || Visible(b, 0f, rect) || Crosses(a, b, rect))
                            PlaceLine(Get(_lines, line++, Texture2D.whiteTexture), a, b, PortalColor);
                    }
                    if (!Visible(a, DotSize, rect)) continue;
                    Place(Get(_dots, dot++, _disc), a, DotSize, portal.Paired ? PortalColor : UnpairedColor);
                    if (names && !string.IsNullOrEmpty(portal.Tag))
                    {
                        var t = GetLabel(label++);
                        if (t == null) continue;
                        t.text = portal.Tag;
                        t.color = portal.Paired ? Color.white : UnpairedColor;
                        var rt = t.rectTransform;
                        rt.anchorMin = rt.anchorMax = Vector2.zero;
                        rt.anchoredPosition = a + new Vector2(0f, -DotSize);
                    }
                }
            }

            Hide(_rings, ring);
            Hide(_fills, fill);
            Hide(_lines, line);
            Hide(_dots, dot);
            for (int i = label; i < _labels.Count; i++)
                if (_labels[i] != null && _labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);
        }

        static Color ColorOf(AreaKind kind)
        {
            switch (kind)
            {
                case AreaKind.WardYours: return WardYoursColor;
                case AreaKind.WardOther: return WardOtherColor;
                case AreaKind.WardOff: return WardOffColor;
                default: return StationColor;
            }
        }

        Texture2D RingFor(float diameter)
        {
            for (int i = 0; i < RingSizes.Length; i++)
                if (RingSizes[i] >= diameter) return _ringTextures[i];
            return _ringTextures[RingSizes.Length - 1];
        }

        // Same mapping as Minimap.WorldToMapPoint and MapPointToLocalGuiPos, from the container's bottom-left corner.
        static Vector2 ToLocal(Vector3 world, Rect uv, Rect rect, float worldSize)
        {
            float u = world.x / worldSize + 0.5f, v = world.z / worldSize + 0.5f;
            return new Vector2((u - uv.x) / uv.width * rect.width, (v - uv.y) / uv.height * rect.height);
        }

        static bool Visible(Vector2 p, float margin, Rect rect) =>
            p.x >= -margin && p.y >= -margin && p.x <= rect.width + margin && p.y <= rect.height + margin;

        // A line whose ends are both off the map can still cross it.
        static bool Crosses(Vector2 a, Vector2 b, Rect rect) =>
            !(Mathf.Max(a.x, b.x) < 0f || Mathf.Min(a.x, b.x) > rect.width || Mathf.Max(a.y, b.y) < 0f || Mathf.Min(a.y, b.y) > rect.height);

        RawImage Get(List<RawImage> pool, int index, Texture texture)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("NorseSense_MapMark", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(_root, false);
                var image = go.GetComponent<RawImage>();
                image.raycastTarget = false; // never eat map clicks or pin placement
                image.texture = texture;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                pool.Add(image);
            }
            var result = pool[index];
            if (!result.gameObject.activeSelf) result.gameObject.SetActive(true);
            return result;
        }

        TMP_Text GetLabel(int index)
        {
            while (_labels.Count <= index)
            {
                var prefab = PinNamePrefab(_map);
                if (prefab == null) return null;
                // The game's own pin name, so tags look like marker names.
                var go = Instantiate(prefab, _root);
                go.name = "NorseSense_PortalTag";
                var text = go.GetComponentInChildren<TMP_Text>(true);
                if (text == null)
                {
                    Destroy(go);
                    return null;
                }
                text.raycastTarget = false;
                _labels.Add(text);
            }
            var label = _labels[index];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            return label;
        }

        static void Place(RawImage image, Vector2 center, float size, Color color)
        {
            var rt = image.rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(size, size);
            image.color = color;
        }

        static void PlaceLine(RawImage image, Vector2 a, Vector2 b, Color color)
        {
            var rt = image.rectTransform;
            Vector2 d = b - a;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, LineWidth);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            image.color = color;
        }

        static void Hide(List<RawImage> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }
    }
}
