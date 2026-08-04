using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonToggleWidget : AExUIWidget
    {
        [SerializeField]
        private Toggle m_Toggle = null;

        [SerializeField]
        private TMP_Text m_Label = null;

        private Action<bool> m_ValueChangedHandler;

        public bool IsOn => m_Toggle != null && m_Toggle.isOn;

        public void SetLabel(string label)
        {
            if (m_Label != null)
            {
                m_Label.text = label ?? string.Empty;
            }
        }

        public void SetValue(bool value, bool notify = false)
        {
            if (m_Toggle == null)
            {
                return;
            }

            if (notify)
            {
                m_Toggle.isOn = value;
            }
            else
            {
                m_Toggle.SetIsOnWithoutNotify(value);
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (m_Toggle != null)
            {
                m_Toggle.interactable = interactable;
            }
        }

        public void SetValueChangedHandler(Action<bool> handler)
        {
            m_ValueChangedHandler = handler;
        }

        protected internal override void OnInit(object userData)
        {
            base.OnInit(userData);
            m_Toggle?.onValueChanged.AddListener(HandleValueChanged);
        }

        protected internal override void OnClose(bool isShutdown, object userData)
        {
            m_ValueChangedHandler = null;
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_Toggle?.onValueChanged.RemoveListener(HandleValueChanged);
            base.OnDestroy();
        }

        private void HandleValueChanged(bool value)
        {
            m_ValueChangedHandler?.Invoke(value);
        }
    }
}
