using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class ChoiceCardWidget : MonoBehaviour
    {
        private static readonly Color BaseColor = new Color32(36, 43, 51, 255);
        private static readonly Color SelectedColor = new Color32(27, 155, 142, 255);
        private static readonly Color DisabledColor = new Color32(70, 73, 80, 255);

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Image m_Selection = null;

        [SerializeField]
        private GameObject m_DisabledOverlay = null;

        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_DescriptionText = null;

        [SerializeField]
        private Text m_CostText = null;

        [SerializeField]
        private Button m_Button = null;

        public void Render(BuqiDemoChoiceView view, Action<string> onClick)
        {
            if (view == null)
            {
                Clear();
                return;
            }

            Clear();
            gameObject.SetActive(true);

            SetText(m_TitleText, view.Selected ? string.Format("[SELECTED] {0}", view.Title) : view.Title);
            SetText(m_DescriptionText, view.Description);
            SetText(m_CostText, view.Disabled ? "UNAVAILABLE" : FormatCost(view.Cost));
            SetBackground(view.Disabled ? DisabledColor : view.Selected ? SelectedColor : BaseColor);

            if (m_Selection != null)
                m_Selection.gameObject.SetActive(view.Selected);
            if (m_DisabledOverlay != null)
                m_DisabledOverlay.SetActive(view.Disabled);
            if (m_Button != null)
            {
                m_Button.interactable = !view.Disabled;
                if (onClick != null && !view.Disabled)
                    m_Button.onClick.AddListener(() => onClick(view.Id));
            }
        }

        public void Clear()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.interactable = true;
            }
            if (m_Background != null)
                m_Background.color = BaseColor;
            if (m_Selection != null)
            {
                m_Selection.color = SelectedColor;
                m_Selection.gameObject.SetActive(false);
            }
            if (m_DisabledOverlay != null)
                m_DisabledOverlay.SetActive(false);
            SetText(m_TitleText, string.Empty);
            SetText(m_DescriptionText, string.Empty);
            SetText(m_CostText, string.Empty);
            gameObject.SetActive(false);
        }

        private static string FormatCost(int cost)
        {
            return cost <= 0 ? "FREE" : string.Format("COST {0}", cost);
        }

        private void SetBackground(Color color)
        {
            if (m_Background != null)
                m_Background.color = color;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
