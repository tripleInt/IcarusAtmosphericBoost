using UnityEngine;

namespace IcarusAtmosphericBoost
{
    internal static class BoostFeedback
    {
        internal static void Layer(PlayerController c, int layer, int maximum)
        {
            string speed = (layer * Plugin.SpeedPerLayer.Value).ToString("0.#");
            string text = Localization.isZHCN
                ? $"蓄力 {layer}/{maximum} · {speed} m/s" + (layer == maximum ? " · 松开起飞" : "")
                : $"Charge {layer}/{maximum} · {speed} m/s" + (layer == maximum ? " · Release to launch" : "");
            Show(c, "<color=#80E8FF>" + text + "</color>");
        }

        internal static void Cancel(PlayerController c)
        { Show(c, Localization.isZHCN ? "蓄力已打断" : "Charge cancelled"); }

        internal static void NoEnergy(PlayerController c)
        { Show(c, Localization.isZHCN ? "能量不足，取消弹射" : "Not enough energy to launch"); }

        private static void Show(PlayerController c, string text)
        {
            if (!Plugin.ShowTips.Value || c?.player == null || GameCamera.main == null
                || UIRoot.instance?.uiGame?.generalTips == null || !UIRoot.instance.uiGame.active) return;
            Vector3 position = c.player.position;
            UIRealtimeTip.Popup(text, position + position.normalized * 3f, false, 0);
        }
    }
}
