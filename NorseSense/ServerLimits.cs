using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace NorseSense
{
    // Server limits: a host or dedicated server with Norse Sense can cap the settings that affect balance for every
    // player who has the mod. Players can still choose less (a shorter freecam range, vanilla stamina), never more.
    // Players without the mod and vanilla servers are unaffected: a client says hello to the server, and only a
    // server that has the mod answers. The readouts never depend on this.
    internal class ServerLimits : MonoBehaviour
    {
        const string HelloRpc = "NorseSense_Hello";
        const string LimitsRpc = "NorseSense_Limits";

        // The settings a server can cap, by config key. Numbers cap from above, except stamina multipliers, which
        // cap from below (lower is cheaper); switches can only be turned off.
        static ConfigEntryBase[] Capped => new ConfigEntryBase[]
        {
            Plugin.FreecamAllowed, Plugin.FreecamThroughWalls, Plugin.FreecamRange, Plugin.FreecamReach,
            Plugin.AreaToolsEnabled, Plugin.AreaTerrain, Plugin.MaxPlantGrid, Plugin.MaxHoeSize,
            Plugin.BuildStaminaMultiplier, Plugin.SowStaminaMultiplier, Plugin.BuildDistance, Plugin.PickupRange,
            Plugin.HarvestArea, Plugin.HarvestRadius, Plugin.Replant
        };

        // The server's values while connected to one that sets limits; empty otherwise.
        static readonly Dictionary<string, string> Server = new Dictionary<string, string>();
        static readonly HashSet<long> Listeners = new HashSet<long>();
        static ZRoutedRpc _registeredOn;
        static bool _helloSent;

        internal static bool Active => Server.Count > 0;

        void Awake()
        {
            Plugin.LimitPlayers.SettingChanged += (s, e) => Broadcast();
            Plugin.FreecamAllowed.SettingChanged += (s, e) => Broadcast();
            Plugin.FreecamThroughWalls.SettingChanged += (s, e) => Broadcast();
            Plugin.FreecamRange.SettingChanged += (s, e) => Broadcast();
            Plugin.FreecamReach.SettingChanged += (s, e) => Broadcast();
            Plugin.AreaToolsEnabled.SettingChanged += (s, e) => Broadcast();
            Plugin.AreaTerrain.SettingChanged += (s, e) => Broadcast();
            Plugin.MaxPlantGrid.SettingChanged += (s, e) => Broadcast();
            Plugin.MaxHoeSize.SettingChanged += (s, e) => Broadcast();
            Plugin.BuildStaminaMultiplier.SettingChanged += (s, e) => Broadcast();
            Plugin.SowStaminaMultiplier.SettingChanged += (s, e) => Broadcast();
            Plugin.BuildDistance.SettingChanged += (s, e) => Broadcast();
            Plugin.PickupRange.SettingChanged += (s, e) => Broadcast();
            Plugin.HarvestArea.SettingChanged += (s, e) => Broadcast();
            Plugin.HarvestRadius.SettingChanged += (s, e) => Broadcast();
            Plugin.Replant.SettingChanged += (s, e) => Broadcast();
        }

        void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || ZNet.instance == null)
            {
                if (_registeredOn != null) Reset();
                return;
            }
            if (rpc != _registeredOn)
            {
                Reset();
                _registeredOn = rpc;
                rpc.Register<ZPackage>(HelloRpc, OnHello);
                rpc.Register<ZPackage>(LimitsRpc, OnLimits);
            }
            if (!_helloSent && !ZNet.instance.IsServer() && Player.m_localPlayer != null)
            {
                _helloSent = true;
                var pkg = new ZPackage();
                pkg.Write(1); // protocol version
                rpc.InvokeRoutedRPC(HelloRpc, pkg);
            }
        }

        static void Reset()
        {
            bool had = Server.Count > 0;
            Server.Clear();
            Listeners.Clear();
            _registeredOn = null;
            _helloSent = false;
            if (had) PlayerTweaks.Apply();
        }

        // ---------- Server side ----------

        static void OnHello(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            Listeners.Add(sender);
            Send(sender);
        }

        static void Broadcast()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
            foreach (long peer in Listeners) Send(peer);
        }

        // Empty when limits are off, which also lifts limits a client got earlier.
        static void Send(long peer)
        {
            var pkg = new ZPackage();
            pkg.Write(1);
            var entries = Plugin.LimitPlayers.Value ? Capped : new ConfigEntryBase[0];
            pkg.Write(entries.Length);
            foreach (var entry in entries)
            {
                pkg.Write(entry.Definition.Key);
                pkg.Write(entry.GetSerializedValue());
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, LimitsRpc, pkg);
        }

        // ---------- Client side ----------

        static void OnLimits(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer()) return;
            var server = ZNet.instance.GetServerPeer();
            if (server == null || sender != server.m_uid) return; // only the server sets limits

            pkg.ReadInt(); // protocol version
            int count = pkg.ReadInt();
            Server.Clear();
            for (int i = 0; i < count; i++) Server[pkg.ReadString()] = pkg.ReadString();
            PlayerTweaks.Apply();
            if (Freecam.Active && !Allowed(Plugin.FreecamAllowed)) Freecam.Exit("not allowed on this server");
            Debug.Log("[Norse Sense] " + (Server.Count > 0 ? $"Server limits: {Describe()}" : "No server limits."));
            if (Server.Count > 0) Plugin.Tell("Server limits", "active, see the log for values");
        }

        static string Describe()
        {
            var parts = new List<string>();
            foreach (var kv in Server) parts.Add($"{kv.Key} {kv.Value}");
            return string.Join(", ", parts);
        }

        // ---------- Effective values ----------

        static bool TryServer<T>(ConfigEntry<T> entry, out T value)
        {
            value = default(T);
            if (!Server.TryGetValue(entry.Definition.Key, out string text)) return false;
            try
            {
                value = (T)TomlTypeConverter.ConvertToValue(text, typeof(T));
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // At most the server's value.
        internal static float Max(ConfigEntry<float> entry) =>
            TryServer(entry, out float s) ? Mathf.Min(entry.Value, s) : entry.Value;

        internal static int Max(ConfigEntry<int> entry) =>
            TryServer(entry, out int s) ? Mathf.Min(entry.Value, s) : entry.Value;

        // At least the server's value: stamina multipliers, where lower is cheaper.
        internal static float Min(ConfigEntry<float> entry) =>
            TryServer(entry, out float s) ? Mathf.Max(entry.Value, s) : entry.Value;

        // On only if the server allows it too.
        internal static bool Allowed(ConfigEntry<bool> entry) =>
            entry.Value && (!TryServer(entry, out bool s) || s);

        // Build distance and pickup range: 0 means vanilla, which is the least; anything else is capped.
        internal static float MaxOrVanilla(ConfigEntry<float> entry)
        {
            if (!TryServer(entry, out float s)) return entry.Value;
            if (s <= 0f || entry.Value <= 0f) return 0f;
            return Mathf.Min(entry.Value, s);
        }
    }
}
