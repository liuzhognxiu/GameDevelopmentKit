using Game.Hot.Buqi.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class BattleLogWidget : MonoBehaviour
    {
        [SerializeField]
        private Image m_Marker = null;

        [SerializeField]
        private Text m_ContentText = null;

        public void Render(BattleEvent battleEvent)
        {
            if (battleEvent == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            if (m_ContentText != null)
            {
                m_ContentText.text = BuqiText.Format(
                    "第 {0:000} tick  {1}  {2}",
                    battleEvent.Tick,
                    FormatReason(battleEvent.ReasonCode),
                    battleEvent.Amount);
            }
            if (m_Marker != null)
                m_Marker.color = EventColor(battleEvent.ReasonCode);
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }

        internal static string FormatReason(string reasonCode)
        {
            switch (reasonCode)
            {
                case "Damage":
                    return "伤害";
                case "BurnDamage":
                    return "灼烧伤害";
                case "PoisonDamage":
                    return "中毒伤害";
                case "OvertimeDamage":
                    return "持续伤害";
                case "NoiseAccident":
                    return "过载事故";
                case "BufferGain":
                    return "护体增加";
                case "Heal":
                    return "治疗";
                case "Regen":
                    return "恢复";
                default:
                    return string.IsNullOrEmpty(reasonCode) ? "未知事件" : reasonCode;
            }
        }

        private static Color EventColor(string reasonCode)
        {
            switch (reasonCode)
            {
                case "Damage":
                case "BurnDamage":
                case "PoisonDamage":
                case "OvertimeDamage":
                case "NoiseAccident":
                    return new Color32(218, 83, 75, 255);
                case "BufferGain":
                case "Heal":
                case "Regen":
                    return new Color32(70, 176, 125, 255);
                default:
                    return new Color32(230, 177, 73, 255);
            }
        }
    }
}
