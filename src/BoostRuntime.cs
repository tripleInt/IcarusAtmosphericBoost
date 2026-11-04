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
        private static int requestedLayers;
        private static int maxLayers;
        private static double nextLevelCheck;
        private static bool guarded;
        private static bool coasting;
        private static float outwardBeforeActions;
        private static Vector3 velocityBeforeActions;

        internal static void Reset()
        {
            trackedController = null;
            trackedPlanet = null;
            requestedLayers = maxLayers = 0;
            nextLevelCheck = 0;
            guarded = false;
            coasting = false;
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
                requestedLayers = 0;
                guarded = false;
                coasting = false;
                Gesture.Interrupt(VFInput._jump.pressing);
            }
            if (!Plugin.Enabled.Value || !CanReadJump(c) || planet == null || c.player.warping)
            {
                requestedLayers = 0;
                Gesture.Interrupt(VFInput._jump.pressing);
                if (!Plugin.Enabled.Value || (c != null && (!c.player.isAlive || c.player.warping)))
                    coasting = guarded = false;
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextLevelCheck)
            {
                maxLayers = DriveLevels.Read(c);
                nextLevelCheck = now + 0.5;
            }
            if (!InLaunchRange(c))
            {
                if (Gesture.Interrupt(VFInput._jump.pressing) > 0) BoostFeedback.Cancel(c);
                requestedLayers = 0;
                return;
            }
            bool direction = DirectionPressed();
            if (direction && requestedLayers > 0)
            {
                requestedLayers = 0;
                BoostFeedback.Cancel(c);
            }
            ChargeSample sample = Gesture.Sample(VFInput._jump.pressing, direction, now,
                Plugin.ChargeSecondsPerLayer.Value, maxLayers);
            int lastLayer = sample.ReleasedLayers > 0 ? sample.ReleasedLayers : Gesture.Layers;
            for (int layer = lastLayer - sample.AddedLayers + 1; layer <= lastLayer; layer++)
                BoostFeedback.Layer(c, layer, maxLayers);
            if (sample.CancelledLayers > 0) BoostFeedback.Cancel(c);
            if (sample.ReleasedLayers > 0) requestedLayers = sample.ReleasedLayers;
        }

        private static bool DirectionPressed()
        {
            // Check the four input actions separately, even if opposing keys cancel
            // each other's axis or the player is physically unable to move.
            return VFInput._moveForward.pressing || VFInput._moveBackward.pressing
                || VFInput._moveLeft.pressing || VFInput._moveRight.pressing;
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
            if (CanModify(c) && !c.player.sailing)
            {
                velocityBeforeActions = c.velocity;
                outwardBeforeActions = Vector3.Dot(velocityBeforeActions, c.player.position.normalized);
            }
        }

        internal static void SeparateChargeFromClimb(PlayerController c)
        {
            if (CanModify(c) && CanReadJump(c) && !c.player.warping
                && Gesture.IsCharging && VFInput._jump.pressing && c.input1.y > 0f)
                c.input1.y = 0f;
        }

        internal static void PreserveTakeoffCoast(PlayerController c, ref float force)
        {
            if (CanModify(c) && (coasting || requestedLayers > 0))
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
            int layers = requestedLayers;
            requestedLayers = 0;
            if (radius < 1f)
                return;

            float ceiling = Ceiling(trackedPlanet);
            Vector3 velocity = c.velocity;
            Vector3 up = position / radius;
            bool launched = layers > 0 && InLaunchRange(c) && TryPayForBoost(c);
            if (layers > 0 && !launched) BoostFeedback.NoEnergy(c);
            if (launched)
            {
                Gesture.RecordLaunch(Time.realtimeSinceStartupAsDouble);
                velocity = velocityBeforeActions;
                float speed = unlocked ? Mathf.Max(outwardBeforeActions, layers * Plugin.SpeedPerLayer.Value)
                    : (float)BoostMath.LaunchSpeed(layers * Plugin.SpeedPerLayer.Value, radius, ceiling);
                velocity += up * (speed - Vector3.Dot(velocity, up));
                c.rigid.WakeUp();
                guarded = !unlocked;
                coasting = true;
                if (c.movementStateInFrame < EMovementState.Fly) c.actionWalk.SwitchToFly();
                PlayIgnition(c);
            }

            if (coasting && (launched || outwardBeforeActions > 0f))
            {
                // Initial velocity is assigned once, on release. Subsequent steps
                // preserve the physics result and apply native gravity once.
                if (!launched)
                {
                    if (!DirectionPressed()) velocity = velocityBeforeActions;
                    velocity += up * (outwardBeforeActions - Vector3.Dot(velocity, up));
                }
                force = c.localGravity;
                rigidbodySleep = false;
                c.velocityOnLanding = Vector3.zero;
                if (unlocked)
                {
                    c.velocity = velocity;
                    if (c.movementStateInFrame == EMovementState.Sail
                        || BoostMath.ShouldHandoff(radius - trackedPlanet.realRadius, Vector3.Dot(velocity, up)))
                        HandoffToSail(c);
                    return;
                }
                float radial = Vector3.Dot(velocity, up);
                float safeSpeed = (float)BoostMath.LaunchSpeed(Math.Max(0f, radial), radius, ceiling);
                if (radial > safeSpeed) velocity -= up * (radial - safeSpeed);
            }
            else coasting = false;
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
            if (!CanModify(c) || !c.player.sailing || c.player.warping)
                return;
            int layers = requestedLayers;
            requestedLayers = 0;
            if (layers > 0 && InLaunchRange(c))
            {
                if (TryPayForBoost(c))
                {
                    VectorLF3 offset = c.player.uPosition - trackedPlanet.uPosition;
                    VectorLF3 local = Maths.QInvRotateLF(trackedPlanet.runtimeRotation, offset);
                    VectorLF3 carrier = trackedPlanet.GetUniversalVelocityAtLocalPoint(GameMain.gameTime, local);
                    double radial = VectorLF3.Dot(c.player.uVelocity - carrier, offset.normalized);
                    c.player.uVelocity += offset.normalized * Math.Max(0, layers * Plugin.SpeedPerLayer.Value - radial);
                    BeginSailCoast(c.actionSail);
                    Gesture.RecordLaunch(Time.realtimeSinceStartupAsDouble);
                    PlayIgnition(c);
                }
                else BoostFeedback.NoEnergy(c);
            }
        }

        private static void HandoffToSail(PlayerController c)
        {
            coasting = false;
            if (c.movementStateInFrame != EMovementState.Sail)
            {
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
            BeginSailCoast(c.actionSail);
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
