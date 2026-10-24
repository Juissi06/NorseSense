using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NorseSense
{
    // Shared look for every Norse Sense panel: dark wood panels with a thin gold edge, Valheim's own fonts,
    // shadowed text and one colour palette, so the overlays read like part of the game's HUD.
    internal static class Ui
    {
        // Palette for rich text. Matches the warm tones of Valheim's HUD.
        internal const string Good = "#9be07a";
        internal const string Warn = "#f0b04a";
        internal const string Bad = "#ff6f55";
        internal const string Info = "#8ec9ff";
        internal const string Muted = "#b9ad98";
        internal const string Gold = "#e8c77e";

        internal static readonly Color GoldColor = new Color(0.91f, 0.78f, 0.49f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.85f);

        static bool _ready;
        static Font _body, _title;
        static Texture2D _panel, _panelShadow, _button, _buttonHover, _buttonActive, _field, _fieldFocus,
            _toggleOff, _toggleOn, _scrollTrack, _scrollThumb, _bar;
        static GUIStyle _text, _textCenter, _small, _smallCenter, _smallRight, _titleStyle, _panelStyle;
        static GUISkin _skin;

        internal static GUIStyle Text { get { Init(); return _text; } }
        internal static GUIStyle TextCenter { get { Init(); return _textCenter; } }
        internal static GUIStyle Small { get { Init(); return _small; } }
        internal static GUIStyle SmallCenter { get { Init(); return _smallCenter; } }
        internal static GUIStyle SmallRight { get { Init(); return _smallRight; } }
        internal static GUIStyle Title { get { Init(); return _titleStyle; } }
        internal static GUISkin Skin { get { Init(); return _skin; } }

        // ---------- Drawing helpers (call from OnGUI) ----------

        // Scales the GUI so 1080p layouts look the same at any resolution. Returns the matrix to restore.
        internal static Matrix4x4 BeginScaled(out float width, out float height)
        {
            float scale = Screen.height / 1080f * Plugin.LegendScale.Value;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            width = Screen.width / scale;
            height = Screen.height / scale;
            return old;
        }

        internal static void Panel(Rect r)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(r.x - 2f, r.y + 1f, r.width + 6f, r.height + 5f), _panelShadow, ScaleMode.StretchToFill);
            _panelStyle.Draw(r, false, false, false, false);
        }

        // Thin gold divider, e.g. under a panel title.
        internal static void Divider(float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.35f);
            GUI.DrawTexture(new Rect(x, y, width, 1f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Label with a soft drop shadow so it stays readable over snow, sky and fire.
        internal static void Label(Rect r, string text, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color old = style.normal.textColor;
                style.normal.textColor = ShadowColor;
                style.Draw(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), Plain(text), false, false, false, false);
                style.normal.textColor = old;
            }
            GUI.Label(r, text, style);
        }

        // Horizontal fill bar with a dark track.
        internal static void Bar(Rect r, float fraction, Color fill)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, _bar);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, Mathf.Max(0f, (r.width - 2f) * Mathf.Clamp01(fraction)), r.height - 2f), _bar);
            GUI.color = old;
        }

        // Small coloured square for legends.
        internal static void Swatch(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Texture2D.whiteTexture);
            c.a = Mathf.Max(c.a, 0.85f);
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        internal static float LineWidth(string text, GUIStyle style) => style.CalcSize(new GUIContent(text)).x;

        // The shadow pass must not inherit <color> tags, or it would be drawn in colour.
        static readonly Regex ColorTags = new Regex("</?color[^>]*>", RegexOptions.Compiled);
        static readonly Dictionary<string, string> PlainCache = new Dictionary<string, string>();

        static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<color", System.StringComparison.Ordinal) < 0) return text;
            if (PlainCache.TryGetValue(text, out var plain)) return plain;
            if (PlainCache.Count > 256) PlainCache.Clear();
            return PlainCache[text] = ColorTags.Replace(text, "");
        }

        // ---------- Setup ----------

        static void Init()
        {
            if (_ready) return;
            _ready = true;

            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (font.name == "AveriaSerifLibre-Bold") _body = font;
                else if (font.name == "Norsebold") _title = font;
            }
            if (_title == null) _title = _body;

            _panel = Rounded(new Color(0.10f, 0.075f, 0.05f, 0.88f), new Color(0.80f, 0.64f, 0.36f, 0.75f),
                new Color(0f, 0f, 0f, 0.5f));
            _panelShadow = Rounded(new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0.2f), new Color(0f, 0f, 0f, 0.35f));
            _button = Rounded(new Color(0.24f, 0.18f, 0.12f, 0.95f), new Color(0.62f, 0.50f, 0.30f, 0.9f), new Color(1f, 1f, 1f, 0.06f));
            _buttonHover = Rounded(new Color(0.33f, 0.25f, 0.16f, 0.97f), new Color(0.90f, 0.74f, 0.43f, 1f), new Color(1f, 1f, 1f, 0.08f));
            _buttonActive = Rounded(new Color(0.16f, 0.12f, 0.08f, 1f), new Color(0.95f, 0.80f, 0.50f, 1f), new Color(0f, 0f, 0f, 0.3f));
            _field = Rounded(new Color(0.05f, 0.04f, 0.03f, 0.85f), new Color(0.45f, 0.37f, 0.24f, 0.9f), new Color(0f, 0f, 0f, 0.4f));
            _fieldFocus = Rounded(new Color(0.07f, 0.055f, 0.04f, 0.92f), new Color(0.95f, 0.80f, 0.50f, 1f), new Color(0f, 0f, 0f, 0.4f));
            _toggleOff = Rounded(new Color(0.05f, 0.04f, 0.03f, 0.9f), new Color(0.62f, 0.50f, 0.30f, 0.9f), new Color(0f, 0f, 0f, 0.4f));
            _toggleOn = Check();
            _scrollTrack = Rounded(new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            _scrollThumb = Rounded(new Color(0.62f, 0.50f, 0.30f, 0.8f), new Color(0.80f, 0.64f, 0.36f, 0.9f), new Color(1f, 1f, 1f, 0.1f));
            _bar = Rounded(Color.white, Color.white, Color.white);

            _panelStyle = new GUIStyle { normal = { background = _panel }, border = new RectOffset(6, 6, 6, 6) };

            _text = new GUIStyle(GUI.skin.label)
            {
                font = _body, fontSize = 15, richText = true, wordWrap = false,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0)
            };
            _text.normal.textColor = new Color(0.96f, 0.93f, 0.86f);
            _textCenter = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(_text) { fontSize = 13 };
            _small.normal.textColor = new Color(0.78f, 0.72f, 0.62f);
            _smallCenter = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _smallRight = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
            _titleStyle = new GUIStyle(_text) { font = _title, fontSize = 19 };
            _titleStyle.normal.textColor = GoldColor;

            BuildSkin();
        }

        static void BuildSkin()
        {
            _skin = Object.Instantiate(GUI.skin);
            _skin.name = "NorseSense";
            if (_body != null) _skin.font = _body;

            var cream = new Color(0.96f, 0.93f, 0.86f);
            void Colors(GUIStyle s, Color c)
            {
                s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = c;
                s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = c;
            }

            _skin.label = new GUIStyle(_skin.label) { font = _body, fontSize = 15, richText = true };
            Colors(_skin.label, cream);

            _skin.button = new GUIStyle(_skin.button)
            {
                font = _body, fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(8, 8, 4, 4)
            };
            _skin.button.normal.background = _skin.button.onNormal.background = _button;
            _skin.button.hover.background = _skin.button.onHover.background = _buttonHover;
            _skin.button.active.background = _skin.button.onActive.background = _buttonActive;
            _skin.button.focused.background = _skin.button.onFocused.background = _button;
            Colors(_skin.button, cream);
            _skin.button.hover.textColor = GoldColor;

            _skin.textField = new GUIStyle(_skin.textField)
            {
                font = _body, fontSize = 14, border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(7, 7, 4, 4)
            };
            _skin.textField.normal.background = _skin.textField.hover.background = _field;
            _skin.textField.focused.background = _skin.textField.active.background = _fieldFocus;
            Colors(_skin.textField, cream);

            // Left/top border covers the whole 16 px box, so only the box is drawn and the label sits beside it.
            _skin.toggle = new GUIStyle(_skin.toggle)
            {
                font = _body, fontSize = 14, border = new RectOffset(16, 0, 16, 0),
                padding = new RectOffset(22, 2, 0, 0), overflow = new RectOffset(0, 0, -4, 0),
                alignment = TextAnchor.MiddleLeft
            };
            _skin.toggle.normal.background = _skin.toggle.hover.background = _skin.toggle.active.background = _toggleOff;
            _skin.toggle.onNormal.background = _skin.toggle.onHover.background = _skin.toggle.onActive.background = _toggleOn;
            Colors(_skin.toggle, cream);
            _skin.toggle.hover.textColor = _skin.toggle.onHover.textColor = GoldColor;

            _skin.box = new GUIStyle(_skin.box) { border = new RectOffset(6, 6, 6, 6) };
            _skin.box.normal.background = _panel;

            _skin.verticalScrollbar = new GUIStyle(_skin.verticalScrollbar)
            {
                border = new RectOffset(6, 6, 6, 6), fixedWidth = 12f, margin = new RectOffset(4, 0, 0, 0)
            };
            _skin.verticalScrollbar.normal.background = _scrollTrack;
            _skin.verticalScrollbarThumb = new GUIStyle(_skin.verticalScrollbarThumb)
            {
                border = new RectOffset(6, 6, 6, 6), fixedWidth = 12f
            };
            _skin.verticalScrollbarThumb.normal.background = _scrollThumb;
        }

        // 16x16 rounded rectangle: fill, 1 px outer edge and 1 px inner highlight/shade, for 9-slicing with border 6.
        static Texture2D Rounded(Color fill, Color edge, Color inner)
        {
            const int size = 16;
            const float radius = 4f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the rounded rect, measured from the pixel centre.
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = Mathf.Max(radius - px, 0f, px - (size - radius));
                    float dy = Mathf.Max(radius - py, 0f, py - (size - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 inside
                    float coverage = Mathf.Clamp01(0.5f - dist);
                    Color c;
                    if (dist <= -2f) c = fill;
                    else if (dist > -1f) c = edge;
                    else c = new Color(Mathf.Lerp(fill.r, inner.r, inner.a), Mathf.Lerp(fill.g, inner.g, inner.a),
                        Mathf.Lerp(fill.b, inner.b, inner.a), fill.a);
                    c.a *= coverage;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Checked toggle: the empty box with a gold square inside.
        static Texture2D Check()
        {
            var tex = Rounded(new Color(0.05f, 0.04f, 0.03f, 0.9f), new Color(0.90f, 0.74f, 0.43f, 1f), new Color(0f, 0f, 0f, 0.4f));
            for (int y = 5; y < 11; y++)
                for (int x = 5; x < 11; x++)
                    tex.SetPixel(x, y, new Color(0.93f, 0.78f, 0.45f, 1f));
            tex.Apply();
            return tex;
        }
    }
}
