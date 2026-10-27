using BepInEx;
using UnityEngine;

namespace NorseSense
{
    // Layout mode: one key, then drag the legend, the build info and the comfort card where you want them, and
    // they stay there. Replaces fiddling with position sliders. Right-click a panel to put it back where it was
    // by default; the key again or Esc finishes. While it's on the game thinks a text box is open, which frees
    // the mouse and holds the camera and movement still (see TextInputVisiblePatch).
    internal class LayoutMode : MonoBehaviour
    {
        internal static bool Active;

        const float Edge = 10f;

        class Box
        {
            internal string Name;
            internal Rect Rect;
            internal System.Func<float, float, Rect> Where; // current place on screen, from the settings
            internal System.Action<Rect, float, float> Save;
            internal System.Action Reset;
        }

        Box[] _boxes;
        Box _dragging;
        Vector2 _grab;

        void Update()
        {
            var player = Player.m_localPlayer;
            if (Active && (player == null || Menu.IsVisible()))
            {
                Stop();
                return;
            }
            if (player == null) return;
            bool key = Plugin.LayoutKey.Value.IsDown();
            if (!Active && key && !Plugin.IsTyping()) Start();
            else if (Active && (key || UnityInput.Current.GetKeyDown(KeyCode.Escape))) Stop();
        }

        void Start()
        {
            Active = true;
            _dragging = null;
            Plugin.Notify("Layout", "drag the panels, Esc when done", true);
        }

        void Stop()
        {
            if (!Active) return;
            Active = false;
            _dragging = null;
            Plugin.Notify("Layout", "saved", false);
        }

        void OnDisable() => Active = false;

        Box[] Boxes()
        {
            if (_boxes != null) return _boxes;
            _boxes = new[]
            {
                new Box
                {
                    Name = "Legend",
                    Where = (sw, sh) =>
                    {
                        Vector2 size = Size(BaseView.LegendRect, 320f, 250f);
                        return new Rect(Plugin.LegendX.Value * (sw - size.x - Edge * 2f) + Edge, Plugin.LegendY.Value * (sh - size.y - Edge * 2f) + Edge, size.x, size.y);
                    },
                    Save = (r, sw, sh) =>
                    {
                        Plugin.LegendX.Value = Fraction(r.x - Edge, sw - r.width - Edge * 2f);
                        Plugin.LegendY.Value = Fraction(r.y - Edge, sh - r.height - Edge * 2f);
                    },
                    Reset = () =>
                    {
                        Plugin.LegendX.Value = (float)Plugin.LegendX.DefaultValue;
                        Plugin.LegendY.Value = (float)Plugin.LegendY.DefaultValue;
                    }
                },
                new Box
                {
                    Name = "Build info",
                    Where = (sw, sh) =>
                    {
                        Vector2 size = Size(InfoBox.LastRect, 360f, 110f);
                        return new Rect(Plugin.BuildInfoX.Value * (sw - size.x), Plugin.BuildInfoY.Value * (sh - size.y), size.x, size.y);
                    },
                    Save = (r, sw, sh) =>
                    {
                        Plugin.BuildInfoX.Value = Fraction(r.x, sw - r.width);
                        Plugin.BuildInfoY.Value = Fraction(r.y, sh - r.height);
                    },
                    Reset = () =>
                    {
                        Plugin.BuildInfoX.Value = (float)Plugin.BuildInfoX.DefaultValue;
                        Plugin.BuildInfoY.Value = (float)Plugin.BuildInfoY.DefaultValue;
                    }
                },
                new Box
                {
                    Name = "Comfort card",
                    Where = (sw, sh) =>
                    {
                        Vector2 size = Size(ComfortInfo.LastRect, 260f, 90f);
                        if (Plugin.ComfortX.Value < 0f || Plugin.ComfortY.Value < 0f)
                            return new Rect(sw - 24f - size.x, sh * 0.4f, size.x, size.y); // automatic: under the status effects
                        return new Rect(Plugin.ComfortX.Value * (sw - size.x - Edge * 2f) + Edge, Plugin.ComfortY.Value * (sh - size.y - Edge * 2f) + Edge, size.x, size.y);
                    },
                    Save = (r, sw, sh) =>
                    {
                        Plugin.ComfortX.Value = Fraction(r.x - Edge, sw - r.width - Edge * 2f);
                        Plugin.ComfortY.Value = Fraction(r.y - Edge, sh - r.height - Edge * 2f);
                    },
                    Reset = () =>
                    {
                        Plugin.ComfortX.Value = -1f;
                        Plugin.ComfortY.Value = -1f;
                    }
                },
            };
            return _boxes;
        }

        // The panel's real size when it has been shown, otherwise a typical one.
        static Vector2 Size(Rect last, float w, float h) => last.width > 1f ? new Vector2(last.width, last.height) : new Vector2(w, h);

        static float Fraction(float offset, float room) => room > 0f ? Mathf.Clamp01(offset / room) : 0f;

        void OnGUI()
        {
            if (!Active) return;
            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);
            var e = Event.current;
            Vector2 mouse = e.mousePosition;

            // Dim the game a little so the panels stand out, with the instructions at the top.
            if (e.type == EventType.Repaint)
            {
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture);
                GUI.color = old;
            }
            string help = $"<color={Ui.Gold}>Layout</color>   Drag a panel to move it, right-click to put it back.   " +
                          $"<color={Ui.Muted}>{Ui.KeyName(Plugin.LayoutKey.Value)} or Esc when done</color>";
            float hw = Ui.LineWidth(help, Ui.Text) + 40f;
            var helpRect = new Rect((sw - hw) * 0.5f, 24f, hw, 40f);
            Ui.Panel(helpRect);
            Ui.Label(new Rect(helpRect.x + 20f, helpRect.y, hw - 40f, 40f), help, Ui.Text);

            foreach (var box in Boxes())
            {
                if (box != _dragging) box.Rect = box.Where(sw, sh);
                bool hot = box == _dragging || box.Rect.Contains(mouse);
                Ui.Panel(box.Rect);
                if (e.type == EventType.Repaint && hot) Ui.Accent(new Rect(box.Rect.x, box.Rect.y, box.Rect.width, 2f), Ui.GoldColor);
                Ui.Label(new Rect(box.Rect.x, box.Rect.y, box.Rect.width, box.Rect.height), $"<b>{box.Name}</b>", Ui.TextCenter);
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    for (int i = _boxes.Length - 1; i >= 0; i--)
                    {
                        if (!_boxes[i].Rect.Contains(mouse)) continue;
                        if (e.button == 1) _boxes[i].Reset();
                        else
                        {
                            _dragging = _boxes[i];
                            _grab = mouse - _boxes[i].Rect.position;
                        }
                        e.Use();
                        break;
                    }
                    break;
                case EventType.MouseDrag:
                    if (_dragging != null)
                    {
                        var r = _dragging.Rect;
                        r.position = mouse - _grab;
                        r.x = Mathf.Clamp(r.x, 0f, sw - r.width);
                        r.y = Mathf.Clamp(r.y, 0f, sh - r.height);
                        _dragging.Rect = r;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_dragging != null)
                    {
                        _dragging.Save(_dragging.Rect, sw, sh);
                        _dragging = null;
                        e.Use();
                    }
                    break;
            }
            GUI.matrix = oldMatrix;
        }
    }
}
