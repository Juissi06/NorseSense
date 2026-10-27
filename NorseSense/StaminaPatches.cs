using HarmonyLib;

namespace NorseSense
{
    static class StaminaPatches
    {
        // Humanoid.GetRightItem() is protected, so read the field directly.
        internal static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> RightItem =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_rightItem");

        internal static float GetToolMultiplier(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return 1f;
            var item = RightItem(player);
            if (item == null) return 1f;

            switch (item.m_shared.m_name)
            {
                case "$item_hammer": return ServerLimits.Min(Plugin.BuildStaminaMultiplier);
                case "$item_cultivator": return ServerLimits.Min(Plugin.SowStaminaMultiplier);
                default: return 1f;
            }
        }
    }

    // Placing, removing and repairing all charge GetBuildStamina(), so this only touches build actions.
    [HarmonyPatch(typeof(Player), "GetBuildStamina")]
    static class BuildStaminaPatch
    {
        static void Postfix(Player __instance, ref float __result) => __result *= StaminaPatches.GetToolMultiplier(__instance);
    }

    // UpdatePlacement checks HaveStamina(tool attack stamina) before each action.
    // Scale that check only while inside UpdatePlacement, so running/jumping/dodging stay vanilla.
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    static class UpdatePlacementPatch
    {
        internal static bool Active;

        static void Prefix(Player __instance) => Active = __instance == Player.m_localPlayer;
        static void Finalizer() => Active = false;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveStamina))]
    static class HaveStaminaPatch
    {
        static void Prefix(Player __instance, ref float amount)
        {
            if (UpdatePlacementPatch.Active) amount *= StaminaPatches.GetToolMultiplier(__instance);
        }

        static void Postfix(Player __instance, ref bool __result)
        {
            if (UpdatePlacementPatch.Active && !__result && StaminaPatches.GetToolMultiplier(__instance) <= 0f)
                __result = true;
        }
    }
}
