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

        GUIStyle _style;

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
                    critical ? $"<color=#ff4030>Hull critical: {percent:P0}! Head for shore.</color>"
                             : $"<color=#ffa030>Hull damaged: {percent:P0}</color>");
                _lastDamageMessage = Time.time;
            }
            else if (Time.time - _lastDamageMessage > DamageMessageCooldown)
            {
                player.Message(MessageHud.MessageType.TopLeft, $"Ship taking damage: hull {percent:P0}");
                _lastDamageMessage = Time.time;
            }
        }

        void OnGUI()
        {
            if (!Plugin.ShowHullReadout.Value || _hull == null || _lastPercent < 0f || Player.m_localPlayer == null) return;
            if (Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = Color.white;
            }

            float scale = Screen.height / 1080f * Plugin.LegendScale.Value;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float w = 150f, h = 30f;
            float x = (Screen.width / scale - w) * 0.5f;
            float y = Plugin.HullReadoutY.Value * (Screen.height / scale - h);

            string color = _lastPercent <= Plugin.HullCriticalLevel.Value ? "#ff4030"
                : _lastPercent <= Plugin.HullWarnLevel.Value ? "#ffa030"
                : Time.time < _flashUntil ? "#ffd080" : "#ffffff";
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Label(new Rect(x, y, w, h), $"Hull <b><color={color}>{_lastPercent:P0}</color></b>", _style);

            GUI.matrix = oldMatrix;
        }
    }
}
