using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Portal hover lists exactly which carried items stop you from teleporting.
    [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
    static class PortalBlockersPatch
    {
        static readonly AccessTools.FieldRef<TeleportWorld, bool> AllowAllItems =
            AccessTools.FieldRefAccess<TeleportWorld, bool>("m_allowAllItems");

        static void Postfix(TeleportWorld __instance, ref string __result)
        {
            var player = Player.m_localPlayer;
            if (!Plugin.ShowTeleportBlockers.Value || player == null || ZoneSystem.instance == null) return;

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
                __instance.Message(MessageHud.MessageType.Center,
                    $"Piece removal is locked ({Plugin.RemovalLockKey.Value} to unlock)");
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
        static readonly AccessTools.FieldRef<Fireplace, ZNetView> NView = AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");
        static readonly AccessTools.FieldRef<Fireplace, bool> Blocked = AccessTools.FieldRefAccess<Fireplace, bool>("m_blocked");
        static readonly AccessTools.FieldRef<Fireplace, bool> Wet = AccessTools.FieldRefAccess<Fireplace, bool>("m_wet");

        static void Postfix(Fireplace __instance, ref string __result)
        {
            if (!Plugin.ShowFireStatus.Value || __instance.m_infiniteFuel || string.IsNullOrEmpty(__result)) return;
            var nview = NView(__instance);
            if (nview == null || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            if (zdo.GetFloat(ZDOVars.s_fuel) <= 0f || zdo.GetInt(ZDOVars.s_state, 1) != 1) return;

            string status = null;
            if (Blocked(__instance))
            {
                Vector3 pos = __instance.transform.position;
                bool buried = Heightmap.GetHeight(pos, out float h) && h > pos.y + __instance.m_checkTerrainOffset;
                status = buried
                    ? "<color=#ff6f55>Not burning: buried in the ground</color>"
                    : "<color=#ff6f55>Not burning: something is too close above the flames, the smoke can't get out</color>";
            }
            else if (Wet(__instance))
            {
                bool rain = EnvMan.IsWet() &&
                            !Cover.IsUnderRoof(__instance.transform.position + Vector3.up * __instance.m_coverCheckOffset);
                string cause = rain ? "rain and no roof above" : "strong wind and not enough cover (needs 70%)";
                status = __instance.m_canTurnOff
                    ? $"<color=#f0b04a>Goes out: {cause}</color>"
                    : $"<color=#f0b04a>Burning low: {cause}</color>";
            }
            if (status != null) __result += "\n" + status;
        }
    }
}
