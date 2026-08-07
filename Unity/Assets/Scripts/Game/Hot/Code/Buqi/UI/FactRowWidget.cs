using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class FactRowWidget : MonoBehaviour
    {
        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_BodyText = null;

        [SerializeField]
        private Text m_TickText = null;

        [SerializeField]
        private Image m_Marker = null;

        [SerializeField]
        private Button m_JumpButton = null;

        private Action<int> m_JumpToTickHandler;
        private int m_Tick;

        public void Render(BuqiDemoFactView view, Action<int> onJumpToTick)
        {
            Clear();
            if (view == null)
                return;

            gameObject.SetActive(true);
            m_JumpToTickHandler = onJumpToTick;
            m_Tick = view.Tick;
            SetText(m_TitleText, string.IsNullOrEmpty(view.Title) ? "终局事实" : view.Title);
            SetText(m_BodyText, view.Body);
            SetText(m_TickText, GameFramework.Utility.Text.Format("跳到 T{0:000}", view.Tick));
            if (m_Marker != null)
                m_Marker.color = new Color32(229, 176, 71, 255);

            if (m_JumpButton != null)
            {
                m_JumpButton.interactable = m_JumpToTickHandler != null;
                m_JumpButton.onClick.RemoveAllListeners();
                if (m_JumpToTickHandler != null)
                    m_JumpButton.onClick.AddListener(HandleJumpToTick);
            }
        }

        public void Clear()
        {
            m_JumpToTickHandler = null;
            m_Tick = 0;
            if (m_JumpButton != null)
            {
                m_JumpButton.onClick.RemoveAllListeners();
                m_JumpButton.interactable = false;
            }

            SetText(m_TitleText, string.Empty);
            SetText(m_BodyText, string.Empty);
            SetText(m_TickText, string.Empty);
            if (m_Marker != null)
                m_Marker.color = new Color32(92, 102, 104, 255);
            gameObject.SetActive(false);
        }

        private void HandleJumpToTick()
        {
            m_JumpToTickHandler?.Invoke(m_Tick);
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
