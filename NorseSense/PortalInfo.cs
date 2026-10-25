using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Where a portal leads and why it isn't connected. The server pairs portals by tag (Game.ConnectPortals):
    // two portals with the same tag pair up, a third with that tag is paired at random or left over.
    internal static class PortalInfo
    {
        const float CacheTime = 0.5f;

        static readonly AccessTools.FieldRef<TeleportWorld, ZNetView> NView =
            AccessTools.FieldRefAccess<TeleportWorld, ZNetView>("m_nview");

        static TeleportWorld _cachedPortal;
        static float _cachedAt;
        static string _cached;

        internal static string Describe(TeleportWorld portal)
        {
            if (portal == _cachedPortal && Time.time - _cachedAt < CacheTime) return _cached;
            _cachedPortal = portal;
            _cachedAt = Time.time;
            _cached = Build(portal);
            return _cached;
        }

        static string Build(TeleportWorld portal)
        {
            var nview = NView(portal);
            if (nview == null || !nview.IsValid() || ZDOMan.instance == null) return null;
            ZDO zdo = nview.GetZDO();
            string tag = zdo.GetString(ZDOVars.s_tag);
            ZDOID target = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);

            // Clients only know the portals near them; the host knows them all.
            bool complete = ZNet.instance != null && ZNet.instance.IsServer();
            int sameTag = 0, freeSameTag = 0;
            foreach (ZDO other in ZDOMan.instance.GetPortalList())
            {
                if (other == zdo || other.GetString(ZDOVars.s_tag) != tag) continue;
                sameTag++;
                if (other.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal).IsNone()) freeSameTag++;
            }
            string crowded = sameTag >= 2
                ? $"\n<color={Ui.Warn}>{sameTag + 1}{(complete ? "" : "+")} portals share this tag, only two can pair. Give each pair its own tag.</color>"
                : "";

            if (!target.IsNone())
            {
                ZDO other = ZDOMan.instance.GetZDO(target);
                if (other == null)
                {
                    ZDOMan.instance.RequestZDO(target); // same as TeleportWorld.TargetFound; the answer arrives shortly
                    return $"<color={Ui.Muted}>Leads to: locating...</color>" + crowded;
                }
                return $"Leads to: {Where(portal.transform.position, other.GetPosition())}" + crowded;
            }

            string unpaired;
            if (sameTag == 0)
                unpaired = complete
                    ? "No other portal has this tag. Build one with the same tag."
                    : "Needs exactly one other portal with this tag.";
            else if (freeSameTag == 0)
                unpaired = "Every other portal with this tag is already paired.";
            else
                unpaired = "A free match exists, the server pairs it within a few seconds.";
            return $"<color={Ui.Warn}>{unpaired}</color>" + crowded;
        }

        static readonly string[] Compass = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        // "1.4 km north-east, Swamp". North is +z on the map.
        static string Where(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float dist = d.magnitude;
            string distance = dist >= 1000f ? $"{dist / 1000f:0.0} km" : $"{dist:0} m";
            string dir = dist < 1f ? "" : " " + Compass[Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f) / 45f) % 8];
            string biome = WorldGenerator.instance != null
                ? ", " + Localization.instance.Localize("$biome_" + WorldGenerator.instance.GetBiome(to).ToString().ToLower())
                : "";
            return $"<b>{distance}</b>{dir}{biome}";
        }
    }
}
