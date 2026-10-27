using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NorseSense
{
    // Paints the spawn view onto the minimap and the large map. Scanning is spread over several
    // frames because a 96 m radius is ~9000 cells of physics checks.
    internal class MapOverlay : MonoBehaviour
    {
        const float RefreshInterval = 5f;
        const int CellsPerFrame = 1500;

        static readonly Color32 ProtectedPixel = new Color32(50, 130, 255, 140);
        static readonly Color32 ExposedPixel = new Color32(255, 60, 25, 170);
        static readonly Color32 SteepPixel = new Color32(255, 215, 50, 70);
        static readonly Color32 RaidOnlyPixel = new Color32(255, 150, 25, 150); // as on the ground grid: only raids
        static readonly Color32 WaterPixel = new Color32(205, 205, 205, 45); // as on the ground grid: grey, only fish and serpents
        static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

        Texture2D _texture;
        bool _visible;
        RawImage _small, _large;
        Minimap _map;

        // Area of the world the current texture covers.
        float _originX, _originZ, _size;
        bool _scanning;
        float _timer;
        Vector3 _scanCenter;

        void Update()
        {
            var player = Player.m_localPlayer;
            _map = Minimap.instance;
            bool show = BaseView.Mode == ViewMode.Spawns && Plugin.ShowOnMap.Value &&
                        player != null && _map != null && ZoneSystem.instance != null;

            if (show != _visible)
            {
                _visible = show;
                if (_small != null) _small.enabled = show;
                if (_large != null) _large.enabled = show;
            }
            if (!show) return;

            EnsureImages();
            _timer -= Time.deltaTime;
            if (!_scanning && (_timer <= 0f || Utils.DistanceXZ(player.transform.position, _scanCenter) > 16f))
            {
                _timer = RefreshInterval;
                StartCoroutine(Scan(player.transform.position));
            }
        }

        // Minimap moves its uvRect in its own Update, so align afterwards.
        void LateUpdate()
        {
            if (!_visible || _map == null || _texture == null || _size <= 0f) return;
            Align(_small, _map.m_mapImageSmall);
            Align(_large, _map.m_mapImageLarge);
        }

        void OnDisable()
        {
            // Mod switched off: the images live on the game's minimap, so hide them explicitly.
            _scanning = false;
            _visible = false;
            if (_small != null) _small.enabled = false;
            if (_large != null) _large.enabled = false;
        }

        void EnsureImages()
        {
            if (_small == null) _small = CreateImage(_map.m_mapImageSmall);
            if (_large == null) _large = CreateImage(_map.m_mapImageLarge);
        }

        RawImage CreateImage(RawImage mapImage)
        {
            // A masked container the size of the map image, so the overlay never draws past the map edge.
            var mask = new GameObject("NorseSense_SpawnMask", typeof(RectTransform), typeof(RectMask2D));
            var maskRect = (RectTransform)mask.transform;
            maskRect.SetParent(mapImage.transform, false);
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;

            var go = new GameObject("NorseSense_SpawnOverlay", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(maskRect, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false; // never eat map clicks or pin placement
            image.texture = _texture;
            image.enabled = _texture != null;
            return image;
        }

        void Align(RawImage image, RawImage mapImage)
        {
            if (image == null || mapImage == null) return;
            image.enabled = _texture != null;
            if (!image.enabled) return;

            // Same mapping as Minimap.WorldToMapPoint: u = x / worldSize + 0.5
            float worldSize = _map.m_textureSize * _map.m_pixelSize;
            Rect uv = mapImage.uvRect;
            float u0 = _originX / worldSize + 0.5f, v0 = _originZ / worldSize + 0.5f;
            float s = _size / worldSize;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2((u0 - uv.x) / uv.width, (v0 - uv.y) / uv.height);
            rt.anchorMax = new Vector2((u0 + s - uv.x) / uv.width, (v0 + s - uv.y) / uv.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        IEnumerator Scan(Vector3 center)
        {
            // The water check needs to know which raids you've unlocked.
            if (Player.m_localPlayer != null) SpawnRules.CountUnlockedRaids(Player.m_localPlayer);
            _scanning = true;
            _scanCenter = center;

            float cell = Mathf.Max(2f, Plugin.OverlayCellSize.Value);
            float radius = Plugin.MapOverlayRadius.Value;
            int n = Mathf.Clamp(Mathf.CeilToInt(radius * 2f / cell), 8, 256);
            float originX = Mathf.Floor(center.x / cell) * cell - n / 2 * cell;
            float originZ = Mathf.Floor(center.z / cell) * cell - n / 2 * cell;
            var pixels = new Color32[n * n];

            int done = 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                if (ZoneSystem.instance == null || Player.m_localPlayer == null)
                {
                    _scanning = false;
                    yield break;
                }

                var p = new Vector3(originX + (x + 0.5f) * cell, center.y, originZ + (z + 0.5f) * cell);
                Color32 color = ClearPixel;
                if (Utils.DistanceXZ(p, center) <= radius)
                {
                    switch (SpawnRules.Classify(ref p))
                    {
                        case SpawnCell.Protected: color = ProtectedPixel; break;
                        case SpawnCell.Exposed: color = ExposedPixel; break;
                        case SpawnCell.Steep: color = SteepPixel; break;
                        case SpawnCell.Water: color = WaterPixel; break;
                        case SpawnCell.RaidOnly: color = RaidOnlyPixel; break;
                    }
                }
                pixels[z * n + x] = color;

                if (++done % CellsPerFrame == 0) yield return null;
            }

            if (_texture == null || _texture.width != n)
            {
                if (_texture != null) Destroy(_texture);
                _texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "NorseSense_SpawnMap"
                };
                if (_small != null) _small.texture = _texture;
                if (_large != null) _large.texture = _texture;
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false);
            _originX = originX;
            _originZ = originZ;
            _size = n * cell;
            _scanning = false;
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
            if (_small != null) Destroy(_small.transform.parent.gameObject);
            if (_large != null) Destroy(_large.transform.parent.gameObject);
        }
    }
}
