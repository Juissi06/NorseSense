using System.Collections.Generic;
using UnityEngine;

namespace NorseSense
{
    // What makes up your comfort level: SE_Rested.CalculateComfortLevel, keeping track of which piece counted
    // for each slot and which were skipped because a better one of the same kind is near. Shown while resting.
    internal class ComfortInfo : MonoBehaviour
    {
        internal const float Radius = 10f; // SE_Rested.c_ComfortRadius
        const float RefreshInterval = 1f;

        // One comfort slot: the piece that counts, and the names of the ones it beat.
        internal class Entry
        {
            internal Piece Piece;
            internal int Value;
            internal readonly List<string> Skipped = new List<string>(2);
        }

        static readonly List<Piece> TempPieces = new List<Piece>();
        static Piece _ghost;

        readonly List<Entry> _entries = new List<Entry>();
        bool _show, _inShelter;
        int _level;
        InfoRow _shelterReason;
        float _timer;

        // Fills entries for the comfort pieces around pos, with an optional ghost piece counted at full value.
        // Returns the comfort level the game would give there.
        internal static int Calculate(bool inShelter, Vector3 pos, Piece ghost, List<Entry> entries)
        {
            entries.Clear();
            if (!inShelter) return 1;

            TempPieces.Clear();
            Piece.GetAllComfortPiecesInRadius(pos, Radius, TempPieces);
            if (ghost != null) TempPieces.Add(ghost);
            _ghost = ghost;
            TempPieces.Sort(ComfortSort);

            int level = 2;
            Entry current = null;
            for (int i = 0; i < TempPieces.Count; i++)
            {
                Piece piece = TempPieces[i];
                if (i > 0)
                {
                    Piece prev = TempPieces[i - 1];
                    if ((piece.m_comfortGroup != 0 && piece.m_comfortGroup == prev.m_comfortGroup) || piece.m_name == prev.m_name)
                    {
                        string name = Localization.instance.Localize(piece.m_name);
                        if (current != null && !current.Skipped.Contains(name)) current.Skipped.Add(name);
                        continue;
                    }
                }
                current = new Entry { Piece = piece, Value = Value(piece) };
                entries.Add(current);
                level += current.Value;
            }
            _ghost = null;
            return level;
        }

        // The ghost's comfort object (e.g. a fire's flames) is usually inactive, so count its base value.
        static int Value(Piece piece) => piece == _ghost ? piece.m_comfort : piece.GetComfort();

        static int ComfortSort(Piece x, Piece y)
        {
            if (x.m_comfortGroup != y.m_comfortGroup) return x.m_comfortGroup.CompareTo(y.m_comfortGroup);
            float a = Value(x), b = Value(y);
            if (a != b) return b.CompareTo(a);
            return y.m_name.CompareTo(x.m_name);
        }

        // Rested lasts SE_Rested.m_baseTTL + (comfort - 1) * m_TTLPerComfortLevel seconds.
        internal static float RestedMinutes(int comfort)
        {
            var rested = ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectRested) as SE_Rested : null;
            float baseTtl = rested != null ? rested.m_baseTTL : 300f;
            float perLevel = rested != null ? rested.m_TTLPerComfortLevel : 60f;
            return (baseTtl + (comfort - 1) * perLevel) / 60f;
        }

