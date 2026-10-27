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
        static readonly List<int> Open = new List<int>(); // side index, see Sides; Above for straight up
        const int Above = 8;

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
                else Open.Add(Direction(view * ray));
            }
            float cover = (float)hits / Rays.Length;
            string coverText = $"{cover * 100f:0}% covered, needs {NeedCover * 100f:0}%";
            if (cover >= NeedCover && roof)
                return new InfoRow("Shelter", "Yes", coverText, Tone.Good, Icon);

            // A small compass shows the open sides; words only when it's one side, which reads quicker.
            int compass = 0;
            foreach (int side in Open) compass |= 1 << side;
            var sides = new List<int>();
            foreach (int side in Open) if (side != Above && !sides.Contains(side)) sides.Add(side);
            string value = !roof ? "No roof above" : sides.Count == 1 ? "Open " + Sides[sides[0]] : "Open";
            // In words, for places without the compass (the comfort card).
            Summary = !roof ? "no roof above" : sides.Count == 0 ? "open" :
                sides.Count <= 2 ? "open " + string.Join(" and ", sides.ConvertAll(s => Sides[s])) : $"open on {sides.Count} sides";
            return new InfoRow("Shelter", value, coverText, Tone.Warn, Icon) { Compass = compass };
        }

        // The last answer in words, e.g. "open behind and to the left".
        internal static string Summary;

        static Sprite Icon => Ui.StatusIcon(SEMan.s_statusEffectShelter);

        static readonly string[] Sides = { "ahead", "ahead right", "to the right", "behind right", "behind", "behind left", "to the left", "ahead left" };

        // Relative to where the camera looks, so "left" is your left: 0 = ahead, clockwise in eighths. Rays angled
        // up and level both count for their side; only straight up is its own.
        static int Direction(Vector3 local)
        {
            if (Mathf.Abs(local.x) < 0.01f && Mathf.Abs(local.z) < 0.01f) return Above;
            float angle = Mathf.Repeat(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 360f);
            return Mathf.RoundToInt(angle / 45f) % 8;
        }
    }
}
