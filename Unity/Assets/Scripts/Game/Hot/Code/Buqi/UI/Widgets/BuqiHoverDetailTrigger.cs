using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Hot.Buqi.UI.Widgets
{
    [DisallowMultipleComponent]
    public sealed class BuqiHoverDetailTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField]
        [Min(0.1f)]
        private float m_LongPressSeconds = 0.5f;

        private string m_DetailId = string.Empty;
        private Action<string> m_ShowDetails;
        private Action m_HideDetails;
        private float m_PressDuration;
        private int m_PressPointerId = int.MinValue;
        private bool m_Pressing;
        private bool m_DetailsVisible;
        private bool m_LongPressOpened;

        public void Bind(string detailId, Action<string> showDetails, Action hideDetails)
        {
            HideDetails();
            m_DetailId = detailId ?? string.Empty;
            m_ShowDetails = showDetails;
            m_HideDetails = hideDetails;
            ResetPress();
        }

        public void Clear()
        {
            HideDetails();
            m_DetailId = string.Empty;
            m_ShowDetails = null;
            m_HideDetails = null;
            ResetPress();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId < 0)
                ShowDetails(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideDetails();
            ResetPress();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId < 0)
            {
                ResetPress();
                return;
            }

            m_Pressing = true;
            m_PressPointerId = eventData.pointerId;
            m_PressDuration = 0f;
            m_LongPressOpened = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!m_Pressing || eventData == null || eventData.pointerId != m_PressPointerId)
                return;
            if (m_LongPressOpened)
                HideDetails();
            ResetPress();
        }

        public void AdvancePress(float unscaledDeltaTime)
        {
            if (!m_Pressing || m_LongPressOpened || unscaledDeltaTime <= 0f)
                return;

            m_PressDuration += unscaledDeltaTime;
            if (m_PressDuration < m_LongPressSeconds)
                return;

            m_LongPressOpened = true;
            ShowDetails(true);
        }

        private void Update()
        {
            AdvancePress(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            HideDetails();
            ResetPress();
        }

        private void ShowDetails(bool longPress)
        {
            if (m_DetailsVisible || string.IsNullOrEmpty(m_DetailId) || m_ShowDetails == null)
                return;

            m_DetailsVisible = true;
            m_LongPressOpened |= longPress;
            m_ShowDetails(m_DetailId);
        }

        private void HideDetails()
        {
            if (!m_DetailsVisible)
                return;

            m_DetailsVisible = false;
            m_HideDetails?.Invoke();
        }

        private void ResetPress()
        {
            m_Pressing = false;
            m_PressPointerId = int.MinValue;
            m_PressDuration = 0f;
            m_LongPressOpened = false;
        }
    }
}
