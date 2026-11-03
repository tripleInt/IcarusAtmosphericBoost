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
        private static bool launchRequested;
        private static bool guarded;
        private static double ascentUntil;
        private static float outwardBeforeActions;

        internal static void Reset()
        {
            trackedController = null;
            trackedPlanet = null;
            launchRequested = guarded = false;
            ascentUntil = 0.0;
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
            if (c != trackedController)
            {
                Reset();
                trackedController = c;
            }
            if (planet != trackedPlanet)
            {
                trackedPlanet = planet;
                launchRequested = guarded = false;
                ascentUntil = 0.0;
                Gesture.CancelHold();
            }
            if (!Plugin.Enabled.Value || !CanReadJump(c) || planet == null || c.player.warping)
            {
                launchRequested = false;
                Gesture.CancelHold();
                return;
            }

            if (Gesture.Sample(VFInput._jump.pressing, Time.realtimeSinceStartupAsDouble, Plugin.HoldSeconds.Value))
                launchRequested = InLaunchRange(c);
        }

        // Vanilla flight can enter Sail above 49 m. Leave room for physics rounding.
        private static float Ceiling(PlanetData planet) => planet.realRadius + BoostMath.LaunchCeilingAltitude;

        private static bool CanModify(PlayerController c)
        {
            return c == trackedController && Plugin.Enabled.Value && CanControl(c)
                && trackedPlanet != null && trackedPlanet == c.gameData.localPlanet;
        }

        private static bool InLaunchRange(PlayerController c)
        {
            double radius = c.player.sailing
                ? (c.player.uPosition - trackedPlanet.uPosition).magnitude
                : c.player.position.magnitude;
            return radius > 1.0 && radius - trackedPlanet.realRadius <= Plugin.LaunchRange.Value
                && (c.mecha.thrusterLevel >= 2 || (!c.player.sailing && radius < Ceiling(trackedPlanet)));
        }

        internal static void BeginTick(PlayerController c)
        {
            Refresh(c);
            CorrectPosition(c);
            outwardBeforeActions = 0f;
            if (CanModify(c) && !c.player.sailing && Time.realtimeSinceStartupAsDouble < ascentUntil)
                outwardBeforeActions = Vector3.Dot(c.velocity, c.player.position.normalized);
        }

        internal static void SeparateChargeFromClimb(PlayerController c)
        {
            if (CanModify(c) && CanReadJump(c) && !c.player.warping
                && VFInput._jump.pressing && InLaunchRange(c) && c.input1.y > 0f)
                c.input1.y = 0f;
        }

        internal static void PreserveTakeoffCoast(PlayerController c, ref float force)
        {
            if (CanModify(c) && c.mecha.thrusterLevel >= 2 && outwardBeforeActions > 0f)
                force = 0f;
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
            double reserve = mecha.coreEnergyCap * 0.05;
            energy = Math.Min(energy, reserve);
            mecha.QueryEnergy(energy, out double available, out _);
            if (available < energy || mecha.coreEnergy - energy < reserve)
                return false;
            mecha.UseEnergy(energy);
            mecha.MarkEnergyChange(4, -energy); // Vanilla flight consumption category.
            return true;
        }

        // After the vanilla movement actions, before Unity receives the accumulated force.
        internal static void BeforeForce(PlayerController c, ref VectorLF3 force, ref bool rigidbodySleep)
        {
            if (!CanModify(c) || c.player.sailing)
                return;
            bool unlocked = c.mecha.thrusterLevel >= 2;
            if (unlocked)
                guarded = false;
            Vector3 position = c.player.position;
            float radius = position.magnitude;
            bool requested = launchRequested;
            launchRequested = false;
            if (radius < 1f)
                return;

            float ceiling = Ceiling(trackedPlanet);
            Vector3 velocity = c.velocity;
            Vector3 up = position / radius;
            bool launched = requested && InLaunchRange(c) && TryPayForBoost(c);
            if (launched)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                Gesture.RecordLaunch(now);
                float speed = unlocked ? Plugin.LaunchSpeed.Value
                    : (float)BoostMath.LaunchSpeed(Plugin.LockedLaunchSpeed.Value, radius, ceiling);
                velocity += up * (speed - Vector3.Dot(velocity, up));
                force = VectorLF3.zero;
                rigidbodySleep = false;
                c.rigid.WakeUp();
                guarded = !unlocked;
                if (unlocked)
                    ascentUntil = now + 2.0;
            }

            if (unlocked && (launched || outwardBeforeActions > 0f))
            {
                // Carry the impulse through the initial ballistic ascent.
                // Use the live Rigidbody velocity before vanilla's 50 m/s Fly cap.
                float radial = Vector3.Dot(velocity, up);
                if (!launched && radial < outwardBeforeActions)
                    velocity += up * (outwardBeforeActions - radial);
                rigidbodySleep = false;
                c.velocity = velocity;
                if (launched)
                    PlayIgnition(c);
                return;
            }

            ascentUntil = 0.0;
            if (!guarded)
                return;

            // Preserve vanilla forces during the coast, accounting for their deferred impulse.
            float dt = Mathf.Max(TickStep, Time.fixedDeltaTime);
            Vector3 impulse = (Vector3)force * Mathf.Clamp(Time.fixedDeltaTime, TickStep, 0.050000004f);
            Vector3 nextVelocity = LimitVelocity(position, velocity + impulse, ceiling, dt);
            c.velocity = nextVelocity - impulse;
            if (launched)
                PlayIgnition(c);
            if ((position + nextVelocity * dt).sqrMagnitude < position.sqrMagnitude)
                guarded = false;
        }

        internal static void CorrectPosition(PlayerController c)
        {
            if (!guarded || !CanModify(c) || c.player.sailing || c.mecha.thrusterLevel >= 2)
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

        internal static void BeforeSail(PlayerController c)
        {
            if (!launchRequested || !CanModify(c) || !c.player.sailing)
                return;
            launchRequested = false;
            if (!InLaunchRange(c) || !TryPayForBoost(c))
                return;
            VectorLF3 offset = c.player.uPosition - trackedPlanet.uPosition;
            VectorLF3 up = offset.normalized;
            VectorLF3 local = Maths.QInvRotateLF(trackedPlanet.runtimeRotation, offset);
            VectorLF3 carrier = trackedPlanet.GetUniversalVelocityAtLocalPoint(GameMain.gameTime, local);
            double radial = VectorLF3.Dot(c.player.uVelocity - carrier, up);
            c.player.uVelocity += up * (Plugin.LaunchSpeed.Value - radial);
            BeginSailCoast(c.actionSail);
            Gesture.RecordLaunch(Time.realtimeSinceStartupAsDouble);
            PlayIgnition(c);
        }

        private static void PlayIgnition(PlayerController c)
        {
            PlayerEffect effect = c.player.effect;
            if (effect == null || effect.backEngineEffect == null)
                return;
            foreach (ParticleSystem thruster in effect.backEngineEffect)
            {
                if (thruster != null && thruster.gameObject.activeInHierarchy)
                    thruster.Emit(12);
            }
        }

        private static void BeginSailCoast(PlayerMove_Sail sail)
        {
            // Vanilla reduces orbital correction and gravity compensation after manual input.
            // Let the same timers decay normally so the launch keeps its radial momentum.
            sail.input_aff_1 = 1.0;
            sail.input_aff_2 = 1.0;
        }
    }
}
