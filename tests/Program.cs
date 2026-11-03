using IcarusAtmosphericBoost;

// Deliberately links only the production scalar rules. These checks cannot replace
// a play test of Harmony hooks, Unity physics, camera transitions, or input focus.
int assertions = 0;
int simulations = 0;
const double tolerance = 1e-8;

void Check(bool condition, string reason)
{
    assertions++;
    if (!condition)
        throw new InvalidOperationException(reason);
}

void Near(double actual, double expected, string reason)
{
    Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected)),
        $"{reason}: expected {expected:R}, got {actual:R}");
}

void TestHoldGesture()
{
    const double delay = 0.5;
    var hold = new HoldGesture();
    Check(!hold.Sample(true, 10, delay), "Pressing starts the hold.");
    Check(!hold.Sample(false, 10.49, delay), "A short release cannot launch.");
    Check(!hold.Sample(true, 11, delay), "A second press starts a new hold.");
    Check(!hold.Sample(true, 12, delay), "Holding beyond the threshold cannot launch.");
    Check(hold.Sample(false, 12, delay), "Releasing after the threshold requests one launch.");
    Check(!hold.Sample(false, 12, delay), "Sampling the same release twice cannot launch again.");
    hold.RecordLaunch(12);
    Check(!hold.Sample(true, 13, delay), "A press during cooldown starts timing.");
    Check(!hold.Sample(false, 14.99, delay), "Release before cooldown expires is discarded.");
    Check(!hold.Sample(false, 15, delay), "Cooldown expiration cannot replay a discarded release.");
    Check(!hold.Sample(true, 15, delay), "Press after cooldown starts a new hold.");
    Check(hold.Sample(false, 15.5, delay), "Release after cooldown and hold threshold can launch.");
    Check(!hold.Sample(true, 16, delay), "Press before UI opens.");
    hold.CancelHold();
    Check(!hold.Sample(false, 17, delay), "Release after UI cancellation cannot launch.");
    Check(!hold.Sample(true, 20, delay), "Press before clock rollback.");
    Check(!hold.Sample(true, 19, delay), "Clock rollback restarts timing.");
    Check(!hold.Sample(false, 19.49, delay), "Rollback cannot arm a premature release.");
}

void TestLaunchSpeed()
{
    Near(BoostMath.LaunchSpeed(80, 201, 255), 80, "Low-altitude launch uses the configured speed");
    Near(BoostMath.LaunchSpeed(80, 245, 255), 40, "Locked boost brakes with remaining altitude");
    Near(BoostMath.LaunchSpeed(80, 254.75, 255), 1, "Locked boost slows near the ceiling");
    Near(BoostMath.LaunchSpeed(80, 255, 255), 0, "The ceiling permits no outward boost");
    Near(BoostMath.LaunchSpeed(80, 256, 255), 0, "Being above the ceiling must not create negative launch speed");
}

void TestSphericalBounds()
{
    // Validate the resulting position geometrically, independently of the rule's
    // algebra. Large tangential speeds expose a flat-altitude clamp's weakness.
    var random = new Random(734219);
    foreach (double dt in new[] { 1.0 / 60, 1.0 / 20, 1.0 / 5 })
    {
        foreach (double radius in new[] { 200.0, 240.0, 254.999, 255.0 })
        {
            const double ceiling = 255;
            double maxTangent = BoostMath.MaximumTangentSpeed(radius, ceiling, dt);
            Check(double.IsFinite(maxTangent) && maxTangent >= 0, "Tangential limit must be finite and nonnegative.");
            Near(Math.Sqrt(radius * radius + Math.Pow(maxTangent * dt, 2)), ceiling,
                "Maximum tangent travel must end exactly on the spherical ceiling");

            foreach (double requestedTangent in new[] { 0.0, 10.0, 300.0, 1e9 })
            {
                double tangent = Math.Min(requestedTangent, maxTangent);
                double minRadial = BoostMath.MinimumRadialSpeed(radius, ceiling, tangent, dt);
                double maxRadial = BoostMath.MaximumRadialSpeed(radius, ceiling, tangent, dt);
                double endRadius = Math.Sqrt(Math.Pow(radius + maxRadial * dt, 2) + Math.Pow(tangent * dt, 2));
                Near(endRadius, ceiling, "Maximum radial travel must end on the spherical ceiling");
                Near(Math.Sqrt(Math.Pow(radius + minRadial * dt, 2) + Math.Pow(tangent * dt, 2)), ceiling,
                    "Minimum radial travel must end on the opposite side of the spherical ceiling");
                foreach (double requestedRadial in new[] { -1e9, -1000.0, -80.0, 0.0, 80.0, 300.0, 1e6 })
                {
                    double radial = Math.Clamp(requestedRadial, minRadial, maxRadial);
                    double endpoint = Math.Sqrt(Math.Pow(radius + radial * dt, 2) + Math.Pow(tangent * dt, 2));
                    Check(endpoint <= ceiling + tolerance, $"Endpoint escaped: r={radius}, vr={requestedRadial}, vt={requestedTangent}, dt={dt}.");
                }
            }
        }

        for (int i = 0; i < 1000; i++)
        {
            double ceiling = 50 + random.NextDouble() * 1000;
            double radius = ceiling * (0.1 + random.NextDouble() * 0.9);
            double tangent = Math.Min(random.NextDouble() * 1e6, BoostMath.MaximumTangentSpeed(radius, ceiling, dt));
            double radial = Math.Clamp((random.NextDouble() - 0.5) * 2e8,
                BoostMath.MinimumRadialSpeed(radius, ceiling, tangent, dt),
                BoostMath.MaximumRadialSpeed(radius, ceiling, tangent, dt));
            double endpoint = Math.Sqrt(Math.Pow(radius + radial * dt, 2) + Math.Pow(tangent * dt, 2));
            Check(double.IsFinite(endpoint) && endpoint <= ceiling + tolerance, "Randomized spherical travel exceeded its ceiling.");
        }
    }
}

