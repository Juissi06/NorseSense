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
        const float ShowAfterResting = 15f; // standing by a fire indoors keeps you Resting, so don't stay up forever

        // One comfort slot: the piece that counts, and the names of the ones it beat.
        internal class Entry
        {
            internal Piece Piece;
            internal int Value;
            internal readonly List<string> Skipped = new List<string>(2);
        }

        static readonly List<Piece> TempPieces = new List<Piece>();
        static Piece _ghost;

        static readonly InfoLines Lines = new InfoLines();
        readonly List<Entry> _entries = new List<Entry>();
        bool _show, _inShelter;
        int _level;
        InfoRow _shelterReason;
        float _timer, _restingSince = -1f;

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

        // "Chair", "Fire"... Pieces without a group only compete with copies of themselves.
        static string GroupName(Piece piece) =>
            piece.m_comfortGroup == Piece.ComfortGroup.None ? "Other" : piece.m_comfortGroup.ToString();

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
            bool resting = player != null && player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectResting);
            if (!resting) _restingSince = -1f;
            else if (_restingSince < 0f) _restingSince = Time.time;
            _show = Plugin.ShowComfortBreakdown.Value && player != null && !player.InPlaceMode() &&
                    (player.IsSitting() || (resting && Time.time - _restingSince < ShowAfterResting));
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

        void OnGUI()
        {
            if (!_show || Hud.IsUserHidden() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()) return;

            var lines = Lines;
            lines.Clear();
            lines.Add(Ui.StatusIcon(SEMan.s_statusEffectRested), "Comfort", $"<b>{_level}</b>",
                $"Rested lasts {RestedMinutes(_level):0} min", _inShelter ? Tone.Good : Tone.Warn);
            if (!_inShelter)
            {
                lines.Add(Ui.StatusIcon(SEMan.s_statusEffectShelter), "Shelter", "None, comfort pieces don't count", null, Tone.Warn);
                lines.Add(_shelterReason);
            }
            else
            {
                lines.Add(Ui.StatusIcon(SEMan.s_statusEffectShelter), "Shelter", "<b>+1</b>", "plus 1 base");
                foreach (var e in _entries)
                {
                    string name = Localization.instance.Localize(e.Piece.m_name);
                    string skipped = e.Skipped.Count > 0 ? "not counted: " + string.Join(", ", e.Skipped) : null;
                    if (e.Value > 0)
                        lines.Add(e.Piece.m_icon, GroupName(e.Piece), $"{name} <b>+{e.Value}</b>", skipped);
                    else
                        lines.Add(e.Piece.m_icon, GroupName(e.Piece), $"{name} +0, not lit", skipped, Tone.Warn);
                }
            }
            InfoBox.Draw(lines);
        }
    }
}
