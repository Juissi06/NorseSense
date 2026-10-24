using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace QolPack
{
    [BepInPlugin("juissi.qolpack", "QoL Pack", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> ShowSpawnRadius;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> MaxDisplayDistance;
        internal static ConfigEntry<float> BuildStaminaMultiplier;
        internal static ConfigEntry<float> SowStaminaMultiplier;

        const string MarkerName = "QoL_SpawnMarker";
        static Material _lineMat;
        float _timer;

        void Awake()
        {
            ShowSpawnRadius = Config.Bind("Spawn Display", "Enabled", false,
                "Show spawner trigger and spawn radius circles.");
            ToggleKey = Config.Bind("Spawn Display", "Toggle key", new KeyboardShortcut(KeyCode.F7),
                "Key that toggles the spawn radius display.");
            MaxDisplayDistance = Config.Bind("Spawn Display", "Max display distance", 100f,
                new ConfigDescription("Only show circles for spawners this close to you.",
                    new AcceptableValueRange<float>(20f, 500f)));

            BuildStaminaMultiplier = Config.Bind("Stamina", "Building multiplier", 0f,
                new ConfigDescription("Stamina cost multiplier while using the hammer. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));
            SowStaminaMultiplier = Config.Bind("Stamina", "Sowing multiplier", 0f,
                new ConfigDescription("Stamina cost multiplier while using the cultivator. 0 = free, 1 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));

            ShowSpawnRadius.SettingChanged += (s, e) => { if (!ShowSpawnRadius.Value) RemoveMarkers(); };

            new Harmony("juissi.qolpack").PatchAll();
        }

        void Update()
        {
            if (Player.m_localPlayer == null) return;

            if (ToggleKey.Value.IsDown() && !(Chat.instance != null && Chat.instance.HasFocus()))
                ShowSpawnRadius.Value = !ShowSpawnRadius.Value;

            if (!ShowSpawnRadius.Value) return;

            _timer += Time.deltaTime;
            if (_timer < 1.5f) return;
            _timer = 0f;
            RefreshMarkers();
        }

        void RefreshMarkers()
        {
            Vector3 playerPos = Player.m_localPlayer.transform.position;
            foreach (var area in FindObjectsOfType<SpawnArea>())
            {
                bool inRange = Vector3.Distance(area.transform.position, playerPos) <= MaxDisplayDistance.Value;
                Transform marker = area.transform.Find(MarkerName);
                if (marker == null)
                {
                    if (!inRange) continue;
                    marker = CreateMarker(area).transform;
                }
                marker.gameObject.SetActive(inRange);
            }
        }

        void RemoveMarkers()
        {
            foreach (var area in FindObjectsOfType<SpawnArea>())
            {
                Transform marker = area.transform.Find(MarkerName);
                if (marker != null) Destroy(marker.gameObject);
            }
        }

        static GameObject CreateMarker(SpawnArea area)
        {
            var root = new GameObject(MarkerName);
            root.transform.SetParent(area.transform, false);
            AddCircle(root.transform, "Trigger", area.m_triggerDistance, Color.yellow);
            AddCircle(root.transform, "Spawn", area.m_spawnRadius, Color.red);
            return root;
        }

        static void AddCircle(Transform parent, string name, float radius, Color color)
        {
            if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default"));

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = _lineMat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.startWidth = lr.endWidth = 0.2f;
            lr.startColor = lr.endColor = color;

            const int segments = 96;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.5f, Mathf.Sin(a) * radius));
            }
        }

        internal static float GetStaminaMultiplier(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return 1f;
            var item = player.GetRightItem();
            if (item == null) return 1f;

            switch (item.m_shared.m_name)
            {
                case "$item_hammer": return BuildStaminaMultiplier.Value;
                case "$item_cultivator": return SowStaminaMultiplier.Value;
                default: return 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
    static class UseStaminaPatch
    {
        static void Prefix(Player __instance, ref float v) => v *= Plugin.GetStaminaMultiplier(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveStamina))]
    static class HaveStaminaPatch
    {
        static void Prefix(Player __instance, ref float amount) => amount *= Plugin.GetStaminaMultiplier(__instance);
    }
}