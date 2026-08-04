using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonProgressBarWidget : AExUIWidget
    {
        [SerializeField]
        private Image m_Fill = null;

        [SerializeField]
        private TMP_Text m_ValueText = null;

        public float NormalizedValue => m_Fill != null ? m_Fill.fillAmount : 0f;

        public void SetNormalizedValue(float value)
        {
            float normalizedValue = Mathf.Clamp01(value);
            if (m_Fill != null)
            {
                m_Fill.fillAmount = normalizedValue;
            }

            if (m_ValueText != null)
            {
                m_ValueText.text = $"{Mathf.RoundToInt(normalizedValue * 100f)}%";
            }
        }

        public void SetValue(float value, float maxValue)
        {
            float normalizedValue = maxValue > 0f ? value / maxValue : 0f;
            if (m_Fill != null)
            {
                m_Fill.fillAmount = Mathf.Clamp01(normalizedValue);
            }

            if (m_ValueText != null)
            {
                m_ValueText.text = $"{Mathf.Max(0f, value):0}/{Mathf.Max(0f, maxValue):0}";
            }
        }

        public void SetValueTextVisible(bool visible)
        {
            if (m_ValueText != null)
            {
                m_ValueText.gameObject.SetActive(visible);
            }
        }
    }
}
