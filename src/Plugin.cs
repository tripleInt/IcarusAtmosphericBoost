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
        public const string PluginVersion = "2.2.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ChargeSecondsPerLayer;
        internal static ConfigEntry<float> SpeedPerLayer;
        internal static ConfigEntry<bool> ShowTips;
        internal static ConfigEntry<float> BoostPowerMultiplier;
        internal static ConfigEntry<float> LaunchRange;
        private Harmony harmony;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Enable atmospheric boost.");
            ChargeSecondsPerLayer = Config.Bind("General", "ChargeSecondsPerLayer", 0.75f,
                new ConfigDescription("Real seconds per charge layer. Maximum layers equal researched Drive Engine level.", new AcceptableValueRange<float>(0.25f, 3f)));
            SpeedPerLayer = Config.Bind("General", "SpeedPerLayer", 75f,
                new ConfigDescription("Outward launch speed per completed layer in m/s. Existing faster outward momentum is preserved.", new AcceptableValueRange<float>(10f, 150f)));
            ShowTips = Config.Bind("General", "ShowTips", true, "Show charge layers and cancellation above Icarus using vanilla floating tips.");
            LaunchRange = Config.Bind("General", "LaunchRange", 300f,
                new ConfigDescription("Maximum height in metres above the planet surface reference radius at release.", new AcceptableValueRange<float>(50f, 400f)));
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
