using HarmonyLib;

namespace IcarusAtmosphericBoost
{
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.GameTick))]
    internal static class ControllerTickPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerController __instance)
        {
            BoostRuntime.Refresh(__instance);
            BoostRuntime.CorrectPosition(__instance);
        }
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
