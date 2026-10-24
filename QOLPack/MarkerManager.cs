using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace QolPack
{
    internal enum EditPermission { BuilderOrWard, AdminsOnly }

    // Window for viewing and editing the markers stored on a cartography table (modifier + E at the table).
    // Edits are kept locally until Save, then merged into the table's *current* data, so changes someone
    // else saved in the meantime aren't overwritten.
    internal class MarkerManager : MonoBehaviour
    {
        internal static MarkerManager Instance;
        internal static bool IsOpen => Instance != null && Instance._table != null;

        const float RowH = 28f;
        static readonly Minimap.PinType[] EditableIcons =
        {
            Minimap.PinType.Icon0, Minimap.PinType.Icon1, Minimap.PinType.Icon2, Minimap.PinType.Icon3, Minimap.PinType.Icon4
        };

        static readonly Func<Minimap, Minimap.PinType, Sprite> GetSprite =
            AccessTools.MethodDelegate<Func<Minimap, Minimap.PinType, Sprite>>(AccessTools.Method(typeof(Minimap), "GetSprite"));
        static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> Pins =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
        static readonly AccessTools.FieldRef<List<PrivateArea>> AllWards =
            AccessTools.StaticFieldRefAccess<List<PrivateArea>>(AccessTools.Field(typeof(PrivateArea), "m_allAreas"));
        static readonly Func<PrivateArea, bool> WardEnabled =
            AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "IsEnabled"));
        static readonly Func<PrivateArea, Vector3, float, bool> WardIsInside =
            AccessTools.MethodDelegate<Func<PrivateArea, Vector3, float, bool>>(AccessTools.Method(typeof(PrivateArea), "IsInside"));
        static readonly Func<PrivateArea, bool> WardHasAccess =
            AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "HaveLocalAccess"));

        // Names of players seen this session, so markers can show who placed them.
        static readonly Dictionary<long, string> KnownNames = new Dictionary<long, string>();

        class Row
        {
            public TableMarker Original;
            public TableMarker Edit;
            public bool Deleted;
            public bool Selected;
            public bool Duplicate;
            public int Group;
            public string Owner;
            public float Distance;
            public bool Dirty => Deleted || !Edit.SameContent(Original);
        }

        enum Sort { Name, Distance, Owner, Icon }

        MapTable _table;
        bool _canEdit;
        string _whyReadOnly;
        long _me;
        readonly List<Row> _rows = new List<Row>();
        List<Row> _shown = new List<Row>();
        bool _filterDirty = true;

        string _search = "";
        readonly HashSet<Minimap.PinType> _hiddenTypes = new HashSet<Minimap.PinType>();
        bool _onlyDuplicates, _onlyCrossed, _onlyMine;
        Sort _sort = Sort.Name;
        Vector2 _scroll;
        bool _confirmDiscard;
        string _status = "";
        Rect _window;

        GUIStyle _label, _header, _small, _rowStyle;

        void Awake() => Instance = this;

        // ---------- Opening and permissions ----------

        internal void Open(MapTable table)
        {
            var player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;

            _table = table;
            _me = player.GetPlayerID();
            _canEdit = CheckPermission(table, out _whyReadOnly);
            _search = "";
            _hiddenTypes.Clear();
            _onlyDuplicates = _onlyCrossed = _onlyMine = false;
            _scroll = Vector2.zero;
            _confirmDiscard = false;
            Reload();
            _status = _rows.Count == 0 ? "This table has no markers yet." : "";
        }

        internal void Close()
        {
            _table = null;
            _rows.Clear();
            _shown.Clear();
        }

        static bool CheckPermission(MapTable table, out string why)
        {
            why = null;
            if (ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost()) return true;
            if (Plugin.ManagerPermission.Value == EditPermission.AdminsOnly)
            {
                why = "Only admins can edit tables on this setting.";
                return false;
            }

            var piece = table.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == Player.m_localPlayer.GetPlayerID()) return true;

            Vector3 pos = table.transform.position;
            foreach (var ward in AllWards())
                if (ward != null && WardEnabled(ward) && WardIsInside(ward, pos, 0f) && WardHasAccess(ward)) return true;

            why = "You can view this table. To edit, you must have built it or be permitted on a ward covering it.";
            return false;
        }

        void Reload()
        {
            _rows.Clear();
            var data = TableData.Load(_table, Minimap.instance);
            if (data != null)
            {
                RememberNames();
                Vector3 tablePos = _table.transform.position;
                foreach (var marker in data.Markers)
                {
                    _rows.Add(new Row
                    {
                        Original = marker,
                        Edit = marker.Clone(),
                        Owner = OwnerName(marker.Owner),
                        Distance = Utils.DistanceXZ(marker.Pos, tablePos)
                    });
                }
            }
            _filterDirty = true;
        }

        static void RememberNames()
        {
            foreach (var p in Player.GetAllPlayers())
                if (p != null) KnownNames[p.GetPlayerID()] = p.GetPlayerName();
        }

        string OwnerName(long owner)
        {
            if (owner == _me || owner == 0L) return "You";
            return KnownNames.TryGetValue(owner, out var name) ? name : $"Player #{Math.Abs(owner) % 10000:0000}";
        }

        void Update()
        {
            if (_table == null) return;
            // Walked away, table destroyed, died or logged out.
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, _table.transform.position) > 6f)
                Close();
        }

        // ---------- Filtering and duplicates ----------

        void RefreshShown()
        {
            float dup = Mathf.Max(1f, Plugin.DuplicateMergeDistance.Value);
            float d2 = dup * dup;
            foreach (var r in _rows) { r.Duplicate = false; r.Group = -1; }
            int group = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var a = _rows[i];
                if (a.Deleted) continue;
                for (int j = i + 1; j < _rows.Count; j++)
                {
                    var b = _rows[j];
                    if (b.Deleted || a.Edit.Type != b.Edit.Type || (a.Edit.Pos - b.Edit.Pos).sqrMagnitude > d2) continue;
                    if (a.Group < 0) a.Group = group++;
                    b.Group = a.Group;
                    a.Duplicate = b.Duplicate = true;
                }
            }

            string search = _search.Trim().ToLowerInvariant();
            IEnumerable<Row> rows = _rows.Where(r =>
                !_hiddenTypes.Contains(r.Edit.Type) &&
                (!_onlyDuplicates || r.Duplicate) &&
                (!_onlyCrossed || r.Edit.Checked) &&
                (!_onlyMine || r.Original.Owner == _me) &&
                (search.Length == 0 || (r.Edit.Name ?? "").ToLowerInvariant().Contains(search) || r.Owner.ToLowerInvariant().Contains(search)));

            if (_onlyDuplicates) rows = rows.OrderBy(r => r.Group).ThenBy(r => r.Original.Owner == _me ? 0 : 1);
            else
            {
                switch (_sort)
                {
                    case Sort.Distance: rows = rows.OrderBy(r => r.Distance); break;
                    case Sort.Owner: rows = rows.OrderBy(r => r.Owner).ThenBy(r => r.Edit.Name); break;
                    case Sort.Icon: rows = rows.OrderBy(r => r.Edit.Type).ThenBy(r => r.Edit.Name); break;
                    default: rows = rows.OrderBy(r => string.IsNullOrEmpty(r.Edit.Name) ? 1 : 0).ThenBy(r => r.Edit.Name); break;
                }
            }
            _shown = rows.ToList();
            _filterDirty = false;
        }

        // ---------- Saving ----------

        void Save()
        {
            var map = Minimap.instance;
            var fresh = TableData.Load(_table, map);
            if (fresh == null)
            {
                _status = "Couldn't read the table, nothing saved.";
                return;
            }

            int changed = 0, missing = 0;
            foreach (var row in _rows.Where(r => r.Dirty))
            {
                var target = fresh.Markers.FirstOrDefault(m => m.SameMarker(row.Original));
                if (target == null) { missing++; continue; }
                if (row.Deleted) fresh.Markers.Remove(target);
                else
                {
                    target.Name = row.Edit.Name;
                    target.Type = row.Edit.Type;
                    target.Checked = row.Edit.Checked;
                }
                changed++;
            }

            if (changed > 0)
            {
                fresh.Save(_table);
                ApplyToOwnMarkers(map);
                // Bring the edits onto your own map as well (shared markers are refreshed by the import patch).
                map.AddSharedMapData(fresh.Write());
            }

            _status = $"Saved {changed} change(s) to the table." +
                      (missing > 0 ? $" {missing} marker(s) were already removed by someone else." : "");
            Reload();
        }

        // Your own markers live on your map with owner 0; mirror edits to them so the table and your map agree.
        void ApplyToOwnMarkers(Minimap map)
        {
            var pins = Pins(map);
            foreach (var row in _rows.Where(r => r.Dirty && r.Original.Owner == _me))
            {
                var pin = pins.FirstOrDefault(p => p.m_ownerID == 0L && (p.m_pos - row.Original.Pos).sqrMagnitude <= 1f);
                if (pin == null) continue;
                map.RemovePin(pin);
                if (!row.Deleted) map.AddPin(row.Edit.Pos, row.Edit.Type, row.Edit.Name, true, row.Edit.Checked);
            }
        }

        bool AnyDirty => _rows.Any(r => r.Dirty);

        void RequestClose()
        {
            if (AnyDirty && !_confirmDiscard) { _confirmDiscard = true; return; }
            Close();
        }

        // ---------- GUI ----------

        void OnGUI()
        {
            if (_table == null) return;
            if (_filterDirty) RefreshShown();
            InitStyles();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                RequestClose();
                if (_table == null) return;
            }

            float scale = Screen.height / 1080f * Plugin.LegendScale.Value;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float sw = Screen.width / scale, sh = Screen.height / scale;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(980f, sw - 40f), h = Mathf.Min(720f, sh - 40f);
            _window = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.Box(_window, GUIContent.none);
            GUI.Box(_window, GUIContent.none);
            GUI.Box(_window, GUIContent.none);

            GUILayout.BeginArea(new Rect(_window.x + 14, _window.y + 10, w - 28, h - 20));
            DrawHeader();
            DrawFilters();
            DrawList(w - 28, h - 20 - 210f);
            DrawFooter();
            GUILayout.EndArea();

            GUI.matrix = oldMatrix;
        }

        void InitStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            _header = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _rowStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            int dirty = _rows.Count(r => r.Dirty);
            GUILayout.Label($"Map table markers  <size=14>({_rows.Count(r => !r.Deleted)}{(dirty > 0 ? $", <color=#ffd060>{dirty} unsaved</color>" : "")})</size>", _header);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(32), GUILayout.Height(28))) RequestClose();
            GUILayout.EndHorizontal();

            if (!_canEdit) GUILayout.Label($"<color=#ffa030>{_whyReadOnly}</color>", _small);
        }

        void DrawFilters()
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", _label, GUILayout.Width(52));
            string search = GUILayout.TextField(_search, _rowStyle, GUILayout.Width(240), GUILayout.Height(26));
            if (search != _search) { _search = search; _filterDirty = true; }
            GUILayout.Space(10);

            foreach (var type in EditableIcons)
            {
                bool hidden = _hiddenTypes.Contains(type);
                GUI.color = hidden ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
                if (GUILayout.Button(GUIContent.none, GUILayout.Width(30), GUILayout.Height(26)))
                {
                    if (hidden) _hiddenTypes.Remove(type); else _hiddenTypes.Add(type);
                    _filterDirty = true;
                }
                DrawIcon(GUILayoutUtility.GetLastRect(), type);
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Sort", _label, GUILayout.Width(34));
            if (GUILayout.Button(_onlyDuplicates ? "By group" : _sort.ToString(), GUILayout.Width(90), GUILayout.Height(26)) && !_onlyDuplicates)
            {
                _sort = (Sort)(((int)_sort + 1) % 4);
                _filterDirty = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Toggle(ref _onlyDuplicates, $"Possible duplicates ({_rows.Count(r => r.Duplicate)}, within {Plugin.DuplicateMergeDistance.Value:0} m)");
            Toggle(ref _onlyCrossed, "Crossed out");
            Toggle(ref _onlyMine, "Mine");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Showing {_shown.Count}", _small);
            GUILayout.EndHorizontal();
        }

        void Toggle(ref bool value, string text)
        {
            bool now = GUILayout.Toggle(value, " " + text, GUILayout.Height(24));
            if (now != value) { value = now; _filterDirty = true; }
            GUILayout.Space(12);
        }

        void DrawList(float width, float height)
        {
            GUILayout.Space(4);
            Rect area = GUILayoutUtility.GetRect(width, height);
            float contentH = _shown.Count * RowH;
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0, 0, width - 20, contentH));

            // Only draw rows in view; tables can hold thousands of markers.
            int first = Mathf.Max(0, (int)(_scroll.y / RowH));
            int last = Mathf.Min(_shown.Count, first + (int)(height / RowH) + 2);
            int lastGroup = int.MinValue;
            for (int i = first; i < last; i++)
            {
                var row = _shown[i];
                float y = i * RowH;
                if (i % 2 == 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.05f);
                    GUI.DrawTexture(new Rect(0, y, width - 20, RowH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                if (_onlyDuplicates && row.Group != lastGroup && i > first)
                {
                    GUI.color = new Color(1f, 0.8f, 0.3f, 0.5f);
                    GUI.DrawTexture(new Rect(0, y, width - 20, 1), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                lastGroup = row.Group;
                DrawRow(row, y, width - 20);
            }
            GUI.EndScrollView();
        }

        void DrawRow(Row row, float y, float width)
        {
            float x = 4;
            GUI.enabled = _canEdit;
            row.Selected = GUI.Toggle(new Rect(x, y + 4, 20, 20), row.Selected, GUIContent.none);
            x += 26;

            // Icon: click to cycle through the five marker icons.
            var iconRect = new Rect(x, y + 2, 24, 24);
            bool editableIcon = Array.IndexOf(EditableIcons, row.Edit.Type) >= 0;
            if (GUI.Button(iconRect, GUIContent.none, GUIStyle.none) && editableIcon && !row.Deleted)
            {
                int idx = Array.IndexOf(EditableIcons, row.Edit.Type);
                row.Edit.Type = EditableIcons[(idx + 1) % EditableIcons.Length];
                _filterDirty = true;
            }
            GUI.enabled = true;
            GUI.color = row.Deleted ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            DrawIcon(iconRect, row.Edit.Type);
            GUI.color = Color.white;
            x += 32;

            float nameW = width - x - 380;
            if (row.Deleted)
                GUI.Label(new Rect(x, y, nameW, RowH), $"<color=#888888><s>{Escape(row.Edit.Name)}</s> (deleted)</color>", _label);
            else
            {
                GUI.enabled = _canEdit;
                string name = GUI.TextField(new Rect(x, y + 2, nameW, RowH - 4), row.Edit.Name ?? "", 40, _rowStyle);
                if (name != row.Edit.Name) row.Edit.Name = name;
                GUI.enabled = true;
            }
            x += nameW + 8;

            GUI.Label(new Rect(x, y, 120, RowH), row.Owner, _small);
            x += 124;
            GUI.Label(new Rect(x, y, 70, RowH), $"{row.Distance:0} m", _small);
            x += 72;

            GUI.enabled = _canEdit && !row.Deleted;
            bool crossed = GUI.Toggle(new Rect(x, y + 4, 90, 20), row.Edit.Checked, " Crossed");
            if (crossed != row.Edit.Checked) { row.Edit.Checked = crossed; _filterDirty = true; }
            x += 94;

            GUI.enabled = _canEdit;
            if (GUI.Button(new Rect(x, y + 2, 72, RowH - 4), row.Deleted ? "Restore" : "Delete"))
            {
                row.Deleted = !row.Deleted;
                _filterDirty = true;
            }
            GUI.enabled = true;

            if (row.Dirty && !row.Deleted)
            {
                GUI.color = new Color(1f, 0.82f, 0.3f);
                GUI.DrawTexture(new Rect(0, y + 4, 2, RowH - 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        void DrawFooter()
        {
            GUILayout.Space(6);
            GUI.enabled = _canEdit;
            GUILayout.BeginHorizontal();
            int selected = _rows.Count(r => r.Selected);
            if (GUILayout.Button("Select shown", GUILayout.Height(28))) foreach (var r in _shown) r.Selected = true;
            if (GUILayout.Button("Select none", GUILayout.Height(28))) foreach (var r in _rows) r.Selected = false;
            GUILayout.Space(10);
            if (GUILayout.Button($"Cross out ({selected})", GUILayout.Height(28)))
                Bulk(r => r.Edit.Checked = true);
            if (GUILayout.Button($"Uncross ({selected})", GUILayout.Height(28)))
                Bulk(r => r.Edit.Checked = false);
            if (GUILayout.Button($"Delete ({selected})", GUILayout.Height(28)))
                Bulk(r => r.Deleted = true);
            GUILayout.Space(10);
            if (GUILayout.Button("Keep one per duplicate group", GUILayout.Height(28)))
                MergeDuplicates();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (_confirmDiscard)
            {
                GUILayout.Label("<color=#ffd060>You have unsaved changes.</color>", _label);
                GUI.enabled = true;
                if (GUILayout.Button("Discard and close", GUILayout.Width(160), GUILayout.Height(32))) Close();
                if (GUILayout.Button("Keep editing", GUILayout.Width(130), GUILayout.Height(32))) _confirmDiscard = false;
            }
            else
            {
                GUILayout.Label(_status, _small);
                GUILayout.FlexibleSpace();
                GUI.enabled = _canEdit && AnyDirty;
                if (GUILayout.Button("Undo all", GUILayout.Width(100), GUILayout.Height(32))) Reload();
                if (GUILayout.Button("<b>Save to table</b>", GUILayout.Width(160), GUILayout.Height(32))) Save();
                GUI.enabled = true;
                if (GUILayout.Button("Close", GUILayout.Width(90), GUILayout.Height(32))) RequestClose();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        void Bulk(Action<Row> action)
        {
            foreach (var r in _rows.Where(r => r.Selected)) action(r);
            _filterDirty = true;
        }

        // In each group of possible duplicates keep one (yours first, then the one with a name) and delete the rest.
        void MergeDuplicates()
        {
            int deleted = 0;
            foreach (var group in _rows.Where(r => r.Duplicate && !r.Deleted).GroupBy(r => r.Group))
            {
                var keep = group.OrderBy(r => r.Original.Owner == _me ? 0 : 1)
                    .ThenBy(r => string.IsNullOrEmpty(r.Edit.Name) ? 1 : 0).First();
                foreach (var r in group)
                    if (r != keep) { r.Deleted = true; deleted++; }
            }
            _status = deleted > 0 ? $"Marked {deleted} duplicate(s) for deletion. Review, then Save." : "No duplicates found.";
            _filterDirty = true;
        }

        static void DrawIcon(Rect rect, Minimap.PinType type)
        {
            var map = Minimap.instance;
            Sprite sprite = map != null ? GetSprite(map, type) : null;
            if (sprite == null) return;
            Rect tr = sprite.textureRect;
            var tex = sprite.texture;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), tex, uv);
        }

        static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
    }

    // ---------- Hooks ----------

    // Modifier + E on either side of a cartography table opens the manager instead of reading or writing.
    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    static class TableSwitchPatch
    {
        static bool Prefix(Switch __instance, Humanoid character, bool hold, ref bool __result)
        {
            if (hold || !MarkerManagerHooks.Enabled || character != Player.m_localPlayer || !MarkerManagerHooks.ModifierHeld()) return true;
            var table = __instance.GetComponentInParent<MapTable>();
            if (table == null) return true;

            MarkerManager.Instance?.Open(table);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MapTable), "GetReadHoverText")]
    static class TableReadHoverPatch
    {
        static void Postfix(ref string __result) => MarkerManagerHooks.AppendHint(ref __result);
    }

    [HarmonyPatch(typeof(MapTable), "GetWriteHoverText")]
    static class TableWriteHoverPatch
    {
        static void Postfix(ref string __result) => MarkerManagerHooks.AppendHint(ref __result);
    }

    // While the manager is open, tell the game a text box is open: this frees the cursor and blocks
    // movement, camera, the map key and the menu key, the same way typing on a sign does.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    static class TextInputVisiblePatch
    {
        static void Postfix(ref bool __result)
        {
            if (MarkerManager.IsOpen) __result = true;
        }
    }

    // The inventory key isn't blocked by open text boxes, so block it here.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    static class InventoryShowPatch
    {
        static bool Prefix() => !MarkerManager.IsOpen;
    }

    static class MarkerManagerHooks
    {
        internal static bool Enabled => Plugin.EnableMarkerManager.Value && !Compat.CartographyManagerDisabled;

        internal static bool ModifierHeld()
        {
            var key = Plugin.ManagerModifier.Value;
            return key != KeyCode.None && UnityInput.Current.GetKey(key);
        }

        internal static void AppendHint(ref string text)
        {
            if (!Enabled || Plugin.ManagerModifier.Value == KeyCode.None) return;
            text += $"\n[<color=yellow><b>{Plugin.ManagerModifier.Value} + $KEY_Use</b></color>] Manage markers";
            text = Localization.instance.Localize(text);
        }
    }
}
