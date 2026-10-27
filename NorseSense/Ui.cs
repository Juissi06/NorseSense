using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NorseSense
{
    // Shared look for every Norse Sense panel: Valheim's own UI art and fonts where available (GameArt), with a
    // generated dark-and-gold fallback, shadowed text and one colour palette, so overlays read like the game's HUD.
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
        static GUIStyle _text, _textCenter, _small, _smallCenter, _smallRight, _titleStyle, _panelStyle, _caption, _keyText;
        static Texture2D _keycap;
        static GUIStyle _keycapStyle;

        // Valheim's own UI art when available (see GameArt); null means use the generated look above.
        static GUIStyle _hudFill, _hudBorder, _wood, _barTrack;
        static Texture2D _separator, _barFill;
        static RectOffset _buttonBorder = new RectOffset(6, 6, 6, 6), _fieldBorder = new RectOffset(6, 6, 6, 6);
        static int _toggleSize = 16;
        static bool _gameButtons;
        static GUISkin _skin;

        internal static GUIStyle Text { get { Init(); return _text; } }
        internal static GUIStyle TextCenter { get { Init(); return _textCenter; } }
        internal static GUIStyle Small { get { Init(); return _small; } }
        internal static GUIStyle SmallCenter { get { Init(); return _smallCenter; } }
        internal static GUIStyle SmallRight { get { Init(); return _smallRight; } }
        internal static GUIStyle Title { get { Init(); return _titleStyle; } }
        // Row labels in the info box: small gold text.
        internal static GUIStyle Caption { get { Init(); return _caption; } }
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

        // HUD panel: dark and see-through with the game's thin orange edge, like Valheim's own tooltips.
        internal static void Panel(Rect r)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(r.x - 2f, r.y + 1f, r.width + 6f, r.height + 5f), _panelShadow, ScaleMode.StretchToFill);
            Inset(r);
        }

        // Same look without the drop shadow, for areas inside a window.
        internal static void Inset(Rect r)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            if (_hudBorder == null)
            {
                _panelStyle.Draw(r, false, false, false, false);
                return;
            }
            _hudFill.Draw(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), false, false, false, false);
            _hudBorder.Draw(r, false, false, false, false);
        }

        // Big window: the game's wood panel, as used by the crafting and inventory screens.
        internal static void Window(Rect r)
        {
            Init();
            if (_wood == null)
            {
                Panel(r);
                return;
            }
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(r.x - 4f, r.y + 2f, r.width + 10f, r.height + 9f), _panelShadow, ScaleMode.StretchToFill);
            _wood.Draw(r, false, false, false, false);
        }

        // Divider: the game's gold separator, or a faint gold line.
        internal static void Divider(float x, float y, float width)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            if (_separator != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f * old.a);
                GUI.DrawTexture(new Rect(x, y - 1f, width, 3f), _separator, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.25f);
                GUI.DrawTexture(new Rect(x, y, width, 1f), Texture2D.whiteTexture);
            }
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
            float inset = 1f;
            if (_barTrack != null && r.height >= 10f) // the game's track has 5 px borders; thin bars get a plain one
            {
                _barTrack.Draw(r, false, false, false, false);
                inset = 2f;
            }
            else
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(r, _bar);
            }
            GUI.color = fill;
            var fillRect = new Rect(r.x + inset, r.y + inset,
                Mathf.Max(0f, (r.width - inset * 2f) * Mathf.Clamp01(fraction)), r.height - inset * 2f);
            GUI.DrawTexture(fillRect, _barFill != null ? _barFill : _bar, ScaleMode.StretchToFill);
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

        // Panel positions are stored as fractions of the free room on screen (0 = left/top, 1 = right/bottom),
        // kept a margin from the edges. These two convert both ways, so every panel and layout mode agree.
        internal static Rect FromFraction(float fx, float fy, float w, float h, float sw, float sh, float margin) =>
            new Rect(margin + fx * Mathf.Max(0f, sw - w - margin * 2f), margin + fy * Mathf.Max(0f, sh - h - margin * 2f), w, h);

        internal static void ToFraction(Rect r, float sw, float sh, float margin, out float fx, out float fy)
        {
            float roomX = sw - r.width - margin * 2f, roomY = sh - r.height - margin * 2f;
            fx = roomX > 0f ? Mathf.Clamp01((r.x - margin) / roomX) : 0.5f;
            fy = roomY > 0f ? Mathf.Clamp01((r.y - margin) / roomY) : 0.5f;
        }

        // A dark square with a faint gold edge, like an inventory slot, for an icon to sit in.
        internal static void Slot(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.22f * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * old.a);
            GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // An icon sitting in a slot, the way every panel shows its icons.
        internal static void SlotIcon(Rect r, Sprite sprite)
        {
            Slot(r);
            if (sprite != null) Icon(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), sprite);
        }

        // Draws a game sprite (also from an atlas), keeping its aspect, with a soft shadow.
        internal static void Icon(Rect r, Sprite sprite)
        {
            if (sprite == null || Event.current.type != EventType.Repaint || !GameArt.UvRect(sprite, out Rect uv)) return;
            Texture tex = sprite.texture;

            float aspect = uv.width * tex.width / Mathf.Max(1f, uv.height * tex.height);
            Rect fit = aspect >= 1f
                ? new Rect(r.x, r.y + (r.height - r.width / aspect) * 0.5f, r.width, r.width / aspect)
                : new Rect(r.x + (r.width - r.height * aspect) * 0.5f, r.y, r.height * aspect, r.height);

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f * old.a);
            GUI.DrawTextureWithTexCoords(new Rect(fit.x + 1.5f, fit.y + 1.5f, fit.width, fit.height), tex, uv, true);
            GUI.color = old;
            GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
        }

        // Thin rounded bar in a status colour, marking a row as good, warning or bad.
        internal static void Accent(Rect r, Color c)
        {
            Init();
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _bar, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        // A key drawn as a small key cap, like the game's own key hints. Height comes from r; returns the width used.
        internal static float Keycap(Rect r, string key)
        {
            Init();
            float w = KeycapWidth(key);
            r.width = w;
            if (Event.current.type == EventType.Repaint)
                _keycapStyle.Draw(r, false, false, false, false);
            GUI.Label(r, key, _keyText);
            return w;
        }

        internal static float KeycapWidth(string key)
        {
            Init();
            return Mathf.Max(20f, LineWidth(key, _keyText) + 12f);
        }

        // Unity key names to what's printed on the key: LeftAlt -> L Alt, Alpha1 -> 1, Mouse2 -> Middle mouse.
        internal static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt: return "L Alt";
                case KeyCode.RightAlt: return "R Alt";
                case KeyCode.LeftShift: return "L Shift";
                case KeyCode.RightShift: return "R Shift";
                case KeyCode.LeftControl: return "L Ctrl";
                case KeyCode.RightControl: return "R Ctrl";
                case KeyCode.Mouse0: return "Left mouse";
                case KeyCode.Mouse1: return "Right mouse";
                case KeyCode.Mouse2: return "Middle mouse";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
            }
            string name = key.ToString();
            return name.StartsWith("Alpha") ? name.Substring(5) : name;
        }

        internal static string KeyName(BepInEx.Configuration.KeyboardShortcut shortcut)
        {
            var parts = new List<string>(3);
            foreach (var m in shortcut.Modifiers) parts.Add(KeyName(m));
            parts.Add(KeyName(shortcut.MainKey));
            return string.Join(" + ", parts);
        }

        static readonly Dictionary<int, Sprite> StatusIcons = new Dictionary<int, Sprite>();

        // Icon of a status effect (e.g. SEMan.s_statusEffectShelter), as shown in the game's status bar.
        internal static Sprite StatusIcon(int hash)
        {
            if (StatusIcons.TryGetValue(hash, out var sprite) && sprite != null) return sprite;
            sprite = ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(hash)?.m_icon : null;
            if (sprite != null) StatusIcons[hash] = sprite;
            return sprite;
        }

        static readonly GUIContent Measure = new GUIContent();

        internal static float LineWidth(string text, GUIStyle style)
        {
            Measure.text = text;
            return style.CalcSize(Measure).x;
        }

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

        static bool _artReady;

        // Textures, fonts and the game's UI art. Copying sprites renders to a texture, so Plugin.Update calls this
        // once in game, outside OnGUI; Init falls back to it if a panel is drawn first.
        internal static void PrepareArt()
        {
            if (_artReady) return;
            _artReady = true;

            // Valheim's own fonts. They live in the game's Resources, so load them if nothing has yet.
            FindFonts(Resources.FindObjectsOfTypeAll<Font>());
            if (_body == null || _title == null) FindFonts(Resources.LoadAll<Font>(""));
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
            _keycap = Rounded(new Color(0.16f, 0.13f, 0.10f, 0.95f), new Color(0.62f, 0.52f, 0.36f, 0.9f), new Color(1f, 1f, 1f, 0.07f));

            _panelStyle = new GUIStyle { normal = { background = _panel }, border = new RectOffset(6, 6, 6, 6) };
            LoadGameArt();
        }

        // Styles need GUI.skin, which only exists inside OnGUI.
        static void Init()
        {
            if (_ready) return;
            _ready = true;
            PrepareArt();

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
            _caption = new GUIStyle(_text) { fontSize = 13 };
            _caption.normal.textColor = GoldColor;
            _keyText = new GUIStyle(_text) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _keycapStyle = new GUIStyle { normal = { background = _keycap }, border = new RectOffset(6, 6, 6, 6) };
            _keyText.normal.textColor = new Color(0.96f, 0.93f, 0.86f);

            BuildSkin();
        }

        // Swaps in Valheim's own UI sprites where they can be found.
        static void LoadGameArt()
        {
            var border = GameArt.Copy("panel_border_128", out var edge);
            if (border != null)
            {
                _hudBorder = new GUIStyle { normal = { background = border }, border = edge };
                var dark = new Color(0.06f, 0.045f, 0.03f, 0.82f);
                _hudFill = new GUIStyle { normal = { background = Rounded(dark, dark, new Color(0f, 0f, 0f, 0f)) }, border = new RectOffset(6, 6, 6, 6) };
            }

            var wood = GameArt.Copy("panel_bkg_256", out var woodBorder);
            if (wood != null) _wood = new GUIStyle { normal = { background = wood }, border = woodBorder };

            _separator = GameArt.Copy("panel_separator", out _);

            var track = GameArt.Copy("skill_bkg", out var trackBorder);
            if (track != null) _barTrack = new GUIStyle { normal = { background = track }, border = trackBorder };
            _barFill = GameArt.Copy("bar_gradient", out _);

            var button = GameArt.Copy("button", out var buttonBorder);
            var buttonHover = GameArt.Copy("button_highlight", out _);
            var buttonActive = GameArt.Copy("button_pressed", out _);
            if (button != null && buttonHover != null && buttonActive != null)
            {
                _button = button;
                _buttonHover = buttonHover;
                _buttonActive = buttonActive;
                _buttonBorder = buttonBorder;
                _gameButtons = true;
            }

            var field = GameArt.Copy("text_field", out var fieldBorder);
            var fieldFocus = GameArt.Copy("text_field_highlight", out _);
            if (field != null && fieldFocus != null)
            {
                _field = field;
                _fieldFocus = fieldFocus;
                _fieldBorder = fieldBorder;
            }

            // IMGUI draws toggle boxes at texture size, so shrink the 28 px checkbox to fit 24 px rows.
            var box = GameArt.Copy("checkbox", out _);
            var boxOn = GameArt.Composite(box, GameArt.Copy("checkbox_marker", out _));
            if (box != null && boxOn != null)
            {
                _toggleSize = 18;
                _toggleOff = Shrink(box, _toggleSize);
                _toggleOn = Shrink(boxOn, _toggleSize);
            }

            Debug.Log($"[Norse Sense] UI art: {(_hudBorder != null ? "game" : "built-in")} panels, " +
                      $"{(_wood != null ? "game" : "built-in")} window, {(_gameButtons ? "game" : "built-in")} buttons.");
        }

        // The box plus three transparent columns on the right.
        static Texture2D PadRight(Texture2D box)
        {
            int w = box.width + 3, h = box.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < box.width; x++)
                    pixels[y * w + x] = box.GetPixel(x, y);
            tex.SetPixels(pixels); // the extra columns stay transparent (default Color is 0,0,0,0)
            tex.Apply();
            return tex;
        }

        // A copy with every pixel's colour multiplied (alpha kept), e.g. to tint the ticked checkbox green.
        static Texture2D Tint(Texture2D source, Color tint)
        {
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                pixels[i] = new Color(Mathf.Clamp01(c.r * tint.r), Mathf.Clamp01(c.g * tint.g), Mathf.Clamp01(c.b * tint.b), c.a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D Shrink(Texture2D source, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static void FindFonts(Font[] fonts)
        {
            foreach (var font in fonts)
            {
                if (font == null) continue;
                if (_body == null && font.name == "AveriaSerifLibre-Bold") _body = font;
                else if (_title == null && font.name == "Norsebold") _title = font;
            }
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
                border = _buttonBorder, padding = new RectOffset(8, 8, 4, 4)
            };
            _skin.button.normal.background = _skin.button.onNormal.background = _button;
            _skin.button.hover.background = _skin.button.onHover.background = _buttonHover;
            _skin.button.active.background = _skin.button.onActive.background = _buttonActive;
            _skin.button.focused.background = _skin.button.onFocused.background = _button;
            Colors(_skin.button, cream);
            _skin.button.hover.textColor = GoldColor;

            _skin.textField = new GUIStyle(_skin.textField)
            {
                font = _body, fontSize = 14, border = _fieldBorder, padding = new RectOffset(7, 7, 4, 4)
            };
            _skin.textField.normal.background = _skin.textField.hover.background = _field;
            _skin.textField.focused.background = _skin.textField.active.background = _fieldFocus;
            Colors(_skin.textField, cream);

            // Left/top border covers the box, and the 9-slice's middle and right come from transparent columns
            // (PadRight), so only the box is drawn and a wide toggle doesn't smear its edge into a bar.
            _skin.toggle = new GUIStyle(_skin.toggle)
            {
                font = _body, fontSize = 14, border = new RectOffset(_toggleSize, 2, _toggleSize, 0),
                padding = new RectOffset(_toggleSize + 6, 2, 0, 0), overflow = new RectOffset(0, 0, -(24 - _toggleSize) / 2, 0),
                alignment = TextAnchor.MiddleLeft
            };
            var off = PadRight(_toggleOff);
            var on = PadRight(Tint(_toggleOn, new Color(0.75f, 1.35f, 0.6f)));
            _skin.toggle.normal.background = _skin.toggle.hover.background = _skin.toggle.active.background = _skin.toggle.focused.background = off;
            _skin.toggle.onNormal.background = _skin.toggle.onHover.background = _skin.toggle.onActive.background = _skin.toggle.onFocused.background = on;
            Colors(_skin.toggle, cream);
            _skin.toggle.hover.textColor = GoldColor;
            // On reads green (box and label), so it's clear at a glance which switches are on.
            _skin.toggle.onNormal.textColor = _skin.toggle.onActive.textColor = _skin.toggle.onFocused.textColor = new Color(0.61f, 0.88f, 0.48f);
            _skin.toggle.onHover.textColor = new Color(0.75f, 0.95f, 0.62f);

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
