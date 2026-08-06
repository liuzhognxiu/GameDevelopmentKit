using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    public sealed class BuqiItemDetailOpenData
    {
        public BuqiDemoItemView Item;
        public string FullEffectText = string.Empty;
        public string ModificationText = string.Empty;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiItemDetailForm : StarForceUIForm
    {
        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_MetaText = null;

        [SerializeField]
        private Text m_BodyText = null;

        [SerializeField]
        private Text m_ModificationText = null;

        [SerializeField]
        private Button m_CloseButton = null;

        public BuqiItemDetailOpenData CurrentData { get; private set; }

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            BindCloseButton();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            ClearCallbacks();

            if (!(userData is BuqiItemDetailOpenData data))
            {
                Log.Warning("BuqiItemDetailForm requires BuqiItemDetailOpenData.");
                Close();
                return;
            }

            CurrentData = data;
            BindCloseButton();
            Render(data);
        }

        public void OnCloseButtonClick()
        {
            Close();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            ClearCallbacks();
            CurrentData = null;
            ClearVisuals();
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            ClearCallbacks();
            base.OnDestroy();
        }

        private void Render(BuqiItemDetailOpenData data)
        {
            BuqiDemoItemView item = data.Item;
            SetText(m_TitleText, item == null ? "\u88c5\u5907\u8be6\u60c5" : item.Name);
            SetText(
                m_MetaText,
                item == null
                    ? string.Empty
                    : string.Format("{0} \u683c   \u91d1\u5e01 {1}", item.Size, item.Price));
            SetText(
                m_BodyText,
                string.IsNullOrEmpty(data.FullEffectText)
                    ? item == null ? string.Empty : item.Description
                    : data.FullEffectText);
            SetText(
                m_ModificationText,
                string.IsNullOrEmpty(data.ModificationText) ? "\u65e0\u6539\u9020" : data.ModificationText);
        }

        private void BindCloseButton()
        {
            if (m_CloseButton == null)
                return;

            m_CloseButton.onClick.RemoveListener(OnCloseButtonClick);
            m_CloseButton.onClick.AddListener(OnCloseButtonClick);
        }

        private void ClearCallbacks()
        {
            if (m_CloseButton != null)
                m_CloseButton.onClick.RemoveListener(OnCloseButtonClick);
        }

        private void ClearVisuals()
        {
            SetText(m_TitleText, string.Empty);
            SetText(m_MetaText, string.Empty);
            SetText(m_BodyText, string.Empty);
            SetText(m_ModificationText, string.Empty);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
