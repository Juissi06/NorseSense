using System;
using HarmonyLib;
using UnityEngine;

namespace NorseSense
{
    // The game hides hover text while a building tool is out (Player.UpdateHover), so a crop or fire you look at
    // with the cultivator or hammer says nothing. This puts the essentials in the info box instead.
    internal static class HoverInfo
    {
        // Null if a game update renamed them; the plant row then leaves out the time.
        static readonly Func<Plant, float> GetGrowTime = Create<Func<Plant, float>>("GetGrowTime");
        static readonly Func<Plant, double> TimeSincePlanted = Create<Func<Plant, double>>("TimeSincePlanted");

        static T Create<T>(string name) where T : Delegate
        {
            try
            {
                return AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(Plant), name));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Norse Sense] Plant timing unavailable ({name}): {e.Message}");
                return null;
            }
        }

        internal static InfoRow Describe(Piece piece)
        {
            if (piece == null) return null;
            var plant = piece.GetComponent<Plant>();
            if (plant != null) return Plant(plant, piece);
            var fire = piece.GetComponent<Fireplace>();
            if (fire != null && Plugin.ShowFireStatus.Value) return Fire(fire, piece);
            return null;
        }

        static InfoRow Plant(Plant plant, Piece piece)
        {
            string name = Localization.instance.Localize(plant.m_name);
            switch (plant.GetStatus())
            {
                case global::Plant.Status.Healthy:
                    return new InfoRow(name, "Growing", ReadyIn(plant), Tone.Good, piece.m_icon);
                case global::Plant.Status.NoSun: return Bad(plant, name, "no open sky", piece);
                case global::Plant.Status.NoSpace: return Bad(plant, name, "too close to something", piece);
                case global::Plant.Status.WrongBiome: return Bad(plant, name, "wrong biome", piece);
                case global::Plant.Status.NotCultivated: return Bad(plant, name, "ground isn't cultivated", piece);
                case global::Plant.Status.NoAttachPiece: return Bad(plant, name, "nothing to climb on", piece);
                case global::Plant.Status.TooHot: return Bad(plant, name, "too hot", piece);
                case global::Plant.Status.TooCold: return Bad(plant, name, "too cold", piece);
                default: return null;
            }
        }

        // Plant.Grow: with m_destroyIfCantGrow the plant dies at grow time, otherwise it just waits.
        static InfoRow Bad(Plant plant, string name, string why, Piece piece) =>
            new InfoRow(name, $"Not growing, {why}", plant.m_destroyIfCantGrow ? "dies when it should be grown" : "waits until fixed",
                Tone.Bad, piece.m_icon);

        static string ReadyIn(Plant plant)
        {
            if (GetGrowTime == null || TimeSincePlanted == null) return null;
            try
            {
                double left = GetGrowTime(plant) - TimeSincePlanted(plant);
                if (left <= 0) return "ready any moment";
                return left < 60 ? "ready in under a minute" : $"ready in about {Math.Ceiling(left / 60):0} min";
            }
            catch (Exception)
            {
                return null; // not networked yet
            }
        }

        static InfoRow Fire(Fireplace fire, Piece piece)
        {
            string name = Localization.instance.Localize(piece.m_name);
            if (fire.m_infiniteFuel) return null;
            var nview = FireStatusPatch.NView(fire);
            if (nview == null || !nview.IsValid()) return null;
            float fuel = nview.GetZDO().GetFloat(ZDOVars.s_fuel);
            string fuelText = $"fuel {fuel:0.#} / {fire.m_maxFuel:0}";
            if (fuel <= 0f) return new InfoRow(name, "Out of fuel", null, Tone.Warn, piece.m_icon);
            string problem = FireStatusPatch.Problem(fire, out Tone tone);
            return problem != null
                ? new InfoRow(name, problem, fuelText, tone, piece.m_icon)
                : new InfoRow(name, "Burning", fuelText, Tone.Good, piece.m_icon);
        }
    }
}
