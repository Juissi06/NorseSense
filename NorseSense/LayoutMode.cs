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

        class Box
        {
            internal string Name;
            internal Rect Rect;
            internal System.Func<float, float, Rect> Where; // current place on screen, from the settings
            internal System.Action<Rect, float, float> Save;
            internal System.Action Reset;
            internal float Margin; // how close to the screen edge the panel itself goes
        }

        Box[] _boxes;
        Box _dragging;
        Vector2 _grab;
        float _sw, _sh; // the scaled screen size of the last OnGUI, to save a drag cut short by Esc

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
            else if (Active && key) Stop();
        }

        void Start()
        {
            Active = true;
            _dragging = null;
            // No message: layout mode shows its own instructions at the top.
        }

        void Stop()
        {
            if (!Active) return;
            Active = false;
            _dragging?.Save(_dragging.Rect, _sw, _sh); // a drag still in progress counts
            _dragging = null;
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
                    Margin = BaseView.Edge,
                    Where = (sw, sh) =>
                    {
                        Vector2 size = Size(BaseView.LegendRect, 320f, 250f);
                        return BaseView.PlaceLegend(size.x, size.y, sw, sh);
                    },
                    Save = BaseView.SaveLegendPlace,
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
                        return InfoBox.PlaceOnScreen(size.x, size.y, sw, sh);
                    },
                    Save = InfoBox.SavePlace,
                    Reset = () =>
                    {
                        Plugin.BuildInfoX.Value = (float)Plugin.BuildInfoX.DefaultValue;
                        Plugin.BuildInfoY.Value = (float)Plugin.BuildInfoY.DefaultValue;
                    }
                },
                new Box
                {
                    Name = "Comfort card",
                    Margin = ComfortInfo.Margin,
                    Where = (sw, sh) =>
                    {
                        Vector2 size = Size(ComfortInfo.LastRect, 260f, 90f);
                        return ComfortInfo.Place(size.x, size.y, sw, sh, Screen.height / sh);
                    },
                    Save = ComfortInfo.SavePlace,
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

        void OnGUI()
        {
            if (!Active) return;
            var e = Event.current;
            // Esc here rather than in Update: OnGUI runs after every Update, so the game's menu has already seen
            // the "text box open" that layout mode reports, and doesn't open on the same key press.
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Stop();
                return;
            }
            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);
            _sw = sw;
            _sh = sh;
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
                // The comfort card keeps clear of the legend, so it should see the legend box being moved here.
                if (box.Name == "Legend")
                {
                    BaseView.LegendRect = box.Rect;
                    BaseView.LegendDrawnAt = Time.unscaledTime;
                }
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
                        // The same edge margin the panel keeps, so it doesn't jump when let go.
                        float m = _dragging.Margin;
                        r.x = Mathf.Clamp(r.x, m, Mathf.Max(m, sw - r.width - m));
                        r.y = Mathf.Clamp(r.y, m, Mathf.Max(m, sh - r.height - m));
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
