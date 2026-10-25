using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorseSense
{
    internal enum ViewMode { Off, Spawns, Repairs, Comfort }

    // Which modes F7 steps through.
    [Flags]
    internal enum ViewModes { None = 0, Spawns = 1, Repairs = 2, Comfort = 4, All = Spawns | Repairs | Comfort }

    // The base view: one overlay key (F7) that cycles Spawns -> Repairs -> Comfort -> off, and one legend
    // panel shared by the modes, so each mode adds depth without its own key, window or settings page.
    internal static class BaseView
    {
        static readonly ViewMode[] Order = { ViewMode.Spawns, ViewMode.Repairs, ViewMode.Comfort };

        internal static ViewMode Mode => Plugin.BaseViewMode.Value;

        static bool InCycle(ViewMode mode)
        {
            var modes = Plugin.BaseViewModes.Value;
            switch (mode)
            {
                case ViewMode.Spawns: return (modes & ViewModes.Spawns) != 0;
                case ViewMode.Repairs: return (modes & ViewModes.Repairs) != 0;
                case ViewMode.Comfort: return (modes & ViewModes.Comfort) != 0;
                default: return false;
            }
        }

        // Next enabled mode after the current one, then off.
        internal static void Cycle()
        {
            int start = Array.IndexOf(Order, Mode);
            ViewMode next = ViewMode.Off;
            for (int i = start + 1; i < Order.Length; i++)
            {
                if (!InCycle(Order[i])) continue;
                next = Order[i];
                break;
            }
            Plugin.BaseViewMode.Value = next;
            Plugin.Notify("Base view", next == ViewMode.Off ? "off" : Name(next), next != ViewMode.Off);
        }

        internal static string Name(ViewMode mode) => mode.ToString();

        // ---------- Legend ----------

        // One line of the legend: a colour swatch (clear for none) and rich text.
        internal class Legend
        {
            internal readonly List<KeyValuePair<Color, string>> Lines = new List<KeyValuePair<Color, string>>(12);
            internal string BarText;
            internal float BarFraction = -1f; // < 0: no bar
            internal Color BarColor;

            internal void Clear()
            {
                Lines.Clear();
                BarText = null;
                BarFraction = -1f;
            }

            internal void Add(Color swatch, string text) => Lines.Add(new KeyValuePair<Color, string>(swatch, text));
            internal void Add(string text) => Lines.Add(new KeyValuePair<Color, string>(Color.clear, text));
        }

        internal static bool LegendVisible =>
            Plugin.ShowLegend.Value && Player.m_localPlayer != null && !Hud.IsUserHidden() && !Menu.IsVisible() &&
            !InventoryGui.IsVisible() && !Minimap.IsOpen();

        // Tabs for the modes in the cycle (current one gold), an optional bar, the colour key, then stats
        // (lines without a colour) under a divider. Call from OnGUI.
        internal static void DrawLegend(Legend legend)
        {
            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);

            int keyLines = 0;
            foreach (var line in legend.Lines) if (line.Key.a > 0f) keyLines++;
            int statLines = legend.Lines.Count - keyLines;

            const float lineH = 22f, pad = 12f, tabsH = 28f, barH = 37f;
            bool bar = legend.BarFraction >= 0f;
            float content = TabsWidth();
            if (bar) content = Mathf.Max(content, Ui.LineWidth(legend.BarText, Ui.Small));
            foreach (var line in legend.Lines)
                content = Mathf.Max(content, Ui.LineWidth(line.Value, Ui.Text) + (line.Key.a > 0f ? 22f : 0f));
            float w = Mathf.Clamp(content + pad * 2f + 4f, 300f, 520f);
            float h = pad * 2 + tabsH + 6f + (bar ? barH : 0f) + legend.Lines.Count * lineH + (statLines > 0 && keyLines > 0 ? 9f : 0f);
            float x = Plugin.LegendX.Value * (sw - w - 20f) + 10f;
            float y = Plugin.LegendY.Value * (sh - h - 20f) + 10f;
            float cx = x + pad, cy = y + pad, inner = w - pad * 2;

            Ui.Panel(new Rect(x, y, w, h));
            DrawTabs(cx, cy, inner, tabsH);
            cy += tabsH;
            Ui.Divider(cx, cy + 2f, inner);
            cy += 6f;

            if (bar)
            {
                Ui.Label(new Rect(cx, cy, inner, 18f), legend.BarText, Ui.Small);
                Ui.Bar(new Rect(cx, cy + 19f, inner, 12f), legend.BarFraction, legend.BarColor);
                cy += barH;
            }

            bool dividerDrawn = keyLines == 0;
            foreach (var line in legend.Lines)
            {
                float textX = cx;
                if (line.Key.a > 0f)
                {
                    Ui.Swatch(new Rect(cx + 1f, cy + 5f, 12f, 12f), line.Key);
                    textX += 22f;
                }
                else if (!dividerDrawn)
                {
                    dividerDrawn = true;
                    Ui.Divider(cx, cy + 4f, inner);
                    cy += 9f;
                }
                Ui.Label(new Rect(textX, cy, inner - (textX - cx), lineH), line.Value, Ui.Text);
                cy += lineH;
            }

            GUI.matrix = oldMatrix;
        }

        static float TabsWidth()
        {
            float width = Ui.KeycapWidth(Ui.KeyName(Plugin.ToggleKey.Value)) + 6f + Ui.LineWidth("Next", Ui.Small) + 16f;
            foreach (var mode in Order)
                if (InCycle(mode)) width += Ui.LineWidth($"<b>{Name(mode)}</b>", Ui.Title) + 16f;
            return width;
        }

        // "Spawns  Repairs  Comfort        [F7] Next": shows where you are in the cycle and how to move on.
        static void DrawTabs(float x, float y, float width, float height)
        {
            string key = Ui.KeyName(Plugin.ToggleKey.Value);
            float keyW = Ui.KeycapWidth(key);
            float hintW = keyW + 6f + Ui.LineWidth("Next", Ui.Small);
            Ui.Keycap(new Rect(x + width - hintW, y + (height - 18f) * 0.5f, 0f, 18f), key);
            Ui.Label(new Rect(x + width - hintW + keyW + 6f, y, hintW, height), "Next", Ui.Small);

            float tx = x;
            foreach (var mode in Order)
            {
                if (!InCycle(mode)) continue;
                bool current = mode == Mode;
                string text = current ? $"<b>{Name(mode)}</b>" : $"<color={Ui.Muted}>{Name(mode)}</color>";
                var style = current ? Ui.Title : Ui.Text;
                float tw = Ui.LineWidth(text, style);
                Ui.Label(new Rect(tx, y, tw + 4f, height), text, style);
                if (current) Ui.Accent(new Rect(tx, y + height - 3f, tw, 2f), Ui.GoldColor);
                tx += tw + 16f;
            }
        }
    }
}
