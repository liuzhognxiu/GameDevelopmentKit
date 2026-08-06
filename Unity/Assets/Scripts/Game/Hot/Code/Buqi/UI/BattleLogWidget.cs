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
                    "T{0:000}  {1}  {2}",
                    battleEvent.Tick,
                    battleEvent.ReasonCode,
                    battleEvent.Amount);
            }
            if (m_Marker != null)
                m_Marker.color = EventColor(battleEvent.ReasonCode);
        }

        public void Clear()
        {
            gameObject.SetActive(false);
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
