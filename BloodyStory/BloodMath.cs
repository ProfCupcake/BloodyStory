using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloodyStory
{
    internal static class BloodMath
    {
        private static BloodyStoryModConfig modConfig => ConfigManager.modConfig;
        // Calculates the amount of cumulative damage over the given time
        // calculus innit
        public static double CalculateDmgCum(double dt, double bleedDmg, double regenRate, double regenBoost = 0)
        {
            double num = 2 * bleedDmg * dt - modConfig.bleedHealRate * Math.Pow(dt, 2) - 2 * modConfig.bleedQuotient * regenRate * dt;
            double den = 2 * modConfig.bleedQuotient;
            return (num / den) - Math.Min(dt * modConfig.regenBoostRate, regenBoost);
        }
        // I wrote this because I couldn't find a lerp method in the docs
        // I have since found the lerp method, but this still works so it stays I guess
        public static float Interpolate(float min, float max, float w, float p = 1)
        {
            return (min + (max - min) * (float)Math.Pow(w, p));
        }
    }
}
