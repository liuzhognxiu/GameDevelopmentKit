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
                Log.Warning("装备详情窗口缺少打开数据。");
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
            SetText(m_TitleText, item == null ? "装备详情" : item.Name);
            SetText(
                m_MetaText,
                item == null
                    ? string.Empty
                    : GameFramework.Utility.Text.Format("{0} 格   金币 {1}", item.Size, item.Price));
            SetText(
                m_BodyText,
                string.IsNullOrEmpty(data.FullEffectText)
                    ? item == null ? string.Empty : item.Description
                    : data.FullEffectText);
            SetText(
                m_ModificationText,
                string.IsNullOrEmpty(data.ModificationText) ? "无改造" : data.ModificationText);
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
