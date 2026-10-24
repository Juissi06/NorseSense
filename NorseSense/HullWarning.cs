using UnityEngine;

namespace NorseSense
{
    // While aboard a ship: a small hull readout, plus warnings when it takes damage or drops below set levels.
    internal class HullWarning : MonoBehaviour
    {
        const float CheckInterval = 0.25f;
        const float DamageMessageCooldown = 5f;

        Ship _ship;
        WearNTear _hull;
        float _lastPercent = -1f;
        float _lowestWarned = 2f;
        float _timer;
        float _lastDamageMessage = -999f;
        float _flashUntil;

        GUIStyle _rightStyle;

        void Update()
        {
            var player = Player.m_localPlayer;
            Ship ship = player != null ? Ship.GetLocalShip() : null;
            if (ship != _ship)
            {
                _ship = ship;
                _hull = ship != null ? ship.GetComponent<WearNTear>() : null;
                _lastPercent = -1f;
                _lowestWarned = 2f;
            }
            if (_hull == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = CheckInterval;

            float percent = _hull.GetHealthPercentage();
            if (_lastPercent >= 0f && percent < _lastPercent - 0.001f)
                OnDamaged(player, percent);
            else if (percent > _lastPercent)
                _lowestWarned = 2f; // repaired: allow the threshold warnings again
            _lastPercent = percent;
        }

        void OnDamaged(Player player, float percent)
        {
            _flashUntil = Time.time + 1.5f;
            if (!Plugin.HullWarnings.Value) return;

            // Threshold crossings always warn; plain damage is rate-limited (Ashlands water damages constantly).
            float threshold = percent <= Plugin.HullCriticalLevel.Value ? Plugin.HullCriticalLevel.Value
                : percent <= Plugin.HullWarnLevel.Value ? Plugin.HullWarnLevel.Value
                : 2f;
            if (threshold < _lowestWarned)
            {
                _lowestWarned = threshold;
                bool critical = threshold <= Plugin.HullCriticalLevel.Value;
                player.Message(MessageHud.MessageType.Center,
                    critical ? $"<color=#ff5a40>Hull critical: {percent:P0}! Head for shore.</color>"
                             : $"<color=#f0b04a>Hull damaged: {percent:P0}</color>");
                _lastDamageMessage = Time.time;
            }
            else if (Time.time - _lastDamageMessage > DamageMessageCooldown)
            {
                player.Message(MessageHud.MessageType.TopLeft, $"Ship taking damage: hull {percent:P0}");
                _lastDamageMessage = Time.time;
            }
        }

        static readonly Color HealthyColor = new Color(0.55f, 0.85f, 0.45f);
        static readonly Color DamagedColor = new Color(0.94f, 0.69f, 0.29f);
        static readonly Color CriticalColor = new Color(1f, 0.38f, 0.28f);

        void OnGUI()
        {
            if (!Plugin.ShowHullReadout.Value || _hull == null || _lastPercent < 0f || Player.m_localPlayer == null) return;
            if (Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;

            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);

            const float w = 210f, h = 48f, pad = 12f;
            float x = (sw - w) * 0.5f;
            float y = Plugin.HullReadoutY.Value * (sh - h);

            bool critical = _lastPercent <= Plugin.HullCriticalLevel.Value;
            Color color = critical ? CriticalColor : _lastPercent <= Plugin.HullWarnLevel.Value ? DamagedColor : HealthyColor;
            // Flash brighter when hit, and pulse while critical.
            if (Time.time < _flashUntil) color = Color.Lerp(color, Color.white, 0.5f * (_flashUntil - Time.time) / 1.5f);
            if (critical) color.a = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 4f));

            Ui.Panel(new Rect(x, y, w, h));
            Ui.Label(new Rect(x + pad, y + 5f, w - pad * 2, 20f), "Hull", Ui.Text);
            Ui.Label(new Rect(x + pad, y + 5f, w - pad * 2, 20f),
                $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{_lastPercent * 100f:0}%</color></b>",
                _rightStyle ?? (_rightStyle = new GUIStyle(Ui.Text) { alignment = TextAnchor.MiddleRight }));
            Ui.Bar(new Rect(x + pad, y + 29f, w - pad * 2, 10f), _lastPercent, color);

            GUI.matrix = oldMatrix;
        }
    }
}