        void Update()
        {
            var player = Player.m_localPlayer;
            // Only while sitting: the game counts you as Resting whenever you're sheltered near a fire, so
            // following that would keep the card up the whole time you walk around your base. Not on a ship's seats.
            _show = Plugin.ShowComfortBreakdown.Value && player != null && !player.InPlaceMode() && player.IsSitting() && !player.IsAttachedToShip();
            if (!_show)
            {
                _timer = 0f;
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            _inShelter = player.InShelter();
            _level = Calculate(_inShelter, player.transform.position, null, _entries);
            _shelterReason = _inShelter ? null : ShelterInfo.Describe(player, true);
        }

        // A small card under the game's status effects, right below the Resting and Rested entries it explains:
        // total and Rested time on one line, then each piece that counts as its icon and value, and one line for
        // what doesn't count.
        const float Pad = 10f, HeaderH = 24f, CellH = 28f, IconSize = 22f, Gap = 14f, FootH = 18f;
        const int PerRow = 6;

        void OnGUI()
        {
            if (!_show || Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;
            Matrix4x4 oldMatrix = Ui.BeginScaled(out float sw, out float sh);
            float scale = Screen.height / sh;

            string header = $"<color={Ui.Gold}>Comfort</color> <b>{_level}</b>";
            string rested = _inShelter ? $"Rested {RestedMinutes(_level):0} min" : $"<color={Ui.Warn}>not sheltered</color>";
            int skipped = 0;
            foreach (var e in _entries) skipped += e.Skipped.Count;
            string foot = !_inShelter
                ? _shelterReason?.Value
                : skipped > 0 ? $"{skipped} more nearby don't count (same kind)" : null;

            // Cells: shelter first, then every comfort slot.
            int cells = _inShelter ? _entries.Count + 1 : 0;
            int rows = (cells + PerRow - 1) / PerRow;
            float cellW = IconSize + 4f + Ui.LineWidth("<b>+3</b>", Ui.Small) + Gap;
            float w = Mathf.Max(Ui.LineWidth(header, Ui.Text) + 16f + Ui.LineWidth(rested, Ui.Small),
                Mathf.Min(cells, PerRow) * cellW - Gap);
            if (foot != null) w = Mathf.Max(w, Ui.LineWidth(foot, Ui.Small));
            w = Mathf.Min(w, sw * 0.4f) + Pad * 2f;
            float h = Pad * 2f + HeaderH + rows * CellH + (foot != null ? FootH : 0f);

            Rect box = Place(w, h, sw, sh, scale);
            Ui.Panel(box);
            float x = box.x + Pad, y = box.y + Pad;
            Ui.Label(new Rect(x, y, w, HeaderH), header, Ui.Text);
            Ui.Label(new Rect(box.xMax - Pad - Ui.LineWidth(rested, Ui.Small) - 2f, y, w, HeaderH), rested, Ui.Small);
            y += HeaderH;

            for (int i = 0; i < cells; i++)
            {
                float cx = x + (i % PerRow) * cellW, cy = y + (i / PerRow) * CellH;
                Sprite icon;
                string value;
                if (i == 0)
                {
                    icon = Ui.StatusIcon(SEMan.s_statusEffectShelter);
                    value = "<b>+1</b>";
                }
                else
                {
                    var e = _entries[i - 1];
                    icon = e.Piece.m_icon;
                    value = e.Value > 0 ? $"<b>+{e.Value}</b>" : $"<color={Ui.Warn}>+0</color>";
                }
                Ui.Icon(new Rect(cx, cy + (CellH - IconSize) * 0.5f, IconSize, IconSize), icon);
                Ui.Label(new Rect(cx + IconSize + 4f, cy, cellW, CellH), value, Ui.Small);
            }
            y += rows * CellH;

            if (foot != null) Ui.Label(new Rect(x, y, w - Pad * 2f, FootH), foot, Ui.Small);
            GUI.matrix = oldMatrix;
        }

        // Right side, under the status effect list and lined up with its right edge, so it follows the list as
        // effects come and go. Keeps clear of the base view legend, which also defaults to the right.
        static Rect Place(float w, float h, float sw, float sh, float scale)
        {
            const float margin = 10f;
            float right = sw - 24f, top = sh * 0.4f;
            var hud = Hud.instance;
            if (hud != null && hud.m_statusEffectListRoot != null &&
                InfoBox.ContentRect(hud.m_statusEffectListRoot.gameObject, scale, out Rect list))
            {
                right = list.xMax;
                top = list.yMax + margin;
            }
            var box = new Rect(right - w, top, w, h);

            if (BaseView.LegendShowing && box.Overlaps(BaseView.LegendRect))
            {
                box.y = BaseView.LegendRect.yMax + margin;              // below the legend if there's room
                if (box.yMax > sh - margin) box.y = BaseView.LegendRect.yMin - h - margin; // otherwise above it
            }
            box.y = Mathf.Clamp(box.y, margin, sh - h - margin);
            return box;
        }
    }
}
