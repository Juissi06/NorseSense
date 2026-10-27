using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Harvest an area: hold the harvest key while picking a crop and every ripe crop of the same kind around it is
    // picked too. Each one goes through the game's own pick, exactly as if you pressed the key on it. With a
    // cultivator and seeds in your inventory, the crops picked this way are planted again, paid for like
    // planting them yourself (seed, stamina, cultivator durability, farming skill). A plain pick never replants,
    // so clearing a field stays possible.
    internal class MassFarming : MonoBehaviour
    {
        const float ReplantDelay = 0.4f; // the crop is removed by whoever owns it; plant once it's gone

        static readonly System.Func<Player, ItemDrop.ItemData, float> GetPlaceDurability =
            AccessTools.MethodDelegate<System.Func<Player, ItemDrop.ItemData, float>>(AccessTools.Method(typeof(Player), "GetPlaceDurability"));

        static MassFarming _instance;
        static bool _picking;
        // Grown crop prefab name -> the seed piece that grows it.
        static Dictionary<string, Piece> _seedFor;

        void Awake() => _instance = this;

        internal static bool KeyHeld() =>
            ServerLimits.Allowed(Plugin.HarvestArea) && Compat.AreaPlantingDisabledBy == null && Plugin.HarvestKey.Value != KeyCode.None &&
            BepInEx.UnityInput.Current.GetKey(Plugin.HarvestKey.Value);

        // After the game picked one: pick the same kind around it, then replant.
        internal static void OnPicked(Pickable picked, Player player)
        {
            if (_picking || _instance == null || player != Player.m_localPlayer) return;
            if (!KeyHeld()) return;
            bool area = true;
            bool replant = ServerLimits.Allowed(Plugin.Replant) && IsCrop(picked);

            string kind = Utils.GetPrefabName(picked.gameObject);
            var spots = new List<Vector3> { picked.transform.position };
            if (area)
            {
                float radius = ServerLimits.Max(Plugin.HarvestRadius);
                Vector3 center = picked.transform.position;
                var others = new List<Pickable>();
                foreach (var p in FindObjectsByType<Pickable>(FindObjectsSortMode.None))
                    if (p != picked && p.CanBePicked() && Utils.GetPrefabName(p.gameObject) == kind &&
                        Utils.DistanceXZ(p.transform.position, center) <= radius)
                        others.Add(p);
                others.Sort((a, b) => Utils.DistanceXZ(a.transform.position, center).CompareTo(Utils.DistanceXZ(b.transform.position, center)));

                _picking = true;
                try
                {
                    foreach (var p in others)
                    {
                        // The game's own pick, as if you pressed the key on it; wards and everything else apply.
                        if (!PrivateArea.CheckAccess(p.transform.position, 0f, false, false)) continue;
                        p.Interact(player, false, false);
                        spots.Add(p.transform.position);
                    }
                }
                finally
                {
                    _picking = false;
                }
            }

            if (replant) _instance.StartCoroutine(Replant(player, kind, spots));
        }

        // Crops disappear when picked (they don't grow back like berry bushes do).
        static bool IsCrop(Pickable p) => p.m_respawnTimeMinutes <= 0f && SeedFor(Utils.GetPrefabName(p.gameObject)) != null;

        static Piece SeedFor(string crop)
        {
            if (_seedFor == null)
            {
                _seedFor = new Dictionary<string, Piece>();
                if (ObjectDB.instance == null) return null;
                foreach (var item in ObjectDB.instance.m_items)
                {
                    var table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
                    if (table == null) continue;
                    foreach (var go in table.m_pieces)
                    {
                        var plant = go != null ? go.GetComponent<Plant>() : null;
                        if (plant == null) continue;
                        foreach (var grown in plant.m_grownPrefabs)
                            if (grown != null && !_seedFor.ContainsKey(grown.name)) _seedFor[grown.name] = go.GetComponent<Piece>();
                    }
                }
            }
            return _seedFor.TryGetValue(crop, out var piece) ? piece : null;
        }

        static IEnumerator Replant(Player player, string kind, List<Vector3> spots)
        {
            yield return new WaitForSeconds(ReplantDelay);
            var seed = SeedFor(kind);
            var cultivator = Cultivator(player, seed);
            if (seed == null || cultivator == null || player == null || player != Player.m_localPlayer) yield break;

            var plant = seed.GetComponent<Plant>();
            // Player.GetBuildStamina for the cultivator, with farming skill, and the sowing setting.
            float stamina = cultivator.m_shared.m_attack.m_attackStamina * ServerLimits.Min(Plugin.SowStaminaMultiplier);
            stamina -= stamina * 0.5f * player.GetSkillFactor(Skills.SkillType.Farming);
            bool free = player.PlacementCostDisabled;
            int planted = 0;
            string stopped = null;
            foreach (var spot in spots)
            {
                if (!free && !player.HaveRequirements(seed, Player.RequirementMode.CanBuild)) { stopped = "out of seeds"; break; }
                if (player.GetStamina() < stamina) { stopped = "out of stamina"; break; }
                if (cultivator.m_shared.m_useDurability && cultivator.m_durability <= 0f) { stopped = "cultivator broken"; break; }
                // Only where it would grow: same checks as the planting preview, minus "too close" to the crop
                // that is being picked there.
                if (!PrivateArea.CheckAccess(spot, 0f, false, false)) continue;
                Heightmap hm = Heightmap.FindHeightmap(spot);
                if (hm == null || (hm.GetBiome(spot) & plant.m_biome) == 0) continue;
                if (plant.m_needCultivatedGround && !hm.IsCultivated(spot)) continue;

                player.PlacePiece(seed, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), doAttack: false);
                if (!free && !ZoneSystem.instance.GetGlobalKey(seed.FreeBuildKey())) player.ConsumeResources(seed.m_resources, 0);
                player.UseStamina(stamina);
                player.RaiseSkill(Skills.SkillType.Farming);
                if (cultivator.m_shared.m_useDurability)
                    cultivator.m_durability -= GetPlaceDurability(player, cultivator) * Game.m_durabilityRate;
                planted++;
            }
            if (stopped != null)
                Plugin.Tell("Replant", $"{planted} of {spots.Count}, {stopped}", Tone.Warn);
        }

        // A cultivator in your inventory that can plant this seed.
        static ItemDrop.ItemData Cultivator(Player player, Piece seed)
        {
            if (player == null || seed == null) return null;
            foreach (var item in player.GetInventory().GetAllItems())
            {
                var table = item.m_shared.m_buildPieces;
                if (table == null) continue;
                foreach (var go in table.m_pieces)
                    if (go != null && go.GetComponent<Piece>() == seed) return item;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    static class MassFarmingPickPatch
    {
        static void Prefix(Pickable __instance, out bool __state) => __state = __instance.CanBePicked();

        static void Postfix(Pickable __instance, Humanoid character, bool repeat, bool __state)
        {
            // Only a real pick by you: not a repeat while holding the key, not one that did nothing.
            if (!__state || repeat || !(character is Player player)) return;
            MassFarming.OnPicked(__instance, player);
        }
    }
}
