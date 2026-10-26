using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Fine rotation, rotation matching and a comfort preview while building.
    internal class BuildHelpers : MonoBehaviour
    {
        internal static readonly AccessTools.FieldRef<Player, int> PlaceRotation =
            AccessTools.FieldRefAccess<Player, int>("m_placeRotation");
        internal static readonly AccessTools.FieldRef<Player, float> PlaceRotationDegrees =
            AccessTools.FieldRefAccess<Player, float>("m_placeRotationDegrees");
        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // Extra yaw in degrees on top of vanilla's 22.5° steps.
        internal static float FineOffset;
        // The rotation line shows briefly after a change instead of staying up the whole time.
        internal static float RotationShownUntil;
        const float RotationShowTime = 2.5f;

        static readonly List<ComfortInfo.Entry> Entries = new List<ComfortInfo.Entry>();

        // The first time you build each session, the box lists the mod's keys for a few seconds.
        const float TipTime = 7f;
        static bool _tipShown;
        static float _tipUntil;

        int _comfortNow, _comfortAfter;
        bool _showComfort, _inShelter;
        float _comfortTimer;
        string _noGainReason;
        InfoRow _shelterLine, _stationLine, _supportLine, _hoverLine;
        float _shelterTimer;

        internal static bool FineModifierHeld() =>
            !Compat.FineRotationDisabled &&
            Plugin.FineRotationModifier.Value != KeyCode.None &&
            UnityInput.Current.GetKey(Plugin.FineRotationModifier.Value);

        void Update()
        {
            var player = Player.m_localPlayer;
            if (player != null && player.InPlaceMode() && !_tipShown && Plugin.ShowKeyTips.Value)
            {
                _tipShown = true;
                _tipUntil = Time.time + TipTime;
            }
            if (player == null || !player.InPlaceMode())
            {
                _showComfort = false;
                _shelterLine = null;
                _supportLine = null;
                _stationLine = null;
                _hoverLine = null;
                return;
            }

            if (!Compat.FineRotationDisabled && Plugin.MatchRotationKey.Value.IsDown() &&
                !Hud.IsPieceSelectionVisible() && !Plugin.IsTyping())
                MatchOrResetRotation(player);

            _comfortTimer -= Time.deltaTime;
            if (_comfortTimer <= 0f)
            {
                _comfortTimer = 0.25f;
                UpdateComfort(player);
                var hovered = player.GetHoveringPiece();
                _supportLine = Plugin.ShowSupportInfo.Value ? SupportInfo.Describe(hovered) : null;
                _hoverLine = HoverInfo.Describe(hovered);
                var ghost = PlacementGhost(player);
                var extension = Plugin.ShowStationInfo.Value && ghost != null && ghost.activeInHierarchy ? ghost.GetComponent<StationExtension>() : null;
                _stationLine = extension != null ? StationInfo.DescribeGhost(extension, ghost.transform.position) : null;
            }

            _shelterTimer -= Time.deltaTime;
            if (_shelterTimer <= 0f)
            {
                _shelterTimer = 1f; // the game re-checks cover once a second too
                _shelterLine = Plugin.ShowShelterInfo.Value && PlantPreview.Line == null ? ShelterInfo.Describe(player, _showComfort) : null;
            }
        }

        static void MatchOrResetRotation(Player player)
        {
            RotationShownUntil = Time.time + RotationShowTime;
            Piece hovered = player.GetHoveringPiece();
            float step = PlaceRotationDegrees(player);
            if (hovered != null)
            {
                float yaw = hovered.transform.rotation.eulerAngles.y;
                int steps = Mathf.RoundToInt(yaw / step);
                PlaceRotation(player) = steps;
                FineOffset = yaw - steps * step;
                player.Message(MessageHud.MessageType.TopLeft, $"Rotation matched: {yaw:0.#}°");
            }
            else
            {
                FineOffset = 0f;
                player.Message(MessageHud.MessageType.TopLeft, "Fine rotation reset");
            }
        }

        internal static float CurrentYaw(Player player) =>
            Mathf.Repeat(PlaceRotationDegrees(player) * PlaceRotation(player) + FineOffset, 360f);

        // Comfort now and with the ghost placed, and if it adds nothing, why.
        void UpdateComfort(Player player)
        {
            _showComfort = false;
            if (!Plugin.ShowComfortPreview.Value) return;

            var ghost = PlacementGhost(player);
            var ghostPiece = ghost != null && ghost.activeInHierarchy ? ghost.GetComponent<Piece>() : null;
            if (ghostPiece == null || ghostPiece.m_comfort <= 0) return;

            Vector3 pos = player.transform.position;
            _inShelter = player.InShelter();
            _comfortNow = ComfortInfo.Calculate(_inShelter, pos, null, Entries);
            _comfortAfter = _comfortNow;
            _noGainReason = null;

            if (_inShelter && Vector3.Distance(pos, ghost.transform.position) >= ComfortInfo.Radius)
                _noGainReason = $"too far from you, comfort counts within {ComfortInfo.Radius:0} m";
            else if (_inShelter)
            {
                _comfortAfter = ComfortInfo.Calculate(true, pos, ghostPiece, Entries);
                if (_comfortAfter <= _comfortNow)
                {
                    // The ghost lost its slot to a piece of the same kind that's worth at least as much.
                    foreach (var e in Entries)
                    {
                        if (e.Piece == ghostPiece)
                        {
                            if (e.Skipped.Count == 0) continue;
                            _noGainReason = $"{e.Skipped[0]} nearby already gives the same";
                            break;
                        }
                        bool sameSlot = (ghostPiece.m_comfortGroup != 0 && e.Piece.m_comfortGroup == ghostPiece.m_comfortGroup) ||
                                        e.Piece.m_name == ghostPiece.m_name;
                        if (!sameSlot) continue;
                        string group = ghostPiece.m_comfortGroup != 0 ? ghostPiece.m_comfortGroup.ToString().ToLowerInvariant() : "piece";
                        _noGainReason = $"{Localization.instance.Localize(e.Piece.m_name)} (+{e.Value}) already counts as your {group}";
                        break;
                    }
                }
            }
            _showComfort = true;
        }

        void OnGUI()
        {
            var player = Player.m_localPlayer;
            if (!Plugin.ShowBuildInfo.Value || player == null || !player.InPlaceMode()) return;
            if (Hud.IsUserHidden() || Hud.IsPieceSelectionVisible() || Menu.IsVisible()) return;

            bool showRotation = FineModifierHeld() || Time.time < RotationShownUntil;
            InfoRow plantLine = PlantPreview.Line;
            InfoRow placementLine = plantLine == null ? PlacementInfo.Line : null;
            InfoRow shelterLine = plantLine == null ? _shelterLine : null;

            var lines = Lines;
            lines.Clear();
            Sprite piece = player.GetSelectedPiece()?.m_icon;
            // The lock is invisible otherwise, so say so while it's on.
            if (RemovalLockPatch.Locked)
                lines.Add(null, "Removal", "Locked", $"{Ui.KeyName(Plugin.RemovalLockKey.Value)} to unlock", Tone.Warn);
            if (Freecam.Active) Freecam.AddRows(lines, player);
            lines.Add(plantLine, piece);
            lines.Add(placementLine, piece);
            AreaTools.AddRows(lines);
            lines.Add(_stationLine, piece);
            lines.Add(TerrainInfo.Line, piece);
            if (showRotation) lines.Add(piece, "Rotation", $"<b>{CurrentYaw(player):0.#}°</b>", $"{Plugin.FineRotationStep.Value:0.#}° fine steps");
            if (_showComfort) AddComfort(lines);
            lines.Add(shelterLine);
            lines.Add(_hoverLine);
            lines.Add(_supportLine);

            if (Freecam.Active) Freecam.AddHints(lines);
            else if (showRotation && !Compat.FineRotationDisabled)
            {
                if (Plugin.FineRotationModifier.Value != KeyCode.None)
                    lines.Hint($"{Ui.KeyName(Plugin.FineRotationModifier.Value)} + Scroll", "Fine rotate");
                lines.Hint(Ui.KeyName(Plugin.MatchRotationKey.Value), "Match / reset");
                lines.Hint(Ui.KeyName(Plugin.BuildInfoToggleKey.Value), "Hide");
            }
            else if (Time.time < _tipUntil)
            {
                if (!Compat.FreecamDisabled) lines.Hint(Ui.KeyName(Plugin.FreecamKey.Value), "Freecam");
                if (!Compat.FineRotationDisabled && Plugin.FineRotationModifier.Value != KeyCode.None)
                    lines.Hint($"{Ui.KeyName(Plugin.FineRotationModifier.Value)} + Scroll", "Fine rotate");
                lines.Hint(Ui.KeyName(Plugin.RemovalLockKey.Value), "Lock removal");
                lines.Hint(Ui.KeyName(Plugin.ToggleKey.Value), "Base view");
                lines.Hint(Ui.KeyName(Plugin.BuildInfoToggleKey.Value), "Hide this");
            }
            AreaTools.AddHint(lines);
            if (lines.Count == 0 && lines.HintKeys.Count == 0) return;
            InfoBox.Draw(lines);
        }

        void AddComfort(InfoLines lines)
        {
            Sprite rested = Ui.StatusIcon(SEMan.s_statusEffectRested);
            if (!_inShelter)
                lines.Add(rested, "Comfort", "Not sheltered here", "comfort pieces only count under a roof", Tone.Warn);
            else if (_comfortAfter > _comfortNow)
                lines.Add(rested, "Comfort", $"{_comfortNow} » <b>{_comfortAfter}</b>", null, Tone.Good);
            else
                lines.Add(rested, "Comfort", $"{_comfortNow}, no gain", _noGainReason);
        }

        static readonly InfoLines Lines = new InfoLines();
    }

    // Holding the fine-rotation modifier turns vanilla's rotation steps into fine steps.
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    static class FineRotationInputPatch
    {
        static void Prefix(Player __instance, out int __state) => __state = BuildHelpers.PlaceRotation(__instance);

        static void Postfix(Player __instance, int __state)
        {
            if (__instance != Player.m_localPlayer) return;
            int now = BuildHelpers.PlaceRotation(__instance);
            if (now == __state || !BuildHelpers.FineModifierHeld()) return;

            BuildHelpers.PlaceRotation(__instance) = __state;
            BuildHelpers.RotationShownUntil = Time.time + 2.5f;
            BuildHelpers.FineOffset = Mathf.Repeat(BuildHelpers.FineOffset + (now - __state) * Plugin.FineRotationStep.Value, 360f);
        }
    }

    // Feed the combined angle through vanilla's own rotation math so snapping still works.
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    static class FineRotationGhostPatch
    {
        static float _savedDegrees;
        static int _savedRotation;

        static void Prefix(Player __instance, out bool __state)
        {
            __state = __instance == Player.m_localPlayer && !Mathf.Approximately(BuildHelpers.FineOffset, 0f);
            if (!__state) return;

            _savedDegrees = BuildHelpers.PlaceRotationDegrees(__instance);
            _savedRotation = BuildHelpers.PlaceRotation(__instance);
            BuildHelpers.PlaceRotationDegrees(__instance) = _savedDegrees * _savedRotation + BuildHelpers.FineOffset;
            BuildHelpers.PlaceRotation(__instance) = 1;
        }

        static void Finalizer(Player __instance, bool __state)
        {
            if (!__state) return;
            BuildHelpers.PlaceRotationDegrees(__instance) = _savedDegrees;
            BuildHelpers.PlaceRotation(__instance) = _savedRotation;
        }
    }

    // Vanilla copy-piece (Shift + middle mouse) sets a 22.5° rotation; drop any fine offset with it.
    [HarmonyPatch(typeof(Player), "CopyPiece")]
    static class CopyPiecePatch
    {
        static void Postfix(bool __result)
        {
            if (__result) BuildHelpers.FineOffset = 0f;
        }
    }
}
