using System;
using Game.Hot.Buqi.DemoUI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public enum BuqiDeploySlotVisualState
    {
        Normal,
        Selected,
        Legal,
        Illegal,
        Continuation,
        Locked,
    }

    [DisallowMultipleComponent]
    public sealed class BuqiDeploySlotWidget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IDropHandler
    {
        private static readonly Color normalColor = new Color32(52, 62, 70, 255);
        private static readonly Color selectedColor = new Color32(184, 139, 48, 255);
        private static readonly Color legalColor = new Color32(42, 128, 92, 255);
        private static readonly Color illegalColor = new Color32(153, 57, 57, 255);
        private static readonly Color continuationColor = new Color32(72, 81, 87, 255);
        private static readonly Color lockedColor = new Color32(42, 46, 49, 255);

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Text m_IndexText = null;

        [SerializeField]
        private Text m_ItemText = null;

        [SerializeField]
        private Text m_StateText = null;

        [SerializeField]
        private GameObject m_InvalidSymbol = null;

        private BuqiDeploymentSlotRef m_Slot;
        private Action<BuqiDeploymentSlotRef> m_Click;
        private Action<BuqiDeploymentSlotRef, bool> m_Hover;
        private Action<BuqiDeploymentSlotRef> m_Drop;

        public void Render(
            BuqiDeploymentSlotRef slot,
            string itemName,
            BuqiDeploySlotVisualState state,
            string stateText,
            Action<BuqiDeploymentSlotRef> click,
            Action<BuqiDeploymentSlotRef, bool> hover,
            Action<BuqiDeploymentSlotRef> drop)
        {
            m_Slot = slot;
            m_Click = click;
            m_Hover = hover;
            m_Drop = drop;
            if (m_Background != null)
                m_Background.color = StateColor(state);
            SetText(m_IndexText, GameFramework.Utility.Text.Format(
                slot.Area == BuqiDeploymentArea.Board ? "{0:00}" : "\u4ED3 {0:00}",
                slot.Index + 1));
            SetText(m_ItemText, itemName);
            SetText(m_StateText, ResolveStateText(state, stateText, string.IsNullOrEmpty(itemName)));
            m_InvalidSymbol?.SetActive(state == BuqiDeploySlotVisualState.Illegal);
        }

        public void Clear()
        {
            m_Click = null;
            m_Hover = null;
            m_Drop = null;
            if (m_Background != null)
                m_Background.color = normalColor;
            SetText(m_IndexText, string.Empty);
            SetText(m_ItemText, string.Empty);
            SetText(m_StateText, string.Empty);
            m_InvalidSymbol?.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Hover?.Invoke(m_Slot, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Hover?.Invoke(m_Slot, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            m_Click?.Invoke(m_Slot);
        }

        public void OnDrop(PointerEventData eventData)
        {
            m_Drop?.Invoke(m_Slot);
        }

        private static Color StateColor(BuqiDeploySlotVisualState state)
        {
            switch (state)
            {
                case BuqiDeploySlotVisualState.Selected:
                    return selectedColor;
                case BuqiDeploySlotVisualState.Legal:
                    return legalColor;
                case BuqiDeploySlotVisualState.Illegal:
                    return illegalColor;
                case BuqiDeploySlotVisualState.Continuation:
                    return continuationColor;
                case BuqiDeploySlotVisualState.Locked:
                    return lockedColor;
                default:
                    return normalColor;
            }
        }

        private static string ResolveStateText(
            BuqiDeploySlotVisualState state,
            string stateText,
            bool isEmpty)
        {
            switch (state)
            {
                case BuqiDeploySlotVisualState.Selected:
                    return "\u25C6 \u5DF2\u9009\u62E9";
                case BuqiDeploySlotVisualState.Legal:
                    return "\u2713 \u53EF\u653E\u7F6E";
                case BuqiDeploySlotVisualState.Illegal:
                    return string.IsNullOrEmpty(stateText)
                        ? "\u00D7 \u4E0D\u53EF\u653E\u7F6E"
                        : GameFramework.Utility.Text.Format("\u00D7 {0}", stateText);
                case BuqiDeploySlotVisualState.Continuation:
                    return "\u2194 \u5360\u7528\u5EF6\u7EED";
                case BuqiDeploySlotVisualState.Locked:
                    return "\u25A0 \u5DF2\u9501\u5B9A";
                default:
                    return isEmpty ? "\u7A7A\u4F4D" : "\u5DF2\u5360\u7528";
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
