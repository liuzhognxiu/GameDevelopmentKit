using System;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    public sealed class BuqiConfirmOpenData
    {
        public string Title = string.Empty;
        public string Message = string.Empty;
        public string ConfirmLabel = string.Empty;
        public string CancelLabel = string.Empty;
        public Action Confirm;
        public Action Cancel;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiConfirmForm : StarForceUIForm
    {
        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_MessageText = null;

        [SerializeField]
        private Button m_ConfirmButton = null;

        [SerializeField]
        private Button m_CancelButton = null;

        [SerializeField]
        private Text m_ConfirmText = null;

        [SerializeField]
        private Text m_CancelText = null;

        private Action m_OnConfirm;
        private Action m_OnCancel;

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            BindButtons();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            ClearCallbacks();

            if (!(userData is BuqiConfirmOpenData data))
            {
                Log.Warning("确认窗口缺少打开数据。");
                Close();
                return;
            }

            SetText(m_TitleText, data.Title);
            SetText(m_MessageText, data.Message);
            SetText(m_ConfirmText, string.IsNullOrEmpty(data.ConfirmLabel) ? "\u786e\u8ba4" : data.ConfirmLabel);
            SetText(m_CancelText, string.IsNullOrEmpty(data.CancelLabel) ? "\u53d6\u6d88" : data.CancelLabel);
            m_OnConfirm = data.Confirm;
            m_OnCancel = data.Cancel;
            BindButtons();
        }

        public void OnConfirmButtonClick()
        {
            Action callback = m_OnConfirm;
            ClearCallbacks();
            callback?.Invoke();
            Close();
        }

        public void OnCancelButtonClick()
        {
            Action callback = m_OnCancel;
            ClearCallbacks();
            callback?.Invoke();
            Close();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            ClearCallbacks();
            ClearVisuals();
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            ClearCallbacks();
            base.OnDestroy();
        }

        private void BindButtons()
        {
            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.RemoveListener(OnConfirmButtonClick);
                m_ConfirmButton.onClick.AddListener(OnConfirmButtonClick);
            }
            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.RemoveListener(OnCancelButtonClick);
                m_CancelButton.onClick.AddListener(OnCancelButtonClick);
            }
        }

        private void ClearCallbacks()
        {
            if (m_ConfirmButton != null)
                m_ConfirmButton.onClick.RemoveListener(OnConfirmButtonClick);
            if (m_CancelButton != null)
                m_CancelButton.onClick.RemoveListener(OnCancelButtonClick);
            m_OnConfirm = null;
            m_OnCancel = null;
        }

        private void ClearVisuals()
        {
            SetText(m_TitleText, string.Empty);
            SetText(m_MessageText, string.Empty);
            SetText(m_ConfirmText, string.Empty);
            SetText(m_CancelText, string.Empty);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
