using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonItemSlotWidget : AExUIWidget
    {
        [SerializeField]
        private Button m_Button = null;

        [SerializeField]
        private Image m_Frame = null;

        [SerializeField]
        private Image m_Icon = null;

        [SerializeField]
        private TMP_Text m_QuantityText = null;

        [SerializeField]
        private GameObject m_Selection = null;

        [SerializeField]
        private GameObject m_LockOverlay = null;

        [SerializeField]
        private CommonBadgeWidget m_Badge = null;

        private Action m_ClickHandler;
        private bool m_Selected;
        private bool m_Locked;

        public void SetIcon(Sprite sprite)
        {
            if (m_Icon == null)
            {
                return;
            }

            m_Icon.sprite = sprite;
            m_Icon.gameObject.SetActive(sprite != null);
        }

        public void SetQuantity(int quantity)
        {
            if (m_QuantityText == null)
            {
                return;
            }

            m_QuantityText.text = Mathf.Max(0, quantity).ToString();
            m_QuantityText.gameObject.SetActive(quantity > 1);
        }

        public void SetQualityColor(Color color)
        {
            if (m_Frame != null)
            {
                m_Frame.color = color;
            }
        }

        public void SetSelected(bool selected)
        {
            m_Selected = selected;
            RefreshState();
        }

        public void SetLocked(bool locked)
        {
            m_Locked = locked;
            RefreshState();
        }

        public void SetBadgeCount(int count)
        {
            m_Badge?.SetCount(count);
        }

        public void SetClickHandler(Action handler)
        {
            m_ClickHandler = handler;
        }

        protected internal override void OnInit(object userData)
        {
            base.OnInit(userData);
            m_Button?.onClick.AddListener(HandleClick);
            RefreshState();
        }

        protected internal override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_Badge?.TryOpen();
            RefreshState();
        }

        protected internal override void OnClose(bool isShutdown, object userData)
        {
            m_ClickHandler = null;
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_Button?.onClick.RemoveListener(HandleClick);
            base.OnDestroy();
        }

        private void RefreshState()
        {
            m_Selection?.SetActive(m_Selected);
            m_LockOverlay?.SetActive(m_Locked);
            if (m_Button != null)
            {
                m_Button.interactable = !m_Locked;
            }
        }

        private void HandleClick()
        {
            m_ClickHandler?.Invoke();
        }
    }
}
