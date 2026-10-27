using System.Collections.Generic;
using UnityEngine;

namespace NorseSense
{
    // Why you are or aren't sheltered: the same 17 cover rays as Cover.GetCoverForPoint, naming the open ones.
    internal static class ShelterInfo
    {
        const float RayDistance = 30f; // Cover.m_coverRayDistance
        const float MinDistance = 0.5f;
        const float NeedCover = 0.8f;  // Player.InShelter

        // Cover.CreateCoverRays. Its normalize loop works on copies, so the game casts these unnormalized.
        static readonly Vector3[] Rays =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
            new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f),
            new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 1f), new Vector3(0f, 1f, -1f),
            new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f)
        };

        static int _mask;
        static readonly List<string> Open = new List<string>();

        // Null when out in the open with nothing to explain, unless always is set.
        internal static InfoRow Describe(Player player, bool always)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

            Vector3 start = player.GetCenterPoint();
            bool roof = Cover.IsUnderRoof(start);
            if (!roof && !always) return null;

            var view = Quaternion.Euler(0f, -(GameCamera.instance != null ? GameCamera.instance.transform.eulerAngles.y : 0f), 0f);
            Open.Clear();
            int hits = 0;
            foreach (var ray in Rays)
            {
                if (Physics.Raycast(start + ray * MinDistance, ray, RayDistance - MinDistance, _mask)) hits++;
                else Open.Add(DirectionName(view * ray));
            }
            float cover = (float)hits / Rays.Length;
            string coverText = $"{cover * 100f:0}% covered, needs {NeedCover * 100f:0}%";
            if (cover >= NeedCover && roof)
                return new InfoRow("Shelter", "Yes", coverText, Tone.Good, Icon);

            string value = !roof ? "No roof above" : "Open " + OpenList();
            if (!roof && cover < NeedCover) value += ", open " + OpenList();
            return new InfoRow("Shelter", value, coverText, Tone.Warn, Icon);
        }

        static Sprite Icon => Ui.StatusIcon(SEMan.s_statusEffectShelter);

        // Short: "behind", "behind and to the left", or "on 4 sides" once it's more than two; the cover % next to
        // it says how far off it is.
        static string OpenList()
        {
            var sides = new List<string>();
            foreach (var o in Open) if (!sides.Contains(o)) sides.Add(o);
            if (sides.Count == 1) return sides[0];
            if (sides.Count == 2) return sides[0] + " and " + sides[1];
            return $"on {sides.Count} sides";
        }

        static readonly string[] Sides = { "ahead", "ahead right", "to the right", "behind right", "behind", "behind left", "to the left", "ahead left" };

        // Relative to where the camera looks, so "left" is your left. Rays angled up and level both count for
        // their side; only straight up is its own.
        static string DirectionName(Vector3 local)
        {
            if (Mathf.Abs(local.x) < 0.01f && Mathf.Abs(local.z) < 0.01f) return "above";
            float angle = Mathf.Repeat(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 360f);
            return Sides[Mathf.RoundToInt(angle / 45f) % 8];
        }
    }
}
