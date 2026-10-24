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

        internal static string Describe(Piece piece)
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
            string color = support < min ? "#ff6f55" : support < min * 2f ? "#f0b04a" : "#ffffff";
            return $"{name}: support <color={color}><b>{support:0}</b></color> / {max:0}  <size=12>({material} falls below {min:0})</size>";
        }
    }
}