void Simulate(double dt, double requestedSpeed, double initialTangential)
{
    const double ceiling = 200 + BoostMath.LaunchCeilingAltitude, ground = 201;
    double x = ground, y = 0, vx = -100, vy = initialTangential;
    bool sawLaunch = false, sawDescent = false;
    for (int step = 0; step < 10000; step++)
    {
        double radius = Math.Sqrt(x * x + y * y);
        double ux = x / radius, uy = y / radius;
        double radial = vx * ux + vy * uy;
        double tangent = vx * -uy + vy * ux;
        bool boosting = step == 0;
        if (boosting)
            radial = BoostMath.LaunchSpeed(requestedSpeed, radius, ceiling);
        else
            radial -= 9.8 * dt;
        double tangentLimit = BoostMath.MaximumTangentSpeed(radius, ceiling, dt);
        tangent = Math.Clamp(tangent, -tangentLimit, tangentLimit);
        radial = Math.Clamp(radial, BoostMath.MinimumRadialSpeed(radius, ceiling, Math.Abs(tangent), dt),
            BoostMath.MaximumRadialSpeed(radius, ceiling, Math.Abs(tangent), dt));
        vx = ux * radial - uy * tangent;
        vy = uy * radial + ux * tangent;
        x += vx * dt;
        y += vy * dt;
        double endpoint = Math.Sqrt(x * x + y * y);
        Check(double.IsFinite(endpoint) && endpoint <= ceiling + tolerance,
            $"Simulation escaped at step={step}, dt={dt}, speed={requestedSpeed}, initialTangent={initialTangential}.");
        sawLaunch |= endpoint > ground + 1;
        sawDescent |= !boosting && radial < -1;
        if (endpoint < ground)
        {
            // Minimal ground collision model; no Unity/game behavior is claimed.
            x *= ground / endpoint;
            y *= ground / endpoint;
            vx = vy = 0;
        }
    }
    Check(sawLaunch, "Boost must overcome the initial falling velocity.");
    Check(sawDescent, "A single impulse must allow descent even if the key stays held.");
    simulations++;
}

TestHoldGesture();
TestLaunchSpeed();
TestSphericalBounds();
foreach (double dt in new[] { 1.0 / 60, 1.0 / 20, 1.0 / 5 })
    foreach (double tangent in new[] { 0.0, 30.0, 1e9 })
    {
        Simulate(dt, 80, tangent);
        Simulate(dt, 300, tangent);
    }
Check(BoostMath.LaunchCeilingAltitude < 49, "Launch ceiling must stay below vanilla sailing altitude.");
Console.WriteLine($"PASS: {assertions:N0} assertions; {simulations} simulations of 10,000 steps at 60/20/5 UPS.");
Console.WriteLine("Coverage: release trigger/cooldown/block/clock rollback, near-ceiling deceleration, spherical radial+tangential bounds, coast/gravity, sailing altitude guard.");
