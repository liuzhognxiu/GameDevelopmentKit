using System.Text;
using Game.Hot.Buqi.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class ItemCardWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        [SerializeField]
        private GameObject m_DetailPanel = null;

        [SerializeField]
        private Text m_DetailText = null;

        public void Render(BattleReplayItemFrame frame, IItemDefinitionProvider definitions)
        {
            Render(frame, definitions, false);
        }

        public void Render(BattleReplayItemFrame frame, IItemDefinitionProvider definitions, bool triggered)
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
                if (m_DetailText != null)
                    m_DetailText.text = FormatEffects(definition);
            }
            if (m_EffectText != null)
                m_EffectText.text = primaryEffect;
            if (m_StatusText != null)
            {
                m_StatusText.text = BuqiText.Format(
                    "{0}格  充能 {1}  冻结 {2}",
                    frame.Size,
                    frame.Charge,
                    frame.FrozenTicks);
            }
            if (m_CooldownFill != null)
                m_CooldownFill.fillAmount = Mathf.Clamp01(frame.Cooldown01);
            if (m_FrozenMarker != null)
                m_FrozenMarker.SetActive(frame.FrozenTicks > 0);
            if (m_Background != null)
                m_Background.color = triggered ? new Color32(229, 176, 71, 255) : SizeColor(frame.Size);
            m_DetailPanel?.SetActive(false);
        }

        public void Clear()
        {
            m_DetailPanel?.SetActive(false);
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_DetailPanel?.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_DetailPanel?.SetActive(false);
        }

        private static string FormatEffects(BuqiItemDefinition definition)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < definition.Effects.Count; index++)
            {
                BuqiEffectSpec effect = definition.Effects[index];
                if (index > 0) builder.Append('\n');
                builder.Append(effect.Trigger).Append(" | ")
                    .Append(effect.Effect).Append(' ').Append(effect.Amount)
                    .Append(" | ").Append(effect.Target);
            }
            return builder.ToString();
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
