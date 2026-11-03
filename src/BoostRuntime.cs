using System;
using UnityEngine;

namespace IcarusAtmosphericBoost
{
    internal static class BoostRuntime
    {
        private const float SailStep = 1f / 60f;
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
            if (!Plugin.Enabled.Value || !CanControl(c) || planet == null || !TryAtmosphere(planet, out float top, out float ceiling))
            {
                Gesture.Reset();
                return;
            }

            Vector3 position = LocalPosition(c);
            float radius = position.magnitude;
            if (c.mecha.thrusterLevel >= 2)
                guarded = false;
            else if (radius < top)
                guarded = true; // Remains armed after release and across UI interruptions.

            bool pressed = CanReadJump(c) && VFInput._jump.pressing;
            bool held = Gesture.Sample(pressed, Time.realtimeSinceStartupAsDouble, Plugin.HoldSeconds.Value);
            boosting = held && radius > 1f && radius < top && !c.player.warping;
        }

        private static bool TryAtmosphere(PlanetData planet, out float top, out float ceiling)
        {
            float height = planet.atmosphereHeight;
            top = planet.realRadius + height;
            ceiling = top - Mathf.Min(5f, height * 0.1f);
            return height > 0.5f && !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private static Vector3 LocalPosition(PlayerController c)
        {
            return c.player.sailing
                ? (Vector3)Maths.QInvRotateLF(c.gameData.relativeRot, c.player.uPosition - c.gameData.relativePos)
                : c.player.position;
        }

        private static Vector3 LocalVelocity(PlayerController c, Vector3 position)
        {
            if (!c.player.sailing)
                return c.velocity;
            VectorLF3 carrier = trackedPlanet.GetUniversalVelocityAtLocalPoint(GameMain.gameTime, position);
            return (Vector3)Maths.QInvRotateLF(c.gameData.relativeRot, c.player.uVelocity - carrier);
        }

        private static void SetVelocity(PlayerController c, Vector3 position, Vector3 velocity)
        {
            c.velocity = velocity;
            if (c.player.sailing)
            {
                c.player.uVelocity = Maths.QRotateLF(c.gameData.relativeRot, velocity)
                    + trackedPlanet.GetUniversalVelocityAtLocalPoint(GameMain.gameTime, position);
                c.actionSail.visual_uvel = Maths.QRotateLF(c.gameData.relativeRot, velocity);
                // The original Sail action can request landing even while boost is active.
                if (c.movementStateInFrame != EMovementState.Sail)
                    c.velocityOnLanding = velocity;
            }
        }

        private static bool CanModify(PlayerController c, out float ceiling)
        {
            ceiling = 0f;
            return c == trackedController && Plugin.Enabled.Value && CanControl(c) && trackedPlanet != null
                && trackedPlanet == c.gameData.localPlanet && TryAtmosphere(trackedPlanet, out _, out ceiling);
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
            float speed = (float)BoostMath.LaunchSpeed(requested, position.magnitude, ceiling, c.mecha.thrusterLevel >= 2);
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

        // Runs before Sail integrates uPosition so a falling Icarus reverses immediately.
        internal static void BeforeSail(PlayerController c)
        {
            if (!c.player.sailing || !CanModify(c, out float ceiling))
                return;
            CorrectPosition(c);
            Vector3 position = LocalPosition(c);
            Vector3 velocity = LocalVelocity(c, position);
            bool launched = boosting && TryPayForBoost(c);
            if (launched)
            {
                Gesture.Consume();
                velocity = BoostVelocity(c, position, velocity, ceiling);
            }
            boosting = false;
            if (guarded && c.mecha.thrusterLevel < 2)
                velocity = LimitVelocity(position, velocity, ceiling, SailStep);
            if (launched || guarded)
                SetVelocity(c, position, velocity);
        }

        // After every movement action, before Unity receives the accumulated force.
        internal static void BeforeForce(PlayerController c, ref VectorLF3 force, ref bool rigidbodySleep)
        {
            if (!CanModify(c, out float ceiling))
                return;
            CorrectPosition(c);
            Vector3 position = LocalPosition(c);
            if (position.sqrMagnitude < 1f)
                return;
            Vector3 velocity = LocalVelocity(c, position);
            // Sail already applied its impulse before integrating movement.
            if (boosting && !c.player.sailing)
                boosting = TryPayForBoost(c);
            if (boosting)
            {
                Gesture.Consume();
                velocity = BoostVelocity(c, position, velocity, ceiling);
                force = VectorLF3.zero; // Fly otherwise overwrites the impulse with hover thrust.
                rigidbodySleep = false;
                c.rigid.WakeUp();
            }
            if (guarded && c.mecha.thrusterLevel < 2)
            {
                float dt = c.player.sailing ? SailStep : Mathf.Max(SailStep, Time.fixedDeltaTime);
                // AddForce is deferred. Include its impulse in the ceiling calculation.
                Vector3 impulse = c.player.sailing ? Vector3.zero : (Vector3)force * Mathf.Clamp(Time.fixedDeltaTime, SailStep, 0.050000004f);
                velocity = LimitVelocity(position, velocity + impulse, ceiling, dt) - impulse;
                if (c.movementStateInFrame == EMovementState.Sail && !c.player.sailing)
                    c.movementStateInFrame = c.player.movementState;
            }
            if (boosting || (guarded && c.mecha.thrusterLevel < 2))
                SetVelocity(c, position, velocity);

            if (boosting && !c.player.sailing && c.mecha.thrusterLevel >= 2)
            {
                float transitionRadius = trackedPlanet.realRadius + Mathf.Min(50f, (trackedPlanet.atmosphereHeight - 1f) * 0.5f);
                if ((position + velocity * Mathf.Max(SailStep, Time.fixedDeltaTime)).magnitude >= transitionRadius)
                    EnterSail(c);
            }
            boosting = false;
        }

        private static void EnterSail(PlayerController c)
        {
            if (c.movementStateInFrame == EMovementState.Sail)
                return; // Vanilla already performed the transition and notifications.
            if (c.cmd.type == ECommand.Build)
            {
                c.cmd.SetNoneCommand();
                c.actionBuild.blueprintMode = EBlueprintMode.None;
            }
            c.movementStateInFrame = EMovementState.Sail;
            c.actionSail.ResetSailState();
            GameCamera.instance?.SyncForSailMode();
            GameMain.gameScenario?.NotifyOnSailModeEnter();
        }

        // Backstop for collisions, save loading, or vanilla Sail's direct integration.
        // Runs before UpdatePhysicsDirect synchronizes local/universal coordinates.
        internal static void CorrectPosition(PlayerController c)
        {
            if (!guarded || !CanModify(c, out float ceiling) || c.mecha.thrusterLevel >= 2)
                return;
            Vector3 position = LocalPosition(c);
            float radius = position.magnitude;
            if (radius <= ceiling || radius < 1f)
                return;
            position *= (ceiling - 0.01f) / radius;
            Vector3 velocity = LocalVelocity(c, position);
            velocity -= position.normalized * Mathf.Max(0f, Vector3.Dot(velocity, position.normalized));
            if (c.player.sailing)
                c.player.uPosition = c.gameData.relativePos + Maths.QRotateLF(c.gameData.relativeRot, position);
            c.player.position = position;
            c.rigid.position = position;
            SetVelocity(c, position, velocity);
        }
    }
}
