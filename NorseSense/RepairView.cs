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
        // Two bands, not a gradient: on brown wood a red-to-yellow blend all reads as orange. Deeper than the
        // palette's red, since it's multiplied onto the piece's own colour. Weather (rain or water) is one blue.
        static readonly Color BadColor = new Color(0.62f, 0.03f, 0.03f);
        static readonly Color WornColor = new Color(1f, 0.92f, 0.12f);
        static readonly Color RainColor = new Color(0.35f, 0.62f, 1f);
        static readonly Color WaterColor = RainColor;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // Static so the highlight patch below can find a piece's tint.
        static readonly Dictionary<WearNTear, Color> _tinted = new Dictionary<WearNTear, Color>();
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

        // The game's build-mode highlight resets a piece's colour when you look away; put the tint back at once
        // instead of waiting for the next scan, so hovered pieces don't flicker.
        internal static void Retint(WearNTear wnt)
        {
            if (_tinted.TryGetValue(wnt, out Color color)) Tint(wnt, color);
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
            _legend.BarColor = Color.Lerp(Ui.BadColor, Ui.GoodColor, _legend.BarFraction);

            if (_damaged > 0 && _lowest < Badly) _legend.Add(BadColor, "Badly damaged");
            if (_damaged > 0) _legend.Add(WornColor, "Damaged");
            if (_rain + _water > 0)
                _legend.Add(RainColor, $"Rain or water wears it down <size=12><color={Ui.Muted}>(to half, {_rain + _water})</color></size>");
            if (EnvMan.IsWet() && _rain > 0)
                _legend.Add($"<color={Ui.Warn}>Raining now</color>");
            if (!string.IsNullOrEmpty(_materialsText))
                _legend.Add($"<size=13><color={Ui.Muted}>{_materialsText}</color></size>");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(WearNTear), "ResetHighlight")]
    static class RepairRetintPatch
    {
        static void Postfix(WearNTear __instance) => RepairView.Retint(__instance);
    }
}
