using TMPro;
using UnityEngine;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonBadgeWidget : AExUIWidget
    {
        [SerializeField]
        private TMP_Text m_CountText = null;

        [SerializeField]
        private int m_MaxDisplayValue = 99;

        private int m_Count;

        public int Count => m_Count;

        public void SetCount(int count)
        {
            m_Count = Mathf.Max(0, count);
            RefreshVisual();
        }

        protected internal override void OnInit(object userData)
        {
            base.OnInit(userData);
            RefreshVisual();
        }

        protected internal override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (m_CountText != null)
            {
                m_CountText.text = m_Count > m_MaxDisplayValue ? $"{m_MaxDisplayValue}+" : m_Count.ToString();
            }

            bool visible = m_Count > 0;
            if (Initialized)
            {
                Visible = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
