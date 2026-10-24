using System.Collections.Generic;
using UnityEngine;

namespace NorseSense
{
    // Valheim's own UI sprites (panel borders, wood, buttons...), copied out of the game's sprite atlas into
    // standalone textures so IMGUI styles can use them. Every lookup can fail (renamed in an update, atlas
    // packed differently); callers then keep Norse Sense's generated look.
    internal static class GameArt
    {
        static Dictionary<string, Sprite> _sprites;

        internal static Sprite Find(string name)
        {
            if (_sprites == null)
            {
                _sprites = new Dictionary<string, Sprite>();
                foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (sprite != null && !_sprites.ContainsKey(sprite.name)) _sprites[sprite.name] = sprite;
            }
            return _sprites.TryGetValue(name, out var found) && found != null ? found : null;
        }

        // The sprite's area of its texture in UV space. Works for atlas sprites, tight or not, unless rotated.
        internal static bool UvRect(Sprite sprite, out Rect uv)
        {
            uv = default;
            if (sprite == null || sprite.texture == null) return false;
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) return false;
            var uvs = sprite.uv;
            if (uvs == null || uvs.Length == 0) return false;
            Vector2 min = uvs[0], max = uvs[0];
            foreach (var p in uvs)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            uv = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return uv.width > 0f && uv.height > 0f;
        }

        // True when the sprite's mesh covers its whole rect, so the UV bounds map 1:1 onto the sprite
        // (tight meshes can trim transparent edges, which would shift 9-slice borders).
        static bool CoversRect(Sprite sprite)
        {
            var verts = sprite.vertices;
            if (verts == null || verts.Length == 0) return false;
            Vector2 min = verts[0], max = verts[0];
            foreach (var v in verts)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            Vector2 size = (max - min) * sprite.pixelsPerUnit;
            return Mathf.Abs(size.x - sprite.rect.width) <= 1.5f && Mathf.Abs(size.y - sprite.rect.height) <= 1.5f;
        }

        // A readable standalone copy of a sprite, plus its 9-slice border as an IMGUI RectOffset.
        internal static Texture2D Copy(string name, out RectOffset border)
        {
            border = null;
            var sprite = Find(name);
            if (sprite == null || !UvRect(sprite, out Rect uv) || !CoversRect(sprite)) return null;

            int w = Mathf.RoundToInt(sprite.rect.width), h = Mathf.RoundToInt(sprite.rect.height);
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(sprite.texture, rt, new Vector2(uv.width, uv.height), new Vector2(uv.x, uv.y));
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
                {
                    name = "NorseSense_" + name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
                };
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                // Sprite.border is left, bottom, right, top.
                Vector4 b = sprite.border;
                border = new RectOffset(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.z), Mathf.RoundToInt(b.w), Mathf.RoundToInt(b.y));
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Norse Sense] Couldn't copy UI sprite {name}: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // Paints `top` over `bottom` (same size), e.g. the checkbox marker into the checkbox.
        internal static Texture2D Composite(Texture2D bottom, Texture2D top)
        {
            if (bottom == null || top == null || bottom.width != top.width || bottom.height != top.height) return null;
            var a = bottom.GetPixels();
            var b = top.GetPixels();
            for (int i = 0; i < a.Length; i++)
            {
                float alpha = b[i].a;
                a[i] = new Color(Mathf.Lerp(a[i].r, b[i].r, alpha), Mathf.Lerp(a[i].g, b[i].g, alpha),
                    Mathf.Lerp(a[i].b, b[i].b, alpha), Mathf.Max(a[i].a, alpha));
            }
            var tex = new Texture2D(bottom.width, bottom.height, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels(a);
            tex.Apply();
            return tex;
        }
    }
}
