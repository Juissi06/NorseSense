using System;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Exact structural support of the piece you look at in build mode (not grounded ones). The game only tints it blue to red.
    internal static class SupportInfo
    {
        // Null if a game update renamed them; the readout then stays off.
        static readonly Func<WearNTear, float> GetSupport = Create("GetSupport");
        static readonly Func<WearNTear, float> GetMaxSupport = Create("GetMaxSupport");
        static readonly Func<WearNTear, float> GetMinSupport = Create("GetMinSupport");

        static Func<WearNTear, float> Create(string name)
        {
            try
            {
                return AccessTools.MethodDelegate<Func<WearNTear, float>>(AccessTools.Method(typeof(WearNTear), name));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Norse Sense] Support readout unavailable ({name}): {e.Message}");
                return null;
            }
        }

        internal static InfoRow Describe(Piece piece)
        {
            if (piece == null || GetSupport == null || GetMaxSupport == null || GetMinSupport == null) return null;
            var wnt = piece.GetComponent<WearNTear>();
            if (wnt == null || !wnt.m_supports) return null;

            float max = GetMaxSupport(wnt);
            if (max <= 0f) return null;
            float support = GetSupport(wnt);
            float min = GetMinSupport(wnt);
            string name = Localization.instance.Localize(piece.m_name);
            string material = wnt.m_materialType.ToString().ToLowerInvariant();

            if (support >= max) return null; // grounded: nothing to worry about, keep the screen clear
            Tone tone = support < min ? Tone.Bad : support < min * 2f ? Tone.Warn : Tone.None;
            // The same scale and colours as the game's own support tint (WearNTear.GetSupportColorValue).
            float shade = Mathf.Clamp01((support - min) / (max * 0.5f - min));
            return new InfoRow("Support", $"<b>{support:0}</b> / {max:0}", $"{name}, {material} falls below {min:0}", tone, piece.m_icon)
            {
                Bar = shade,
                BarColor = Color.Lerp(new Color(1f, 0f, 0f), new Color(0f, 1f, 0f), shade)
            };
        }
    }
}
