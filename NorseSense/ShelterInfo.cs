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
        internal static string Describe(Player player, bool always)
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
            if (cover >= NeedCover && roof)
                return $"Shelter: <color=#9be07a>yes</color> <size=12>(cover {cover * 100f:0}%)</size>";

            var reasons = new List<string>(2);
            if (!roof) reasons.Add("no roof above");
            if (cover < NeedCover) reasons.Add($"cover {cover * 100f:0}% of {NeedCover * 100f:0}%, open {OpenList()}");
            return $"Shelter: <color=#f0b04a>no</color>, {string.Join("; ", reasons)}";
        }

        static string OpenList() =>
            Open.Count <= 6 ? string.Join(", ", Open) : string.Join(", ", Open.GetRange(0, 6)) + $" +{Open.Count - 6} more";

        static readonly string[] Sides = { "ahead", "ahead-right", "right", "behind-right", "behind", "behind-left", "left", "ahead-left" };

        // Relative to where the camera looks, so "left" is your left.
        static string DirectionName(Vector3 local)
        {
            if (Mathf.Abs(local.x) < 0.01f && Mathf.Abs(local.z) < 0.01f) return "straight up";
            float angle = Mathf.Repeat(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 360f);
            string side = Sides[Mathf.RoundToInt(angle / 45f) % 8];
            return local.y > 0f ? side + " (up)" : side;
        }
    }
}
