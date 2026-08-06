using Game.Hot.Buqi.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class ItemCardWidget : MonoBehaviour
    {
        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Image m_CooldownFill = null;

        [SerializeField]
        private Text m_NameText = null;

        [SerializeField]
        private Text m_EffectText = null;

        [SerializeField]
        private Text m_StatusText = null;

        [SerializeField]
        private GameObject m_FrozenMarker = null;

        public void Render(BattleReplayItemFrame frame, IItemDefinitionProvider definitions)
        {
            if (frame == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            if (m_NameText != null)
                m_NameText.text = frame.DefinitionId;

            string primaryEffect = "--";
            if (definitions != null &&
                definitions.TryGet(frame.DefinitionId, out BuqiItemDefinition definition) &&
                definition.Effects.Count > 0)
            {
                primaryEffect = definition.Effects[0].Effect.ToString();
            }
            if (m_EffectText != null)
                m_EffectText.text = primaryEffect;
            if (m_StatusText != null)
            {
                m_StatusText.text = BuqiText.Format(
                    "{0}\u683C  \u5145\u80FD {1}  \u51BB\u7ED3 {2}",
                    frame.Size,
                    frame.Charge,
                    frame.FrozenTicks);
            }
            if (m_CooldownFill != null)
                m_CooldownFill.fillAmount = Mathf.Clamp01(frame.Cooldown01);
            if (m_FrozenMarker != null)
                m_FrozenMarker.SetActive(frame.FrozenTicks > 0);
            if (m_Background != null)
                m_Background.color = SizeColor(frame.Size);
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }

        private static Color SizeColor(int size)
        {
            switch (size)
            {
                case 2:
                    return new Color32(48, 93, 104, 255);
                case 3:
                    return new Color32(105, 72, 65, 255);
                default:
                    return new Color32(53, 67, 78, 255);
            }
        }
    }
}
