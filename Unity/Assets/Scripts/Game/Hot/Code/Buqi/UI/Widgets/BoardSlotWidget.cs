using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class BoardSlotWidget : MonoBehaviour
    {
        private static readonly Color BaseColor = new Color32(36, 43, 51, 255);
        private static readonly Color SelectedColor = new Color32(27, 155, 142, 255);
        private static readonly Color LockedColor = new Color32(80, 82, 90, 255);
        private static readonly Color EmptyColor = new Color32(44, 52, 61, 255);

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Image m_Selection = null;

        [SerializeField]
        private GameObject m_LockedOverlay = null;

        [SerializeField]
        private Text m_NameText = null;

        [SerializeField]
        private Text m_SizeText = null;

        [SerializeField]
        private Text m_SlotText = null;

        [SerializeField]
        private Button m_Button = null;

        public void Render(BuqiDemoItemView view, Action<int> onClick)
        {
            if (view == null)
            {
                Clear();
                return;
            }

            Clear();
            gameObject.SetActive(true);

            if (view.Empty)
            {
                SetText(m_NameText, "EMPTY");
                SetText(m_SizeText, "OPEN SLOT");
                SetText(m_SlotText, FormatSlot(view.Slot));
                SetBackground(EmptyColor);
                if (m_Button != null && view.Slot >= 0 && onClick != null)
                    m_Button.onClick.AddListener(() => onClick(view.Slot));
                return;
            }

            SetText(m_NameText, string.IsNullOrEmpty(view.Name) ? view.Id : view.Name);
            SetText(m_SizeText, view.Locked ? "LOCKED" : string.Format("SIZE {0}", view.Size));
            SetText(m_SlotText, FormatSlot(view.Slot));
            SetBackground(view.Locked ? LockedColor : view.Selected ? SelectedColor : BaseColor);

            if (m_Selection != null)
                m_Selection.gameObject.SetActive(view.Selected);
            if (m_LockedOverlay != null)
                m_LockedOverlay.SetActive(view.Locked);
            if (m_Button != null)
            {
                m_Button.interactable = !view.Locked;
                if (onClick != null)
                    m_Button.onClick.AddListener(() => onClick(view.Slot));
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
            if (m_LockedOverlay != null)
                m_LockedOverlay.SetActive(false);
            SetText(m_NameText, string.Empty);
            SetText(m_SizeText, string.Empty);
            SetText(m_SlotText, string.Empty);
            gameObject.SetActive(false);
        }

        private static string FormatSlot(int slot)
        {
            return slot < 0 ? "SLOT --" : string.Format("SLOT {0:00}", slot + 1);
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
