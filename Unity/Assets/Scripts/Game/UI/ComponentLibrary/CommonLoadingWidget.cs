using TMPro;
using UnityEngine;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class CommonLoadingWidget : AExUIWidget
    {
        [SerializeField]
        private RectTransform m_Spinner = null;

        [SerializeField]
        private TMP_Text m_Label = null;

        [SerializeField]
        private float m_DegreesPerSecond = 180f;

        public void SetLabel(string label)
        {
            if (m_Label == null)
            {
                return;
            }

            m_Label.text = label ?? string.Empty;
            m_Label.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }

        protected internal override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            if (m_Spinner != null)
            {
                m_Spinner.localRotation = Quaternion.identity;
            }
        }

        protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (m_Spinner != null)
            {
                m_Spinner.Rotate(0f, 0f, -m_DegreesPerSecond * realElapseSeconds, Space.Self);
            }
        }
    }
}
