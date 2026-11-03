using System;
using UnityEngine;

namespace IcarusAtmosphericBoost
{
    internal static class BoostRuntime
    {
        private const float TickStep = 1f / 60f;
        private static readonly HoldGesture Gesture = new HoldGesture();
        private static PlayerController trackedController;
        private static PlanetData trackedPlanet;
        private static bool boosting;
        private static bool guarded;

        internal static void Reset()
        {
            trackedController = null;
            trackedPlanet = null;
            boosting = guarded = false;
            Gesture.Reset();
        }

        private static bool IsLocal(PlayerController c)
        {
            return c != null && c.player != null && c.player == GameMain.mainPlayer && c.gameData != null;
        }

        private static bool CanControl(PlayerController c)
        {
            return IsLocal(c) && GameMain.isRunning && !GameMain.isPaused && !GameMain.isFullscreenPaused
                && !DSPGame.IsMenuDemo && !c.gameData.disableController && !c.gameData.guideRunning
                && c.player.isAlive && !c.player.respawning;
        }

        private static bool CanReadJump(PlayerController c)
        {
            if (!CanControl(c) || !Application.isFocused || VFInput.inputing || VFInput.inFullscreenGUI
                || VFInput.inCombatScreenGUI || VFInput.inScreenshotMode || UIGame.viewMode >= EViewMode.Globe)
                return false;
            bool buildLocksMovement = (c.cmd.type == ECommand.Build && !PlayerController.operationWhenBuild)
                || (c.actionBuild != null && c.actionBuild.blueprintMode > EBlueprintMode.None);
            return !buildLocksMovement || VFInput._godModeMechaMove;
        }

        internal static void Refresh(PlayerController c)
        {
            if (c != null && !IsLocal(c))
                return;
            PlanetData planet = IsLocal(c) ? c.gameData.localPlanet : null;
            if (c != trackedController || planet != trackedPlanet)
            {
                Reset();
                trackedController = c;
                trackedPlanet = planet;
            }
            boosting = false;
            if (!Plugin.Enabled.Value || !CanControl(c) || planet == null || c.player.sailing)
            {
                if (c != null && c.player.sailing)
                    guarded = false;
                Gesture.Reset();
                return;
            }

            float radius = c.player.position.magnitude;
            bool pressed = CanReadJump(c) && VFInput._jump.pressing;
            bool held = Gesture.Sample(pressed, Time.realtimeSinceStartupAsDouble, Plugin.HoldSeconds.Value);
            boosting = held && radius > 1f && radius < Ceiling(planet);
        }

        // Vanilla flight can enter Sail above 49 m. Leave room for physics rounding.
        private static float Ceiling(PlanetData planet) => planet.realRadius + BoostMath.LaunchCeilingAltitude;

        private static bool CanModify(PlayerController c)
        {
            return c == trackedController && Plugin.Enabled.Value && CanControl(c) && !c.player.sailing
                && trackedPlanet != null && trackedPlanet == c.gameData.localPlanet;
        }

        private static Vector3 LimitVelocity(Vector3 position, Vector3 velocity, float ceiling, float dt)
        {
            float radius = position.magnitude;
            Vector3 up = position / radius;
            float radial = Vector3.Dot(velocity, up);
            Vector3 tangent = velocity - up * radial;
            double maxTangent = BoostMath.MaximumTangentSpeed(radius, ceiling, dt);
            if (tangent.magnitude > maxTangent)
                tangent = tangent.normalized * (float)maxTangent;
            double maxRadial = BoostMath.MaximumRadialSpeed(radius, ceiling, tangent.magnitude, dt);
            double minRadial = BoostMath.MinimumRadialSpeed(radius, ceiling, tangent.magnitude, dt);
            return tangent + up * (float)Math.Max(minRadial, Math.Min(radial, maxRadial));
        }

        private static Vector3 BoostVelocity(PlayerController c, Vector3 position, Vector3 velocity, float ceiling)
        {
            Vector3 up = position.normalized;
            float requested = c.mecha.thrusterLevel >= 2 ? Plugin.LaunchSpeed.Value : Plugin.LockedLaunchSpeed.Value;
            float speed = (float)BoostMath.LaunchSpeed(requested, position.magnitude, ceiling);
            return velocity + up * (speed - Vector3.Dot(velocity, up));
        }

        private static bool TryPayForBoost(PlayerController c)
        {
            Mecha mecha = c.mecha;
            // Match vanilla hover: thrust sufficient to counter gravity at low vertical speed.
            double hoverPower = mecha.thrustPowerPerAcc * c.universalGravity.magnitude * mecha.reactorPowerConsRatio;
            double energy = hoverPower * Plugin.BoostPowerMultiplier.Value; // One burst, priced in hover-seconds.
            if (double.IsNaN(energy) || double.IsInfinity(energy))
                return false;
            if (energy <= 0.0)
                return true;
            mecha.QueryEnergy(energy, out double available, out _);
            if (available < energy)
                return false;
            mecha.UseEnergy(energy);
            mecha.MarkEnergyChange(4, -energy); // Vanilla flight consumption category.
            return true;
        }

        // After the vanilla movement actions, before Unity receives the accumulated force.
        internal static void BeforeForce(PlayerController c, ref VectorLF3 force, ref bool rigidbodySleep)
        {
            if (!CanModify(c) || (!boosting && !guarded))
                return;
            Vector3 position = c.player.position;
            float radius = position.magnitude;
            if (radius < 1f)
                return;

            float ceiling = Ceiling(trackedPlanet);
            Vector3 velocity = c.velocity;
            if (boosting && c.movementStateInFrame != EMovementState.Sail && radius < ceiling && TryPayForBoost(c))
            {
                Gesture.Consume();
                velocity = BoostVelocity(c, position, velocity, ceiling);
                force = VectorLF3.zero;
                rigidbodySleep = false;
                c.rigid.WakeUp();
                guarded = true;
            }
            boosting = false;
            if (!guarded)
                return;

            // Preserve vanilla forces during the coast, accounting for their deferred impulse.
            float dt = Mathf.Max(TickStep, Time.fixedDeltaTime);
            Vector3 impulse = (Vector3)force * Mathf.Clamp(Time.fixedDeltaTime, TickStep, 0.050000004f);
            Vector3 nextVelocity = LimitVelocity(position, velocity + impulse, ceiling, dt);
            c.velocity = nextVelocity - impulse;
            if ((position + nextVelocity * dt).sqrMagnitude < position.sqrMagnitude)
                guarded = false;
        }

        internal static void CorrectPosition(PlayerController c)
        {
            if (!guarded || !CanModify(c))
                return;
            Vector3 position = c.player.position;
            float radius = position.magnitude;
            float ceiling = Ceiling(trackedPlanet);
            if (radius <= ceiling)
                return;
            Vector3 up = position / radius;
            position = up * (ceiling - 0.01f);
            c.player.position = position;
            c.rigid.position = position;
            c.velocity -= up * Mathf.Max(0f, Vector3.Dot(c.velocity, up));
        }
    }
}
