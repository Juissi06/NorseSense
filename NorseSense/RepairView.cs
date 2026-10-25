using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NorseSense
{
    // Base view, Repairs mode: tints pieces by health and marks the ones the weather is wearing down,
    // plus a summary of what the base around you is built from. Wear rules from WearNTear.UpdateWear:
    // a piece with no roof above wears 5% a minute while it's wet (rain or water), but only down to 50%.
    internal class RepairView : MonoBehaviour
    {
        const float RefreshInterval = 1f;
        const int PiecesPerFrame = 200;
        const float Healthy = 0.995f;

        const float Badly = 0.5f;
        // Two bands, not a gradient: on brown wood a red-to-yellow blend all reads as orange.
        static readonly Color BadColor = new Color(0.62f, 0.03f, 0.03f);
        static readonly Color WornColor = new Color(1f, 0.92f, 0.12f);
        static readonly Color RainColor = new Color(0.35f, 0.62f, 1f);
        static readonly Color WaterColor = new Color(0.2f, 0.9f, 0.85f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly Dictionary<WearNTear, Color> _tinted = new Dictionary<WearNTear, Color>();
        readonly Dictionary<WearNTear, Color> _next = new Dictionary<WearNTear, Color>();
        readonly List<WearNTear> _batch = new List<WearNTear>();
        readonly Dictionary<string, int> _materials = new Dictionary<string, int>();
        readonly BaseView.Legend _legend = new BaseView.Legend();

        bool _active, _scanning;
        float _timer;

        // Results of the last finished scan.
        int _pieces, _damaged, _rain, _water;
        float _lowest = 1f;
        string _materialsText;

        void Update()
        {
            bool active = BaseView.Mode == ViewMode.Repairs && Player.m_localPlayer != null && MaterialMan.instance != null;
            if (active != _active)
            {
                _active = active;
                if (!active) Stop();
                else _timer = 0f;
            }
            if (!active || _scanning) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;
            StartCoroutine(Scan(Player.m_localPlayer.transform.position));
        }

        void OnDisable() => Stop();

        void Stop()
        {
            StopAllCoroutines();
            _scanning = false;
            foreach (var wnt in _tinted.Keys)
                if (wnt != null) Untint(wnt);
            _tinted.Clear();
        }

        IEnumerator Scan(Vector3 center)
        {
            _scanning = true;
            float range = Plugin.MaxDisplayDistance.Value;
            _batch.Clear();
            foreach (var wnt in WearNTear.GetAllInstances())
                if (wnt != null && Utils.DistanceXZ(wnt.transform.position, center) <= range) _batch.Add(wnt);

            _next.Clear();
            _materials.Clear();
            int pieces = 0, damaged = 0, rain = 0, water = 0;
            float lowest = 1f;

            for (int i = 0; i < _batch.Count; i++)
            {
                if (i > 0 && i % PiecesPerFrame == 0) yield return null;
                var wnt = _batch[i];
                if (wnt == null) continue;
                var piece = wnt.GetComponent<Piece>();
                if (piece == null) continue;
                pieces++;
                CountMaterials(piece);

                Vector3 pos = wnt.transform.position;
                float hp = wnt.GetHealthPercentage();
                bool shielded = ShieldGenerator.IsInsideShield(pos);
                WaterVolume volume = null;
                bool inWater = wnt.m_noRoofWear && !shielded && Floating.IsUnderWater(pos, ref volume);
                bool openToRain = wnt.m_noRoofWear && !shielded && !inWater &&
                                  !WearNTear.RoofCheck(wnt.transform, pos, out _, wnt.m_roofCheckOffset);

                Color tint;
                if (hp < Healthy)
                {
                    damaged++;
                    lowest = Mathf.Min(lowest, hp);
                    tint = hp < Badly ? BadColor : WornColor;
                }
                else if (inWater) tint = WaterColor;
                else if (openToRain) tint = RainColor;
                else continue;

                if (inWater) water++;
                else if (openToRain) rain++;
                _next[wnt] = tint;
            }

            // Re-apply every pass: the game's own highlight (hovering in build mode) resets the colour.
            foreach (var kv in _next)
                if (kv.Key != null) Tint(kv.Key, kv.Value);
            foreach (var wnt in _tinted.Keys)
                if (wnt != null && !_next.ContainsKey(wnt)) Untint(wnt);
            _tinted.Clear();
            foreach (var kv in _next) _tinted[kv.Key] = kv.Value;

            _pieces = pieces;
            _damaged = damaged;
            _rain = rain;
            _water = water;
            _lowest = lowest;
            _materialsText = string.Join(", ", _materials.OrderByDescending(m => m.Value).Take(3).Select(m => $"{m.Value} {m.Key}"));
            _scanning = false;
        }

        void CountMaterials(Piece piece)
        {
            foreach (var req in piece.m_resources)
            {
                if (req?.m_resItem == null || req.m_amount <= 0) continue;
                string name = Localization.instance.Localize(req.m_resItem.m_itemData.m_shared.m_name);
                _materials.TryGetValue(name, out int count);
                _materials[name] = count + req.m_amount;
            }
        }

        static void Tint(WearNTear wnt, Color color)
        {
            MaterialMan.instance.SetValue(wnt.gameObject, ColorId, color);
            MaterialMan.instance.SetValue(wnt.gameObject, EmissionId, color * 0.55f);
        }

        static void Untint(WearNTear wnt)
        {
            MaterialMan.instance.ResetValue(wnt.gameObject, ColorId);
            MaterialMan.instance.ResetValue(wnt.gameObject, EmissionId);
        }

        void OnGUI()
        {
            if (!_active || !BaseView.LegendVisible) return;
            if (Event.current.type == EventType.Layout) BuildLegend();
            BaseView.DrawLegend(_legend);
        }

        void BuildLegend()
        {
            _legend.Clear();
            int fine = _pieces - _damaged;
            _legend.BarFraction = _pieces > 0 ? (float)fine / _pieces : 1f;
            _legend.BarText = _damaged == 0
                ? $"All {_pieces} pieces at full health"
                : $"<b>{_damaged}</b> of {_pieces} need repair <size=12><color={Ui.Muted}>(lowest {_lowest * 100f:0}%)</color></size>";
            _legend.BarColor = Color.Lerp(BadColor, new Color(0.45f, 0.85f, 0.4f), _legend.BarFraction);

            _legend.Add(BadColor, "Under 50% health");
            _legend.Add(WornColor, "Damaged");
            _legend.Add(RainColor, $"No roof, rain wears to 50% <size=12><color={Ui.Muted}>({_rain})</color></size>");
            _legend.Add(WaterColor, $"In water, wears to 50% <size=12><color={Ui.Muted}>({_water})</color></size>");
            if (EnvMan.IsWet() && _rain > 0)
                _legend.Add($"<color={Ui.Warn}>Raining: {_rain} pieces wearing now</color>");
            if (!string.IsNullOrEmpty(_materialsText))
                _legend.Add($"<size=13><color={Ui.Muted}>{_materialsText}</color></size>");
        }
    }
}
