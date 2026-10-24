using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    internal class TableMarker
    {
        public long Owner;
        public string Name;
        public Vector3 Pos;
        public Minimap.PinType Type;
        public bool Checked;
        public string Author;

        public TableMarker Clone() => (TableMarker)MemberwiseClone();

        // How a marker is recognised across reads: the game itself matches markers by position.
        public bool SameMarker(TableMarker other) => Owner == other.Owner && (Pos - other.Pos).sqrMagnitude < 0.01f;

        public bool SameContent(TableMarker other) => Name == other.Name && Type == other.Type && Checked == other.Checked;
    }

    // A cartography table's data: format version, explored map (kept as raw bytes) and the marker list.
    // Layout mirrors Minimap.GetSharedMapData / AddSharedMapData.
    internal class TableData
    {
        static readonly Func<Minimap, ZPackage, global::Version.SharedMap, List<bool>> ReadExploredArray =
            AccessTools.MethodDelegate<Func<Minimap, ZPackage, global::Version.SharedMap, List<bool>>>(
                AccessTools.Method(typeof(Minimap), "ReadExploredArray"));

        public global::Version.SharedMap Version;
        public byte[] Header;
        public List<TableMarker> Markers = new List<TableMarker>();

        // Null if the data has no marker section or doesn't match this world's map size.
        public static TableData Parse(byte[] data, Minimap map)
        {
            if (data == null || map == null) return null;
            var pkg = new ZPackage(data);
            var table = new TableData { Version = (global::Version.SharedMap)pkg.ReadInt() };
            if (table.Version < global::Version.SharedMap.Pins) return null;
            if (ReadExploredArray(map, pkg, table.Version) == null) return null;

            int headerLength = pkg.GetPos();
            table.Header = new byte[headerLength];
            Buffer.BlockCopy(data, 0, table.Header, 0, headerLength);

            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                table.Markers.Add(new TableMarker
                {
                    Owner = pkg.ReadLong(),
                    Name = pkg.ReadString(),
                    Pos = pkg.ReadVector3(),
                    Type = (Minimap.PinType)pkg.ReadInt(),
                    Checked = pkg.ReadBool(),
                    Author = table.Version >= global::Version.SharedMap.PinsAuthor ? pkg.ReadString() : ""
                });
            }
            return table;
        }

        public static TableData Load(MapTable table, Minimap map)
        {
            var zdo = table.GetComponent<ZNetView>()?.GetZDO();
            byte[] compressed = zdo?.GetByteArray(ZDOVars.s_data);
            return compressed == null ? null : Parse(Utils.Decompress(compressed), map);
        }

        public byte[] Write()
        {
            var pkg = new ZPackage();
            pkg.Write(Markers.Count);
            foreach (var m in Markers)
            {
                pkg.Write(m.Owner);
                pkg.Write(m.Name);
                pkg.Write(m.Pos);
                pkg.Write((int)m.Type);
                pkg.Write(m.Checked);
                if (Version >= global::Version.SharedMap.PinsAuthor) pkg.Write(m.Author ?? "");
            }

            byte[] markers = pkg.GetArray();
            var result = new byte[Header.Length + markers.Length];
            Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
            Buffer.BlockCopy(markers, 0, result, Header.Length, markers.Length);
            return result;
        }

        // Same RPC the game uses when you write to a table; the table's owner stores it.
        public void Save(MapTable table)
        {
            var nview = table.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
                nview.InvokeRPC("MapData", new ZPackage(Utils.Compress(Write())));
        }
    }
}
