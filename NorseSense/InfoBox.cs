using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NorseSense
{
    internal enum Tone { None, Good, Warn, Bad }

    // One row of the info box: icon, a short label ("Shelter"), the value ("No, open behind")
    // and optional muted detail ("cover 71% of 80%"). Tone colours the accent at the row's left edge.
    internal class InfoRow
    {
        internal Sprite Icon;
        internal string Label;
        internal string Value;
        internal string Detail;
        internal Tone Tone;

        internal InfoRow(string label, string value, string detail = null, Tone tone = Tone.None, Sprite icon = null)
        {
            Label = label;
            Value = value;
            Detail = detail;
            Tone = tone;
            Icon = icon;
        }
    }

    // Rows plus key hints for the footer.
    internal class InfoLines
    {
        internal readonly List<InfoRow> Rows = new List<InfoRow>(8);
        internal readonly List<string> HintKeys = new List<string>(6);
        internal readonly List<string> HintActions = new List<string>(6);
        internal int Count => Rows.Count;

        internal void Clear()
        {
            Rows.Clear();
            HintKeys.Clear();
            HintActions.Clear();
        }

        internal void Add(InfoRow row, Sprite icon = null)
        {
            if (row == null) return;
            if (icon != null && row.Icon == null) row.Icon = icon;
            Rows.Add(row);
        }

        internal void Add(Sprite icon, string label, string value, string detail = null, Tone tone = Tone.None) =>
            Rows.Add(new InfoRow(label, value, detail, tone, icon));

        internal void Hint(string key, string action)
        {
            HintKeys.Add(key);
            HintActions.Add(action);
        }
    }

    // The info box shown while building, resting or digging. Centred at the configured height, but moved
    // up out of the way when it would cover the game's own build HUD or key hints.
    internal static class InfoBox
    {
        const float RowH = 26f, PadX = 14f, PadY = 9f, IconSize = 20f, Gap = 8f, AccentW = 3f;
        const float HintH = 24f, KeyH = 18f, ObstacleRefresh = 0.5f, Margin = 10f;

        static readonly List<Rect> Obstacles = new List<Rect>();
        static readonly List<Graphic> TempGraphics = new List<Graphic>();
        static readonly Vector3[] Corners = new Vector3[4];
        static float _obstaclesAt = -1f;
        static float _obstacleScale;

        // Call from OnGUI.
        internal static void Draw(InfoLines lines)
        {
            if (lines.Count == 0 && lines.HintKeys.Count == 0) return;
            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);

            bool anyIcon = false;
            float labelW = 0f, contentW = 0f;
            foreach (var row in lines.Rows)
            {
                anyIcon |= row.Icon != null;
                if (row.Label != null) labelW = Mathf.Max(labelW, Ui.LineWidth(row.Label, Ui.Caption));
            }
            float iconCol = anyIcon ? IconSize + Gap : 0f;
            float labelCol = labelW > 0f ? labelW + 12f : 0f;
            foreach (var row in lines.Rows)
                contentW = Mathf.Max(contentW, ValueWidth(row));

            float hintsW = HintsWidth(lines);
            float w = Mathf.Max(AccentW + 6f + iconCol + labelCol + contentW, hintsW) + PadX * 2f;
            w = Mathf.Clamp(w, 260f, sw * 0.7f);
            float h = PadY * 2f + lines.Count * RowH + (lines.HintKeys.Count > 0 ? HintH + (lines.Count > 0 ? 7f : 0f) : 0f);

            var target = new Rect(Plugin.BuildInfoX.Value * (sw - w), Plugin.BuildInfoY.Value * (sh - h), w, h);
            LastRect = target;
            Rect box = Animate(AvoidVanillaHud(target, sw, sh));

            Color oldColor = GUI.color;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * _alpha);
            Ui.Panel(box);
            // Contents are laid out relative to the box and clipped by it, so rows slide in as it grows.
            GUI.BeginClip(box);
            float right = box.width - PadX, y = PadY;
            foreach (var row in lines.Rows)
            {
                float cx = PadX;
                if (row.Tone != Tone.None) Ui.Accent(new Rect(cx, y + 5f, AccentW, RowH - 10f), ToneColor(row.Tone));
                cx += AccentW + 6f;
                if (anyIcon)
                {
                    if (row.Icon != null) Ui.SlotIcon(new Rect(cx, y + (RowH - IconSize) * 0.5f, IconSize, IconSize), row.Icon);
                    cx += iconCol;
                }
                if (row.Label != null) Ui.Label(new Rect(cx, y, labelW + 4f, RowH), row.Label, Ui.Caption);
                cx += labelCol;

                float valueW = Ui.LineWidth(row.Value, Ui.Text);
                Ui.Label(new Rect(cx, y, Mathf.Max(0f, right - cx), RowH), row.Value, Ui.Text);
                if (!string.IsNullOrEmpty(row.Detail) && cx + valueW + 10f < right)
                    Ui.Label(new Rect(cx + valueW + 10f, y + 1f, right - cx - valueW - 10f, RowH), row.Detail, Ui.Small);
                y += RowH;
            }

            if (lines.HintKeys.Count > 0)
            {
                if (lines.Count > 0)
                {
                    Ui.Divider(PadX, y + 3f, box.width - PadX * 2f);
                    y += 7f;
                }
                DrawHints(lines, (box.width - hintsW) * 0.5f, y);
            }
            GUI.EndClip();
            GUI.color = oldColor;

            GUI.matrix = oldMatrix;
        }

        static Rect _rect;
        internal static Rect LastRect; // where it was last placed, for layout mode
        static float _alpha, _lastDrawn = -10f, _lastStep, _width, _shrinkAt;

        // Eases the box towards its new size and place instead of jumping when a row comes or goes, fades it in
        // when it appears, and holds the width briefly before shrinking so changing text doesn't make it twitch.
        static Rect Animate(Rect target)
        {
            float now = Time.unscaledTime;
            if (now - _lastDrawn > 0.25f)
            {
                _rect = new Rect(target.x, target.y + 6f, target.width, target.height);
                _alpha = 0f;
                _width = target.width;
                _lastStep = now;
            }
            _lastDrawn = now;

            if (target.width >= _width - 0.5f)
            {
                _width = target.width;
                _shrinkAt = now + 0.6f;
            }
            else if (now >= _shrinkAt) _width = target.width;
            target.x += (target.width - _width) * 0.5f;
            target.width = _width;

            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(now - _lastStep, 0.1f);
                _lastStep = now;
                float k = 1f - Mathf.Exp(-16f * dt);
                _rect.x = Mathf.Lerp(_rect.x, target.x, k);
                _rect.y = Mathf.Lerp(_rect.y, target.y, k);
                _rect.width = Mathf.Lerp(_rect.width, target.width, k);
                _rect.height = Mathf.Lerp(_rect.height, target.height, k);
                _alpha = Mathf.MoveTowards(_alpha, 1f, dt / 0.15f);
            }
            return _rect;
        }

        static float ValueWidth(InfoRow row)
        {
            float width = Ui.LineWidth(row.Value, Ui.Text);
            if (!string.IsNullOrEmpty(row.Detail)) width += 10f + Ui.LineWidth(row.Detail, Ui.Small);
            return width;
        }

        static float HintsWidth(InfoLines lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.HintKeys.Count; i++)
            {
                if (i > 0) width += 16f;
                width += Ui.KeycapWidth(lines.HintKeys[i]) + 6f + Ui.LineWidth(lines.HintActions[i], Ui.Small);
            }
            return width;
        }

        static void DrawHints(InfoLines lines, float x, float y)
        {
            for (int i = 0; i < lines.HintKeys.Count; i++)
            {
                if (i > 0) x += 16f;
                float keyW = Ui.Keycap(new Rect(x, y + (HintH - KeyH) * 0.5f, 0f, KeyH), lines.HintKeys[i]);
                x += keyW + 6f;
                float actionW = Ui.LineWidth(lines.HintActions[i], Ui.Small);
                Ui.Label(new Rect(x, y, actionW + 4f, HintH), lines.HintActions[i], Ui.Small);
                x += actionW;
            }
        }

        internal static Color ToneColor(Tone tone)
        {
            switch (tone)
            {
                case Tone.Good: return new Color(0.61f, 0.88f, 0.48f);
                case Tone.Warn: return new Color(0.94f, 0.69f, 0.29f);
                case Tone.Bad: return new Color(1f, 0.44f, 0.33f);
                default: return Ui.GoldColor;
            }
        }

        // Moves the box above any visible part of the game's build HUD (selected piece and its costs) and
        // build key hints it would overlap. Measured from the live UI so it follows UI scale and resolution.
        static Rect AvoidVanillaHud(Rect box, float sw, float sh)
        {
            float scale = Screen.height / sh;
            if (Time.unscaledTime - _obstaclesAt > ObstacleRefresh || !Mathf.Approximately(scale, _obstacleScale))
            {
                _obstaclesAt = Time.unscaledTime;
                _obstacleScale = scale;
                Obstacles.Clear();
                if (Hud.instance != null) Collect(Hud.instance.m_buildHud, scale, sw, sh);
                if (KeyHints.instance != null) Collect(KeyHints.instance.m_buildHints, scale, sw, sh);
            }

            // A few passes: moving above one element can land on another.
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                foreach (var o in Obstacles)
                {
                    var padded = new Rect(o.x - Margin, o.y - Margin, o.width + Margin * 2f, o.height + Margin * 2f);
                    if (!padded.Overlaps(box)) continue;
                    box.y = padded.yMin - box.height;
                    moved = true;
                }
                if (!moved) break;
            }
            box.y = Mathf.Max(Margin, box.y);
            return box;
        }

        // Everything visible under a HUD object, as one rect: for lists whose container is bigger than its content.
        internal static bool ContentRect(GameObject root, float scale, out Rect r)
        {
            r = default(Rect);
            if (root == null || !root.activeInHierarchy) return false;
            bool any = false;
            TempGraphics.Clear();
            root.GetComponentsInChildren(false, TempGraphics);
            foreach (var g in TempGraphics)
            {
                if (!g.enabled || g.color.a < 0.05f || !ScreenRect(g.rectTransform, scale, out Rect gr)) continue;
                r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, gr.xMin), Mathf.Min(r.yMin, gr.yMin),
                    Mathf.Max(r.xMax, gr.xMax), Mathf.Max(r.yMax, gr.yMax)) : gr;
                any = true;
            }
            return any;
        }

        // Where a piece of the game's HUD is on screen, in scaled GUI units (origin top-left). False if hidden.
        internal static bool ScreenRect(RectTransform rt, float scale, out Rect r)
        {
            r = default(Rect);
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            canvas = canvas.rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            rt.GetWorldCorners(Corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
            r = Rect.MinMaxRect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale,
                Mathf.Max(a.x, b.x) / scale, (Screen.height - Mathf.Min(a.y, b.y)) / scale);
            return r.width > 1f && r.height > 1f;
        }

        static void Collect(GameObject root, float scale, float sw, float sh)
        {
            if (root == null || !root.activeInHierarchy) return;
            TempGraphics.Clear();
            root.GetComponentsInChildren(false, TempGraphics);
            foreach (var g in TempGraphics)
            {
                if (!g.enabled || g.color.a < 0.05f || g.canvas == null) continue;
                var canvas = g.canvas.rootCanvas;
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                g.rectTransform.GetWorldCorners(Corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
                // Screen pixels (origin bottom-left) to scaled GUI units (origin top-left).
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale,
                    Mathf.Max(a.x, b.x) / scale, (Screen.height - Mathf.Min(a.y, b.y)) / scale);
                // Full-screen frames and backgrounds aren't things the box can avoid.
                if (r.width <= 1f || r.height <= 1f || r.width > sw * 0.6f || r.height > sh * 0.5f) continue;
                Obstacles.Add(r);
            }
        }
    }
}
