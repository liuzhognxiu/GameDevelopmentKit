using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    public sealed class BuqiMessageOpenData
    {
        public string Message = string.Empty;
        public bool IsError;
        public float DurationSeconds;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiMessageForm : StarForceUIForm
    {
        public const float DefaultDurationSeconds = 2f;

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Text m_KindText = null;

        [SerializeField]
        private Text m_MessageText = null;

        [SerializeField]
        private Image m_ProgressFill = null;

        private float m_RemainingSeconds;
        private float m_DurationSeconds;

        public float RemainingSeconds => m_RemainingSeconds;
        public bool IsError { get; private set; }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);

            if (!(userData is BuqiMessageOpenData data))
            {
                Log.Warning("BuqiMessageForm requires BuqiMessageOpenData.");
                Close();
                return;
            }

            IsError = data.IsError;
            m_DurationSeconds = data.DurationSeconds > 0f ? data.DurationSeconds : DefaultDurationSeconds;
            m_RemainingSeconds = m_DurationSeconds;
            SetText(m_MessageText, data.Message);
            SetText(m_KindText, IsError ? "错误" : "提示");
            ApplyStateColor();
            UpdateProgress();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#else
        protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#endif
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (m_RemainingSeconds <= 0f)
                return;

            m_RemainingSeconds -= Mathf.Max(0f, realElapseSeconds);
            UpdateProgress();
            if (m_RemainingSeconds <= 0f)
                Close();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            m_RemainingSeconds = 0f;
            m_DurationSeconds = 0f;
            IsError = false;
            SetText(m_KindText, string.Empty);
            SetText(m_MessageText, string.Empty);
            if (m_ProgressFill != null)
                m_ProgressFill.fillAmount = 0f;
            base.OnClose(isShutdown, userData);
        }

        private void ApplyStateColor()
        {
            if (m_Background != null)
            {
                m_Background.color = IsError
                    ? new Color32(111, 48, 48, 250)
                    : new Color32(42, 91, 83, 250);
            }
            if (m_KindText != null)
            {
                m_KindText.color = IsError
                    ? new Color32(255, 205, 194, 255)
                    : new Color32(193, 246, 223, 255);
            }
        }

        private void UpdateProgress()
        {
            if (m_ProgressFill != null)
            {
                m_ProgressFill.fillAmount = m_DurationSeconds <= 0f
                    ? 0f
                    : Mathf.Clamp01(m_RemainingSeconds / m_DurationSeconds);
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
