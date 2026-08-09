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
        private Text m_LabelText = null;

        [SerializeField]
        private GameObject m_RefundPreview = null;

        [SerializeField]
        private Text m_RefundText = null;

        private BuqiSellDragSession m_Session;
        private Func<BuqiRunEconomySnapshot> m_CurrentSnapshot;
        private Action<BuqiRunEconomyResult> m_Settled;
        private string m_CommandInstanceId = string.Empty;
        private int m_CommandRefund;
        private Action<string> m_CommandDropped;
        private bool m_CommandOver;

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

        public void BindCommand(string instanceId, int expectedRefund, Action<string> dropped)
        {
            Clear();
            m_CommandInstanceId = instanceId ?? string.Empty;
            m_CommandRefund = Math.Max(0, expectedRefund);
            m_CommandDropped = dropped;
            RenderPreview();
        }

        public void Cancel()
        {
            m_Session?.Cancel();
            ClearCommand();
            RenderPreview();
        }

        public void Clear()
        {
            m_Session?.Cancel();
            m_Session = null;
            m_CurrentSnapshot = null;
            m_Settled = null;
            ClearCommand();
            if (m_Background != null)
                m_Background.color = normalColor;
            SetText(m_LabelText, "拖动道具至此出售");
            m_RefundPreview?.SetActive(false);
            SetText(m_RefundText, string.Empty);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Session?.SetOverSellZone(true);
            m_CommandOver = hasCommand;
            RenderPreview();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Session?.SetOverSellZone(false);
            m_CommandOver = false;
            RenderPreview();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (m_Session == null || m_CurrentSnapshot == null)
            {
                DropCommand();
                return;
            }

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
            bool sessionVisible = m_Session != null && m_Session.PreviewVisible;
            bool commandVisible = hasCommand && m_CommandOver;
            bool visible = sessionVisible || commandVisible;
            int refund = sessionVisible ? m_Session.ExpectedRefund : m_CommandRefund;
            if (m_Background != null)
                m_Background.color = visible ? previewColor : normalColor;
            m_RefundPreview?.SetActive(visible);
            SetText(
                m_RefundText,
                visible
                    ? GameFramework.Utility.Text.Format("获得 {0} 金币", refund)
                    : string.Empty);
        }

        private bool hasCommand =>
            !string.IsNullOrEmpty(m_CommandInstanceId) && m_CommandDropped != null;

        private void DropCommand()
        {
            if (!hasCommand || !m_CommandOver)
                return;

            string instanceId = m_CommandInstanceId;
            Action<string> dropped = m_CommandDropped;
            ClearCommand();
            RenderPreview();
            dropped(instanceId);
        }

        private void ClearCommand()
        {
            m_CommandInstanceId = string.Empty;
            m_CommandRefund = 0;
            m_CommandDropped = null;
            m_CommandOver = false;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
