using System;

namespace IcarusAtmosphericBoost
{
    // Pure scalar calculations, shared with the executable regression checks.
    internal static class BoostMath
    {
        internal const float LaunchCeilingAltitude = 47f;
        internal const float SailHandoffAltitude = 50f;

        internal static bool ShouldHandoff(double altitude, double outwardSpeed)
        {
            return altitude >= SailHandoffAltitude && outwardSpeed > 0.0;
        }

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
        internal const double CooldownSeconds = 3.0;
        private double startedAt = double.NaN;
        private double nextLaunchAt = double.NegativeInfinity;
        private bool blockedUntilRelease;
        internal int Layers { get; private set; }
        internal bool IsCharging => !double.IsNaN(startedAt) && !blockedUntilRelease;

        internal ChargeSample Sample(bool pressed, bool directionPressed, double now, double secondsPerLayer, int maxLayers)
        {
            if (directionPressed || maxLayers <= 0)
                return new ChargeSample(0, 0, Interrupt(pressed));
            if (blockedUntilRelease)
            {
                if (!pressed) blockedUntilRelease = false;
                return default;
            }
            if (double.IsNaN(startedAt))
            {
                if (!pressed || now < nextLaunchAt) return default;
                startedAt = now;
            }
            if (now < startedAt) startedAt = now;
            int previous = Layers;
            Layers = (int)Math.Min(maxLayers, Math.Floor(Math.Max(0, now - startedAt) / secondsPerLayer));
            int added = Math.Max(0, Layers - previous);
            if (pressed)
                return new ChargeSample(added, 0, 0);
            int released = Layers;
            CancelHold();
            return new ChargeSample(added, released, 0);
        }

        internal void RecordLaunch(double now) { nextLaunchAt = now + CooldownSeconds; }

        internal int Interrupt(bool pressed)
        {
            int cancelled = Layers;
            CancelHold();
            blockedUntilRelease = pressed;
            return cancelled;
        }

        internal void CancelHold() { startedAt = double.NaN; Layers = 0; }

        internal void Reset()
        {
            CancelHold();
            blockedUntilRelease = false;
            nextLaunchAt = double.NegativeInfinity;
        }
    }

    internal readonly struct ChargeSample
    {
        internal readonly int AddedLayers, ReleasedLayers, CancelledLayers;
        internal ChargeSample(int added, int released, int cancelled)
        { AddedLayers = added; ReleasedLayers = released; CancelledLayers = cancelled; }
    }

}
