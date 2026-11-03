using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace IcarusAtmosphericBoost
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.dsp.icarusatmosphericboost";
        public const string PluginName = "Icarus Atmospheric Boost";
        public const string PluginVersion = "2.1.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> HoldSeconds;
        internal static ConfigEntry<float> LaunchSpeed;
        internal static ConfigEntry<float> LockedLaunchSpeed;
        internal static ConfigEntry<float> BoostPowerMultiplier;
        internal static ConfigEntry<float> LaunchRange;
        private Harmony harmony;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Enable atmospheric boost.");
            HoldSeconds = Config.Bind("General", "HoldSeconds", 0.8f,
                new ConfigDescription("Minimum time to hold jump before releasing to launch.", new AcceptableValueRange<float>(0.1f, 2f)));
            LaunchRange = Config.Bind("General", "LaunchRange", 300f,
                new ConfigDescription("Maximum height in metres above the planet surface reference radius at release.", new AcceptableValueRange<float>(50f, 400f)));
            LaunchSpeed = Config.Bind("General", "LaunchSpeed", 300f,
                new ConfigDescription("Outward speed in m/s after interstellar sailing is unlocked.", new AcceptableValueRange<float>(20f, 1000f)));
            LockedLaunchSpeed = Config.Bind("General", "LockedLaunchSpeed", 80f,
                new ConfigDescription("Maximum outward speed before sailing is unlocked. Capped below vanilla sailing altitude.", new AcceptableValueRange<float>(10f, 300f)));
            BoostPowerMultiplier = Config.Bind("General", "BoostPowerMultiplier", 2f,
                new ConfigDescription("Energy per launch in seconds of vanilla low-altitude hovering. Zero disables the extra cost.", new AcceptableValueRange<float>(0f, 20f)));
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Hold and release jump to launch.");
        }

        private void Update() { BoostRuntime.Refresh(GameMain.mainPlayer == null ? null : GameMain.mainPlayer.controller); }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            BoostRuntime.Reset();
        }
    }
}
