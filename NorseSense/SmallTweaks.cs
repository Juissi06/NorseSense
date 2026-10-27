using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Portal hover says where the portal leads (or why it's unconnected) and which carried items stop you from teleporting.
    [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
    static class PortalBlockersPatch
    {
        static readonly AccessTools.FieldRef<TeleportWorld, bool> AllowAllItems =
            AccessTools.FieldRefAccess<TeleportWorld, bool>("m_allowAllItems");

        static void Postfix(TeleportWorld __instance, ref string __result)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            if (Plugin.ShowPortalPairing.Value)
            {
                string pairing = PortalInfo.Describe(__instance);
                if (pairing != null) __result += "\n" + pairing;
            }
            if (!Plugin.ShowTeleportBlockers.Value || ZoneSystem.instance == null) return;

            // Same rules as Inventory.IsTeleportable.
            bool allowAll = AllowAllItems(__instance) || ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
            var blockers = player.GetInventory().GetAllItems()
                .Where(i => i.m_shared.m_toolTier >= 1000 || (!allowAll && !i.m_shared.m_teleportable))
                .GroupBy(i => i.m_shared.m_name)
                .Select(g => $"{Localization.instance.Localize(g.Key)} x{g.Sum(i => i.m_stack)}")
                .ToList();

            __result += blockers.Count == 0
                ? "\n<color=#9be07a>Your items can teleport</color>"
                : "\n<color=#ff6f55>Can't teleport: " + string.Join(", ", blockers) + "</color>";
        }
    }

    // Toggleable guard against removing pieces by accident while building.
    [HarmonyPatch(typeof(Player), "RemovePiece")]
    static class RemovalLockPatch
    {
        internal static bool Locked;
        static float _lastMessage;

        static bool Prefix(Player __instance, ref bool __result)
        {
            if (!Locked || __instance != Player.m_localPlayer) return true;

            if (Time.time - _lastMessage > 1f)
            {
                _lastMessage = Time.time;
                Plugin.Tell("Piece removal", $"locked, {Ui.KeyName(Plugin.RemovalLockKey.Value)} to unlock", Tone.Warn, true);
            }
            __result = false;
            return false;
        }
    }
}

namespace NorseSense
{
    // Fire hover says why a fire with fuel isn't burning or burns low (Fireplace.CheckUnderTerrain / CheckWet).
    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    static class FireStatusPatch
    {
        internal static readonly AccessTools.FieldRef<Fireplace, ZNetView> NView = AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");
        static readonly AccessTools.FieldRef<Fireplace, bool> Blocked = AccessTools.FieldRefAccess<Fireplace, bool>("m_blocked");
        static readonly AccessTools.FieldRef<Fireplace, bool> Wet = AccessTools.FieldRefAccess<Fireplace, bool>("m_wet");

        static void Postfix(Fireplace __instance, ref string __result)
        {
            if (!Plugin.ShowFireStatus.Value || string.IsNullOrEmpty(__result)) return;
            string problem = Problem(__instance, out Tone tone);
            if (problem != null) __result += $"\n<color={(tone == Tone.Bad ? Ui.Bad : Ui.Warn)}>{problem}</color>";
        }

        // Why a fire with fuel isn't burning or burns low; null when it's fine (or out of fuel, which is plain to see).
        internal static string Problem(Fireplace fire, out Tone tone)
        {
            tone = Tone.None;
            if (fire.m_infiniteFuel) return null;
            var nview = NView(fire);
            if (nview == null || !nview.IsValid()) return null;
            var zdo = nview.GetZDO();
            if (zdo.GetFloat(ZDOVars.s_fuel) <= 0f || zdo.GetInt(ZDOVars.s_state, 1) != 1) return null;

            if (Blocked(fire))
            {
                tone = Tone.Bad;
                Vector3 pos = fire.transform.position;
                bool buried = Heightmap.GetHeight(pos, out float h) && h > pos.y + fire.m_checkTerrainOffset;
                return buried
                    ? "Not burning: buried in the ground"
                    : "Not burning: something is too close above the flames, the smoke can't get out";
            }
            if (Wet(fire))
            {
                tone = Tone.Warn;
                bool rain = EnvMan.IsWet() &&
                            !Cover.IsUnderRoof(fire.transform.position + Vector3.up * fire.m_coverCheckOffset);
                string cause = rain ? "rain and no roof above" : "strong wind and not enough cover (needs 70%)";
                return fire.m_canTurnOff ? $"Goes out: {cause}" : $"Burning low: {cause}";
            }
            return null;
        }
    }
}
