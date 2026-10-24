using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Fine rotation, rotation matching and a comfort preview while building.
    internal class BuildHelpers : MonoBehaviour
    {
        const float ComfortRadius = 10f; // SE_Rested.c_ComfortRadius

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

        static readonly List<Piece> TempPieces = new List<Piece>();

        int _comfortNow, _comfortAfter;
        bool _showComfort, _inShelter;
        float _comfortTimer;
        string _shelterLine;
        float _shelterTimer;

        internal static bool FineModifierHeld() =>
            !Compat.FineRotationDisabled &&
            Plugin.FineRotationModifier.Value != KeyCode.None &&
            UnityInput.Current.GetKey(Plugin.FineRotationModifier.Value);

        void Update()
        {
            var player = Player.m_localPlayer;
            if (player == null || !player.InPlaceMode())
            {
                _showComfort = false;
                _shelterLine = null;
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

        // Same algorithm as SE_Rested.CalculateComfortLevel, with the ghost piece optionally added.
        void UpdateComfort(Player player)
        {
            _showComfort = false;
            if (!Plugin.ShowComfortPreview.Value) return;

            var ghost = PlacementGhost(player);
            var ghostPiece = ghost != null && ghost.activeInHierarchy ? ghost.GetComponent<Piece>() : null;
            if (ghostPiece == null || ghostPiece.m_comfort <= 0) return;

            Vector3 pos = player.transform.position;
            _inShelter = player.InShelter();
            _comfortNow = SE_Rested.CalculateComfortLevel(_inShelter, pos);
            _comfortAfter = _comfortNow;

            if (_inShelter && Vector3.Distance(pos, ghost.transform.position) < ComfortRadius)
            {
                TempPieces.Clear();
                Piece.GetAllComfortPiecesInRadius(pos, ComfortRadius, TempPieces);
                TempPieces.Add(ghostPiece);
                TempPieces.Sort(ComfortSort);

                int level = 2;
                for (int i = 0; i < TempPieces.Count; i++)
                {
                    Piece piece = TempPieces[i];
                    if (i > 0)
                    {
                        Piece prev = TempPieces[i - 1];
                        if ((piece.m_comfortGroup != 0 && piece.m_comfortGroup == prev.m_comfortGroup) || piece.m_name == prev.m_name)
                            continue;
                    }
                    level += Comfort(piece, ghostPiece);
                }
                _comfortAfter = level;
            }
            _showComfort = true;
        }

        // The ghost's comfort object (e.g. a fire's flames) is usually inactive, so count its base value.
        static int Comfort(Piece piece, Piece ghost) => piece == ghost ? piece.m_comfort : piece.GetComfort();

        static int ComfortSort(Piece x, Piece y)
        {
            if (x.m_comfortGroup != y.m_comfortGroup) return x.m_comfortGroup.CompareTo(y.m_comfortGroup);
            float a = x.m_comfort, b = y.m_comfort;
            if (a != b) return b.CompareTo(a);
            return y.m_name.CompareTo(x.m_name);
        }

        void OnGUI()
        {
            var player = Player.m_localPlayer;
            if (!Plugin.ShowBuildInfo.Value || player == null || !player.InPlaceMode()) return;
            if (Hud.IsUserHidden() || Hud.IsPieceSelectionVisible() || Menu.IsVisible()) return;

            bool showRotation = FineModifierHeld() || Time.time < RotationShownUntil;
            string plantLine = PlantPreview.Line;
            string shelterLine = PlantPreview.Line == null ? _shelterLine : null;
            string terrainLine = TerrainInfo.Line;
            if (!showRotation && !_showComfort && plantLine == null && shelterLine == null && terrainLine == null) return;

            var lines = new List<string>(5);
            if (plantLine != null) lines.Add(plantLine);
            if (terrainLine != null) lines.Add(terrainLine);
            if (showRotation)
                lines.Add($"Rotation <b>{CurrentYaw(player):0.#}°</b>  <size=12>({Plugin.FineRotationModifier.Value} + scroll: {Plugin.FineRotationStep.Value:0.#}° steps, {Plugin.MatchRotationKey.Value}: match / reset, {Plugin.BuildInfoToggleKey.Value}: hide)</size>");
            if (_showComfort)
            {
                if (!_inShelter)
                    lines.Add("Comfort: <color=#ffa030>not in shelter here, comfort pieces don't count</color>");
                else if (_comfortAfter > _comfortNow)
                    lines.Add($"Comfort here: {_comfortNow} → <b><color=#80ff80>{_comfortAfter}</color></b>  <size=12>({Plugin.BuildInfoToggleKey.Value}: hide)</size>");
                else
                    lines.Add($"Comfort here: {_comfortNow} <color=#c0c0c0>(no gain, out of range or same type already counted)</color>");
            }
            if (shelterLine != null) lines.Add(shelterLine);
            DrawInfoBox(lines);
        }

        static GUIStyle _style;

        // The build info box: centred, at the configured height. Call from OnGUI.
        internal static void DrawInfoBox(List<string> lines)
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15, richText = true, alignment = TextAnchor.MiddleCenter
                };
                _style.normal.textColor = Color.white;
            }

            float scale = Screen.height / 1080f * Plugin.LegendScale.Value;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float w = 640f, lineH = 24f;
            float h = lines.Count * lineH + 10f;
            float x = (Screen.width / scale - w) * 0.5f;
            float y = Plugin.BuildInfoY.Value * (Screen.height / scale - h);

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            for (int i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(x, y + 5f + i * lineH, w, lineH), lines[i], _style);

            GUI.matrix = oldMatrix;
        }
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
