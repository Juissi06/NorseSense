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
                ? "\n<color=#80ff80>Your items can teleport</color>"
                : "\n<color=#ff7050>Can't teleport: " + string.Join(", ", blockers) + "</color>";
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
