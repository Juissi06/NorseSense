using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // Why the build ghost is red. The game only says "Invalid placement", and only when you click;
    // this repeats the checks in Player.UpdatePlacementGhost that can fail and names the one that did.
    internal class PlacementInfo : MonoBehaviour
    {
        const float RefreshInterval = 0.2f;
        const float MaxPenetration = 0.2f; // Player.UpdatePlacementGhost -> TestGhostClipping

        delegate bool RayTestFn(Player player, out Vector3 point, out Vector3 normal, out Piece piece,
            out Heightmap heightmap, out Collider waterSurface, bool water);

        // Null if a game update renamed it; reasons that need the hit then stay quiet.
        static readonly RayTestFn PieceRayTest = CreateRayTest();
        static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
        static readonly AccessTools.FieldRef<Player, int> PlaceRayMask =
            AccessTools.FieldRefAccess<Player, int>("m_placeRayMask");

        // Shown in the build info box; null when the ghost is fine.
        internal static InfoRow Line;

        static readonly List<string> Reasons = new List<string>(3);
        float _timer;

        static RayTestFn CreateRayTest()
        {
            try
            {
                return AccessTools.MethodDelegate<RayTestFn>(AccessTools.Method(typeof(Player), "PieceRayTest"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Norse Sense] Placement reasons limited (PieceRayTest): {e.Message}");
                return null;
            }
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            Line = null;
            var player = Player.m_localPlayer;
            if (player == null || !Plugin.ShowPlacementReasons.Value || !player.InPlaceMode()) return;
            var ghost = PlacementGhost(player);
            var piece = ghost != null && ghost.activeInHierarchy ? ghost.GetComponent<Piece>() : null;
            if (piece == null) return;

            string reason = Describe(player, ghost, piece, player.GetPlacementStatus());
            if (reason != null) Line = new InfoRow("Placement", char.ToUpperInvariant(reason[0]) + reason.Substring(1), null, Tone.Bad);
        }

        static string Describe(Player player, GameObject ghost, Piece piece, Player.PlacementStatus status)
        {
            switch (status)
            {
                case Player.PlacementStatus.Valid:
                case Player.PlacementStatus.NoRayHits:
                    return null;
                case Player.PlacementStatus.ExtensionMissingStation:
                    // The station line already says which station is missing and how far the nearest is.
                    return Plugin.ShowStationInfo.Value ? null : Msg("$msg_extensionmissingstation");
                case Player.PlacementStatus.Invalid:
                    return Invalid(player, ghost, piece);
                case Player.PlacementStatus.MoreSpace:
                    return MoreSpace(ghost, piece) ?? Msg("$msg_needspace");
                case Player.PlacementStatus.WrongBiome:
                    return $"only in {Biomes(piece.m_onlyInBiome)}";
                case Player.PlacementStatus.PrivateZone:
                    return "inside a ward you aren't permitted on";
                case Player.PlacementStatus.NoBuildZone:
                    return "building is blocked around this place";
                case Player.PlacementStatus.BlockedbyPlayer:
                    return "someone is standing in the way";
                case Player.PlacementStatus.NoTeleportArea: return Msg("$msg_noteleportarea");
                case Player.PlacementStatus.NeedCultivated: return Msg("$msg_needcultivated");
                case Player.PlacementStatus.NeedDirt: return Msg("$msg_needdirt");
                case Player.PlacementStatus.NotInDungeon: return Msg("$msg_notindungeon");
                case Player.PlacementStatus.DeepSnow: return Msg("$msg_snowtoodeep");
                case Player.PlacementStatus.NoSnow: return Msg("$msg_nosnow");
                default: return null;
            }
        }

        // Every check that sets PlacementStatus.Invalid, in the game's order.
        static string Invalid(Player player, GameObject ghost, Piece piece)
        {
            Reasons.Clear();
            Vector3 point = Vector3.zero, normal = Vector3.up;
            Piece hitPiece = null;
            Heightmap heightmap = null;
            Collider water = null;
            bool hit = PieceRayTest != null &&
                       PieceRayTest(player, out point, out normal, out hitPiece, out heightmap, out water, piece.m_waterPiece || piece.m_noInWater);

            if (Freecam.Active && Freecam.OutsideStationRange(player, piece, ghost.transform.position))
                Reasons.Add($"outside {Localization.instance.Localize(piece.m_craftingStation.m_name)} range there");
            if (piece.m_mustConnectTo != null && !FindConnection(ghost, piece))
            {
                string target = Localization.instance.Localize(piece.m_mustConnectTo.GetComponent<Piece>()?.m_name ?? piece.m_mustConnectTo.name);
                Reasons.Add(piece.m_mustBeAboveConnected ? $"must stand on a {target}" : $"must touch a {target}");
            }
            if (hit)
            {
                var wnt = hitPiece != null ? hitPiece.GetComponent<WearNTear>() : null;
                if (wnt != null && !wnt.m_supports)
                    Reasons.Add($"{Localization.instance.Localize(hitPiece.m_name)} can't hold other pieces");
                if (piece.m_waterPiece && water == null && !ZInput.GetButton("AltPlace"))
                    Reasons.Add("must float on water");
                if (piece.m_noInWater && water != null)
                    Reasons.Add("can't go in water");
                if (piece.m_groundOnly && heightmap == null)
                    Reasons.Add("must stand on the ground");
                if (piece.m_notOnWood && hitPiece != null && wnt != null &&
                    (wnt.m_materialType == WearNTear.MaterialType.Wood || wnt.m_materialType == WearNTear.MaterialType.HardWood))
                    Reasons.Add("can't stand on wood");
                if (piece.m_notOnTiltingSurface && normal.y < 0.8f)
                    Reasons.Add($"surface too steep ({Vector3.Angle(normal, Vector3.up):0}°, max 37°)");
                if (piece.m_inCeilingOnly && normal.y > -0.5f)
                    Reasons.Add("must hang from a ceiling");
                if (piece.m_notOnFloor && normal.y > 0.1f)
                    Reasons.Add("can't stand on a floor");
            }
            if (piece.m_noClipping)
            {
                string clip = Clipping(ghost, PlaceRayMask(player));
                if (clip != null) Reasons.Add($"overlaps {clip}");
            }
            return Reasons.Count > 0 ? string.Join(", ", Reasons) : null;
        }

        // Same search as the m_mustConnectTo check in UpdatePlacementGhost.
        static bool FindConnection(GameObject ghost, Piece piece)
        {
            foreach (var col in Physics.OverlapSphere(ghost.transform.position, piece.m_connectRadius))
            {
                var nview = col.GetComponentInParent<ZNetView>();
                if (nview == null || nview == Player.m_localPlayer.GetComponent<ZNetView>() || !nview.name.Contains(piece.m_mustConnectTo.name)) continue;
                if (piece.m_mustBeAboveConnected &&
                    (!Physics.Raycast(ghost.transform.position, Vector3.down, out var below) || below.transform.GetComponentInParent<ZNetView>() != nview))
                    continue;
                return true;
            }
            return false;
        }

        // Player.TestGhostClipping, but returns what the ghost clips into.
        static string Clipping(GameObject ghost, int mask)
        {
            var near = Physics.OverlapSphere(ghost.transform.position, 10f, mask);
            foreach (var own in ghost.GetComponentsInChildren<Collider>())
            foreach (var other in near)
            {
                if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > MaxPenetration)
                    return NameOf(other);
            }
            return null;
        }

        // Things the ghost can collide with that aren't pieces have no display name, so tidy the prefab name.
        internal static string NameOf(Collider col)
        {
            var piece = col.GetComponentInParent<Piece>();
            if (piece != null) return Localization.instance.Localize(piece.m_name);
            if (col.GetComponent<Heightmap>() != null) return "the ground";
            var hoverable = col.GetComponentInParent<Hoverable>();
            string name = hoverable?.GetHoverName();
            if (!string.IsNullOrEmpty(name)) return Localization.instance.Localize(name);
            var root = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.transform.root.gameObject;
            return Regex.Replace(Utils.GetPrefabName(root), @"[_\d]+", " ").Trim().ToLowerInvariant();
        }

        // Extensions want room from each other, and some pieces from certain others (Piece.m_blockingPieces).
        static string MoreSpace(GameObject ghost, Piece piece)
        {
            Vector3 pos = ghost.transform.position;
            var ext = ghost.GetComponent<StationExtension>();
            if (ext != null && piece.m_spaceRequirement > 0f)
            {
                var all = StationInfo.AllExtensions();
                foreach (var other in all)
                    if (other != null && other != ext && Vector3.Distance(other.transform.position, pos) < piece.m_spaceRequirement)
                        return $"too close to {Localization.instance.Localize(other.GetComponent<Piece>()?.m_name ?? other.name)}, extensions need {piece.m_spaceRequirement:0.#} m between them";
            }
            if (piece.m_blockRadius > 0f && piece.m_blockingPieces.Count > 0)
                foreach (var col in Physics.OverlapSphere(pos, piece.m_blockRadius, LayerMask.GetMask("piece")))
                {
                    var other = col.GetComponentInParent<Piece>();
                    if (other == null || other == piece) continue;
                    foreach (var blocking in piece.m_blockingPieces)
                        if (blocking.m_name == other.m_name)
                            return $"too close to {Localization.instance.Localize(other.m_name)}, needs {piece.m_blockRadius:0.#} m";
                }
            return null;
        }

        static string Biomes(Heightmap.Biome mask)
        {
            var names = new List<string>();
            foreach (Heightmap.Biome b in Enum.GetValues(typeof(Heightmap.Biome)))
                if (Mathf.IsPowerOfTwo((int)b) && (mask & b) != 0)
                    names.Add(Localization.instance.Localize("$biome_" + b.ToString().ToLower()));
            return names.Count > 0 ? string.Join(", ", names) : "another biome";
        }

        static string Msg(string key) => Localization.instance.Localize(key);
    }
}
