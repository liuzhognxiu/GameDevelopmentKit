using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public enum ResourceChipState
    {
        Normal,
        Warning,
        Terminal,
    }

    public sealed class ResourceChipView
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public string Detail = string.Empty;
        public string Icon = string.Empty;
        public ResourceChipState State = ResourceChipState.Normal;
    }

    [DisallowMultipleComponent]
    public sealed class ResourceChipWidget : MonoBehaviour
    {
        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Text m_IconText = null;

        [SerializeField]
        private Text m_LabelText = null;

        [SerializeField]
        private Text m_ValueText = null;

        [SerializeField]
        private Text m_StateText = null;

        public void Render(ResourceChipView view)
        {
            if (view == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            ResourceChipState state = view.State;
            if (m_IconText != null)
                m_IconText.text = string.IsNullOrEmpty(view.Icon) ? StateIcon(state) : view.Icon;
            if (m_LabelText != null)
                m_LabelText.text = view.Label ?? string.Empty;
            if (m_ValueText != null)
                m_ValueText.text = view.Value ?? string.Empty;
            if (m_StateText != null)
                m_StateText.text = string.IsNullOrEmpty(view.Detail) ? StateLabel(state) : view.Detail;
            if (m_Background != null)
                m_Background.color = StateColor(state);
        }

        public void Clear()
        {
            if (m_IconText != null)
                m_IconText.text = string.Empty;
            if (m_LabelText != null)
                m_LabelText.text = string.Empty;
            if (m_ValueText != null)
                m_ValueText.text = string.Empty;
            if (m_StateText != null)
                m_StateText.text = string.Empty;
            if (m_Background != null)
                m_Background.color = new Color32(35, 43, 50, 255);
            gameObject.SetActive(false);
        }

        private static string StateIcon(ResourceChipState state)
        {
            switch (state)
            {
                case ResourceChipState.Warning:
                    return "!";
                case ResourceChipState.Terminal:
                    return "x";
                default:
                    return "+";
            }
        }

        private static string StateLabel(ResourceChipState state)
        {
            switch (state)
            {
                case ResourceChipState.Warning:
                    return "警告";
                case ResourceChipState.Terminal:
                    return "结束";
                default:
                    return "正常";
            }
        }

        private static Color StateColor(ResourceChipState state)
        {
            switch (state)
            {
                case ResourceChipState.Warning:
                    return new Color32(86, 59, 47, 255);
                case ResourceChipState.Terminal:
                    return new Color32(70, 47, 51, 255);
                default:
                    return new Color32(35, 63, 62, 255);
            }
        }
    }
}
