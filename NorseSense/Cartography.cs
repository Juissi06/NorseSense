using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    internal enum MarkerImport { All, Filtered, None }

    // Reading a cartography table normally imports every shared marker on it. This filters the table's
    // marker list before the game reads it, so skipped markers are never added (or are removed if they
    // came from an earlier read). Exploration (fog) is always imported in full. Your own markers are never touched.
    // It also refreshes shared markers you already have when their name, icon or crossed-out state changed,
    // which the game never does on its own.
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.AddSharedMapData))]
    static class TableImportPatch
    {
        static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> Pins =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        static int _skippedChecked, _skippedDuplicate, _skippedType, _skippedAll, _updated;

        // Set while writing to a table: the write rebuilds the table's marker list from your map,
        // so the read inside it must be unfiltered or other players' markers would be erased from the table.
        internal static bool Bypass;
        internal static bool Silent;

        static void Prefix(Minimap __instance, ref byte[] dataArray)
        {
            _skippedChecked = _skippedDuplicate = _skippedType = _skippedAll = _updated = 0;
            if (Player.m_localPlayer == null || dataArray == null) return;
            bool filter = !Bypass && Plugin.MarkerImportMode.Value != MarkerImport.All;
            bool update = Plugin.UpdateSharedMarkers.Value && !Compat.CartographyManagerDisabled;
            if (!filter && !update) return;

            try
            {
                var table = TableData.Parse(dataArray, __instance);
                if (table == null) return;
                if (filter && Filter(__instance, table)) dataArray = table.Write();
                if (update) RefreshChanged(__instance, table);
            }
            catch (Exception e)
            {
                // Unknown data layout (game update or another mod): fall back to vanilla import.
                Debug.LogWarning($"[Norse Sense] Couldn't process cartography table markers, importing as vanilla: {e.Message}");
            }
        }

        static void Postfix()
        {
            if (Silent || Player.m_localPlayer == null || !Plugin.ShowImportSummary.Value) return;

            var parts = new List<string>();
            if (_skippedAll > 0) parts.Add($"{_skippedAll} (markers off)");
            if (_skippedChecked > 0) parts.Add($"{_skippedChecked} crossed out");
            if (_skippedDuplicate > 0) parts.Add($"{_skippedDuplicate} duplicates");
            if (_skippedType > 0) parts.Add($"{_skippedType} filtered types");
            int skipped = _skippedChecked + _skippedDuplicate + _skippedType + _skippedAll;

            string text = skipped > 0 ? $"skipped {skipped} markers ({string.Join(", ", parts)})" : null;
            if (_updated > 0) text = (text == null ? "" : text + ", ") + $"updated {_updated}";
            if (text != null) Plugin.Tell("Map table", text);
        }

        // Returns true if anything was removed from the table's list.
        static bool Filter(Minimap map, TableData table)
        {
            long me = Player.m_localPlayer.GetPlayerID();
            bool none = Plugin.MarkerImportMode.Value == MarkerImport.None;
            var skippedTypes = ParseTypes(Plugin.SkipMarkerTypes.Value);
            float dupRadius = Plugin.DuplicateMergeDistance.Value;
            var existing = Pins(map);
            var kept = new List<TableMarker>(table.Markers.Count);

            foreach (var marker in table.Markers)
            {
                // The game ignores your own markers from the table; pass them through untouched.
                if (marker.Owner == me) { kept.Add(marker); continue; }

                if (none) { _skippedAll++; continue; }
                if (skippedTypes.Contains(marker.Type)) { _skippedType++; continue; }
                if (marker.Checked && Plugin.SkipCrossedOutMarkers.Value) { _skippedChecked++; continue; }
                if (dupRadius > 0f && IsDuplicate(marker, existing, kept, dupRadius)) { _skippedDuplicate++; continue; }
                kept.Add(marker);
            }

            if (kept.Count == table.Markers.Count) return false;
            table.Markers = kept;
            return true;
        }

        // The game keeps an existing marker as-is when one is already within 1 m. Remove stale copies
        // first so the game adds the table's current version instead.
        static void RefreshChanged(Minimap map, TableData table)
        {
            long me = Player.m_localPlayer.GetPlayerID();
            var existing = Pins(map);
            foreach (var marker in table.Markers)
            {
                if (marker.Owner == me) continue;
                for (int i = existing.Count - 1; i >= 0; i--)
                {
                    var pin = existing[i];
                    if (pin.m_ownerID == 0L || (pin.m_pos - marker.Pos).sqrMagnitude > 1f) continue;
                    if (pin.m_name != marker.Name || pin.m_type != marker.Type || pin.m_checked != marker.Checked)
                    {
                        map.RemovePin(pin);
                        _updated++;
                    }
                    break;
                }
            }
        }

        // Duplicate = same icon close to one of your own markers, or to a table marker already kept.
        // Other players' markers on your map aren't compared: they come from this same list and the game
        // removes any that aren't kept, so duplicates already on your map get cleaned up too.
        static bool IsDuplicate(TableMarker marker, List<Minimap.PinData> existing, List<TableMarker> kept, float radius)
        {
            float r2 = radius * radius;
            foreach (var other in existing)
                if (other.m_ownerID == 0L && other.m_save && other.m_type == marker.Type &&
                    (other.m_pos - marker.Pos).sqrMagnitude <= r2) return true;
            foreach (var other in kept)
                if (other.Type == marker.Type && (other.Pos - marker.Pos).sqrMagnitude <= r2) return true;
            return false;
        }

        internal static HashSet<Minimap.PinType> ParseTypes(string list)
        {
            var result = new HashSet<Minimap.PinType>();
            foreach (var raw in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "fire": result.Add(Minimap.PinType.Icon0); continue;
                    case "house": case "home": result.Add(Minimap.PinType.Icon1); continue;
                    case "hammer": result.Add(Minimap.PinType.Icon2); continue;
                    case "dot": case "circle": result.Add(Minimap.PinType.Icon3); continue;
                    case "rune": case "portal": result.Add(Minimap.PinType.Icon4); continue;
                }
                if (Enum.TryParse(raw.Trim(), true, out Minimap.PinType type)) result.Add(type);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(MapTable), "OnWrite")]
    static class TableWritePatch
    {
        static void Prefix() => TableImportPatch.Bypass = true;

        // The write imported every marker; filter your own map again so it stays tidy.
        static void Finalizer()
        {
            TableImportPatch.Bypass = false;
            if (Plugin.MarkerImportMode.Value == MarkerImport.All || Minimap.instance == null || Player.m_localPlayer == null) return;
            ReimportOwnMap();
        }

        internal static void ReimportOwnMap()
        {
            TableImportPatch.Silent = true;
            try
            {
                Minimap.instance.AddSharedMapData(Minimap.instance.GetSharedMapData(null));
            }
            finally
            {
                TableImportPatch.Silent = false;
            }
        }
    }
}
