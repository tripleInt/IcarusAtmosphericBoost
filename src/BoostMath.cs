using System;

namespace IcarusAtmosphericBoost
{
    // Pure scalar calculations, shared with the executable regression checks.
    internal static class BoostMath
    {
        internal const float LaunchCeilingAltitude = 47f;

        internal static double LaunchSpeed(double requested, double radius, double ceiling)
        {
            return Math.Min(requested, Math.Max(0.0, ceiling - radius) * 4.0);
        }

        // |p + v*dt| <= ceiling, including the outward effect of tangential travel.
        internal static double MaximumRadialSpeed(double radius, double ceiling, double tangentSpeed, double dt)
        {
            double tangentStep = tangentSpeed * dt;
            double radialEnd = Math.Sqrt(Math.Max(0.0, ceiling * ceiling - tangentStep * tangentStep));
            return (radialEnd - radius) / dt;
        }

        internal static double MaximumTangentSpeed(double radius, double ceiling, double dt)
        {
            return Math.Sqrt(Math.Max(0.0, ceiling * ceiling - radius * radius)) / dt;
        }

        internal static double MinimumRadialSpeed(double radius, double ceiling, double tangentSpeed, double dt)
        {
            double tangentStep = tangentSpeed * dt;
            double radialEnd = Math.Sqrt(Math.Max(0.0, ceiling * ceiling - tangentStep * tangentStep));
            return (-radialEnd - radius) / dt;
        }
    }

    internal sealed class HoldGesture
    {
        private double startedAt = double.NaN;
        private bool consumed;

        internal bool Sample(bool pressed, double now, double delay)
        {
            if (!pressed)
            {
                Reset();
                return false;
            }
            if (double.IsNaN(startedAt) || now < startedAt)
                startedAt = now;
            return !consumed && now - startedAt >= delay;
        }

        internal void Consume() { consumed = true; }

        internal void Reset()
        {
            startedAt = double.NaN;
            consumed = false;
        }
    }
}
