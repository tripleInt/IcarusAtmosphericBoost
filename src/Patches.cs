using HarmonyLib;

namespace IcarusAtmosphericBoost
{
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.GameTick))]
    internal static class ControllerTickPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerController __instance)
        {
            BoostRuntime.BeginTick(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerController), "GetInput")]
    internal static class ChargeInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerController __instance) { BoostRuntime.SeparateChargeFromClimb(__instance); }
    }

    [HarmonyPatch(typeof(PlayerMove_Fly), nameof(PlayerMove_Fly.UseThrustEnergy))]
    internal static class TakeoffThrustPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerMove_Fly __instance, ref float force)
        {
            BoostRuntime.PreserveTakeoffCoast(__instance.controller, ref force);
        }
    }

    [HarmonyPatch(typeof(PlayerMove_Sail), nameof(PlayerMove_Sail.GameTick))]
    internal static class SailPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerMove_Sail __instance) { BoostRuntime.BeforeSail(__instance.controller); }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.ApplyLocalForce))]
    internal static class ForcePatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerController __instance, ref VectorLF3 ____lForce, ref bool ___rigidbodySleep)
        {
            BoostRuntime.BeforeForce(__instance, ref ____lForce, ref ___rigidbodySleep);
        }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.UpdatePhysicsDirect))]
    internal static class PhysicsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerController __instance) { BoostRuntime.CorrectPosition(__instance); }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Free))]
    internal static class FreePatch
    {
        [HarmonyPrefix]
        private static void Prefix() { BoostRuntime.Reset(); }
    }
}
