using System;
using Game.Hot.Buqi.DemoUI.Interaction;
using Game.Hot.Buqi.Run.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    [DisallowMultipleComponent]
    public sealed class BuqiSellZoneWidget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IDropHandler
    {
        private static readonly Color normalColor = new Color32(62, 67, 72, 255);
        private static readonly Color previewColor = new Color32(42, 128, 92, 255);

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private GameObject m_RefundPreview = null;

        [SerializeField]
        private Text m_RefundText = null;

        private BuqiSellDragSession m_Session;
        private Func<BuqiRunEconomySnapshot> m_CurrentSnapshot;
        private Action<BuqiRunEconomyResult> m_Settled;

        public void Bind(
            BuqiSellDragSession session,
            Func<BuqiRunEconomySnapshot> currentSnapshot,
            Action<BuqiRunEconomyResult> settled)
        {
            Clear();
            m_Session = session;
            m_CurrentSnapshot = currentSnapshot;
            m_Settled = settled;
            RenderPreview();
        }

        public void Cancel()
        {
            m_Session?.Cancel();
            RenderPreview();
        }

        public void Clear()
        {
            m_Session?.Cancel();
            m_Session = null;
            m_CurrentSnapshot = null;
            m_Settled = null;
            if (m_Background != null)
                m_Background.color = normalColor;
            m_RefundPreview?.SetActive(false);
            SetText(m_RefundText, string.Empty);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Session?.SetOverSellZone(true);
            RenderPreview();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Session?.SetOverSellZone(false);
            RenderPreview();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (m_Session == null || m_CurrentSnapshot == null)
                return;

            BuqiRunEconomyResult result = m_Session.Drop(m_CurrentSnapshot());
            Action<BuqiRunEconomyResult> settled = m_Settled;
            m_Session = null;
            m_CurrentSnapshot = null;
            m_Settled = null;
            RenderPreview();
            settled?.Invoke(result);
        }

        private void RenderPreview()
        {
            bool visible = m_Session != null && m_Session.PreviewVisible;
            if (m_Background != null)
                m_Background.color = visible ? previewColor : normalColor;
            m_RefundPreview?.SetActive(visible);
            SetText(
                m_RefundText,
                visible
                    ? GameFramework.Utility.Text.Format("Refund {0}", m_Session.ExpectedRefund)
                    : string.Empty);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
