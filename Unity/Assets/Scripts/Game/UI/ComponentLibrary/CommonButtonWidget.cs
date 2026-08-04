using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonButtonWidget : AExUIWidget
    {
        [SerializeField]
        private Button m_Button = null;

        [SerializeField]
        private TMP_Text m_Label = null;

        [SerializeField]
        private Image m_Icon = null;

        [SerializeField]
        private CommonBadgeWidget m_Badge = null;

        private Action m_ClickHandler;

        public Button Button => m_Button;

        public void SetLabel(string label)
        {
            if (m_Label != null)
            {
                m_Label.text = label ?? string.Empty;
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (m_Icon == null)
            {
                return;
            }

            m_Icon.sprite = sprite;
            m_Icon.gameObject.SetActive(sprite != null);
        }

        public void SetInteractable(bool interactable)
        {
            if (m_Button != null)
            {
                m_Button.interactable = interactable;
            }
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
        }

        protected internal override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_Badge?.TryOpen();
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

        private void HandleClick()
        {
            m_ClickHandler?.Invoke();
        }
    }
}
