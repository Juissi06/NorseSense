using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Freecam building: while in build mode the camera detaches and flies around the build, the hammer keeps
    // working from where the camera looks, and your body stays put. To keep vanilla balance the camera stays
    // within a set distance of you, you place only near the camera, workbench and ward rules still apply where
    // you stand, and taking damage drops you back into your body.
    internal class Freecam : MonoBehaviour
    {
        const float RunMultiplier = 3f;
        const float GroundClearance = 0.4f;

        internal static bool Active;
        internal static Vector3 Position;

        static readonly AccessTools.FieldRef<Player, int> RemoveRayMask =
            AccessTools.FieldRefAccess<Player, int>("m_removeRayMask");

        static Player _player;
        static float _savedPlaceDistance;
        Vector3 _velocity;
        float _lastHealth;

        void Update()
        {
            var player = Player.m_localPlayer;
            if (Active)
            {
                string why = WhyStop(player);
                if (why != null)
                {
                    Exit(why);
                    return;
                }
            }
            if (player == null || Plugin.IsTyping()) return;

            if (Plugin.FreecamKey.Value.IsDown())
            {
                if (Active) Exit(null);
                else Enter(player);
            }
            if (!Active) return;

            // At full health, food wearing off lowers your max health and your health follows it down; that's not
            // damage, so a drop only counts when it leaves you below your max.
            float health = player.GetHealth();
            if (health < _lastHealth - 0.01f && health < player.GetMaxHealth() - 0.01f)
            {
                Exit("you took damage");
                return;
            }
            _lastHealth = health;

            // The game's reach is measured from your eyes; freecam reach is checked from the camera instead.
            float reach = Plugin.FreecamRange.Value + Plugin.FreecamReach.Value + 2f;
            if (!Mathf.Approximately(player.m_maxPlaceDistance, reach)) player.m_maxPlaceDistance = reach;

            Move(player, Time.unscaledDeltaTime);
        }

        void Enter(Player player)
        {
            if (Compat.FreecamDisabled)
            {
                player.Message(MessageHud.MessageType.TopLeft, $"Freecam is off because {Compat.FreecamDisabledBy} provides one.");
                return;
            }
            if (!player.InPlaceMode() || GameCamera.instance == null)
            {
                player.Message(MessageHud.MessageType.TopLeft, "Freecam works in build mode: equip a hammer, hoe or cultivator.");
                return;
            }
            if (WhyStop(player) != null) return;

            Active = true;
            _player = player;
            _savedPlaceDistance = player.m_maxPlaceDistance;
            _lastHealth = player.GetHealth();
            _velocity = Vector3.zero;
            Position = GameCamera.instance.transform.position;
            Plugin.Notify("Freecam", "on", true);
        }

        internal static void Exit(string why)
        {
            if (!Active) return;
            Active = false;
            if (_player != null) _player.m_maxPlaceDistance = _savedPlaceDistance;
            _player = null;
            PlayerTweaks.Apply(); // a build distance setting wins over what freecam saved
            if (Player.m_localPlayer != null)
                Plugin.Notify("Freecam", why != null ? "off, " + why : "off", false);
        }

        static string WhyStop(Player player)
        {
            if (player == null || (Active && player != _player)) return "player changed";
            if (player.IsDead()) return "you died";
            if (!player.InPlaceMode()) return "left build mode";
            if (player.IsAttached() || player.IsTeleporting() || player.InCutscene()) return "busy";
            return null;
        }

        void Move(Player player, float dt)
        {
            Vector3 input = Vector3.zero;
            bool run = false;
            if (!InventoryGui.IsVisible() && !Minimap.IsOpen() && !Hud.IsPieceSelectionVisible() && !Menu.IsVisible())
            {
                if (ZInput.GetButton("Forward")) input.z += 1f;
                if (ZInput.GetButton("Backward")) input.z -= 1f;
                if (ZInput.GetButton("Left")) input.x -= 1f;
                if (ZInput.GetButton("Right")) input.x += 1f;
                if (ZInput.GetButton("Jump")) input.y += 1f;
                if (ZInput.GetButton("Crouch")) input.y -= 1f;
                run = ZInput.GetButton("Run");
            }

            // Level movement along where you look; up and down are separate keys, like a builder's camera.
            Transform cam = GameCamera.instance.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 dir = forward * input.z + right * input.x + Vector3.up * input.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            Vector3 target = dir * Plugin.FreecamSpeed.Value * (run ? RunMultiplier : 1f);
            _velocity = Vector3.Lerp(_velocity, target, 1f - Mathf.Exp(-12f * dt));
            Position += _velocity * dt;

            Vector3 anchor = player.GetCenterPoint();
            Vector3 offset = Position - anchor;
            if (offset.magnitude > Plugin.FreecamRange.Value) Position = anchor + offset.normalized * Plugin.FreecamRange.Value;
            if (ZoneSystem.instance != null)
                Position.y = Mathf.Max(Position.y, ZoneSystem.instance.GetGroundHeight(Position) + GroundClearance);
        }

        internal static float DistanceFromPlayer(Player player) => Vector3.Distance(Position, player.GetCenterPoint());

        internal static void AddRows(InfoLines lines, Player player)
        {
            float dist = DistanceFromPlayer(player);
            bool atEdge = dist > Plugin.FreecamRange.Value - 0.5f;
            lines.Add(null, "Freecam", $"{dist:0} m from you", $"max {Plugin.FreecamRange.Value:0} m, place within {Plugin.FreecamReach.Value:0} m",
                atEdge ? Tone.Warn : Tone.None);
        }

        internal static void AddHints(InfoLines lines)
        {
            lines.Hint($"{Key("Forward")} {Key("Left")} {Key("Backward")} {Key("Right")}", "Move");
            lines.Hint(Key("Jump"), "Up");
            lines.Hint(Key("Crouch"), "Down");
            lines.Hint(Key("Run"), "Faster");
            lines.Hint(Ui.KeyName(Plugin.FreecamKey.Value), "Exit");
        }

        static string Key(string button)
        {
            string key = Localization.instance.GetBoundKeyString(button, emptyStringOnMissing: true);
            return string.IsNullOrEmpty(key) ? button : key;
        }

        void OnDisable() => Exit(null);

        // Placing and removing reach, measured from the camera while flying.
        internal static bool InReach(Vector3 point) =>
            GameCamera.instance != null && Vector3.Distance(point, GameCamera.instance.transform.position) <= Plugin.FreecamReach.Value;

        // Vanilla checks the workbench where you stand, which is fine at arm's length. With the camera far away
        // that would let you build well outside any workbench, so the piece itself must be in range too.
        internal static bool OutsideStationRange(Player player, Piece piece, Vector3 pos) =>
            piece != null && piece.m_craftingStation != null && !player.PlacementCostDisabled &&
            (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) &&
            CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, pos) == null;

        internal static bool RemoveInReach(Player player)
        {
            var cam = GameCamera.instance.transform;
            return Physics.Raycast(cam.position, cam.forward, out var hit, 50f, RemoveRayMask(player)) && InReach(hit.point);
        }
    }

    // The camera goes where the freecam is; rotation still follows your mouse look.
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    static class FreecamCameraPatch
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(GameCamera __instance)
        {
            if (Freecam.Active) __instance.transform.position = Freecam.Position;
        }
    }

    // Your body stands still while the camera flies: movement keys steer the camera instead.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    static class FreecamControlsPatch
    {
        static void Prefix(Player __instance, ref Vector3 movedir, ref bool jump, ref bool crouch, ref bool run, ref bool autoRun, ref bool dodge)
        {
            if (!Freecam.Active || __instance != Player.m_localPlayer) return;
            movedir = Vector3.zero;
            jump = crouch = run = autoRun = dodge = false;
        }
    }

    // Ghost placement only near the camera, not anywhere along a 50 m ray.
    [HarmonyPatch(typeof(Player), "PieceRayTest")]
    static class FreecamPlaceReachPatch
    {
        static void Postfix(Player __instance, ref bool __result, ref Vector3 point)
        {
            if (__result && Freecam.Active && __instance == Player.m_localPlayer && !Freecam.InReach(point)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    static class FreecamStationRangePatch
    {
        static readonly AccessTools.FieldRef<Player, Player.PlacementStatus> Status =
            AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");
        static readonly AccessTools.FieldRef<Player, GameObject> Ghost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        static void Postfix(Player __instance)
        {
            if (!Freecam.Active || __instance != Player.m_localPlayer || Status(__instance) != Player.PlacementStatus.Valid) return;
            var ghost = Ghost(__instance);
            var piece = ghost != null ? ghost.GetComponent<Piece>() : null;
            if (!Freecam.OutsideStationRange(__instance, piece, ghost.transform.position)) return;
            Status(__instance) = Player.PlacementStatus.Invalid; // TryPlacePiece reads this right after, so placing is refused
            piece.SetInvalidPlacementHeightlight(true);
        }
    }

    [HarmonyPatch(typeof(Player), "RemovePiece")]
    static class FreecamRemoveReachPatch
    {
        static bool Prefix(Player __instance, ref bool __result)
        {
            if (!Freecam.Active || __instance != Player.m_localPlayer || Freecam.RemoveInReach(__instance)) return true;
            __result = false;
            return false;
        }
    }
}
