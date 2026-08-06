using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public enum PhaseStepState
    {
        Available,
        Current,
        Complete,
        Locked,
    }

    public sealed class PhaseStepView
    {
        public BuqiUIDemoPhase Phase;
        public int Index;
        public string Label = string.Empty;
        public PhaseStepState State = PhaseStepState.Available;
        public bool IsCurrent;
        public bool IsVisited;
        public bool IsLocked;
    }

    [DisallowMultipleComponent]
    public sealed class PhaseStepWidget : MonoBehaviour
    {
        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Image m_SelectionOutline = null;

        [SerializeField]
        private Button m_Button = null;

        [SerializeField]
        private Text m_IndexText = null;

        [SerializeField]
        private Text m_LabelText = null;

        [SerializeField]
        private Text m_StateText = null;

        private Action<BuqiUIDemoPhase> m_OnClick;
        private BuqiUIDemoPhase m_Phase;
        private bool m_IsClickable;

        public void Render(PhaseStepView view, Action<BuqiUIDemoPhase> onClick)
        {
            if (view == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            m_Phase = view.Phase;
            m_OnClick = onClick;
            PhaseStepState state = ResolveState(view);
            m_IsClickable = m_OnClick != null && state != PhaseStepState.Locked;

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(HandleClick);
                m_Button.interactable = m_IsClickable;
            }
            if (m_IndexText != null)
                m_IndexText.text = (view.Index > 0 ? view.Index : (int)view.Phase + 1).ToString("00");
            if (m_LabelText != null)
                m_LabelText.text = view.Label ?? string.Empty;
            if (m_StateText != null)
                m_StateText.text = StateLabel(state);
            if (m_Background != null)
                m_Background.color = StateColor(state);
            if (m_SelectionOutline != null)
            {
                m_SelectionOutline.gameObject.SetActive(state == PhaseStepState.Current);
                m_SelectionOutline.color = state == PhaseStepState.Current
                    ? new Color32(229, 176, 71, 255)
                    : new Color32(229, 176, 71, 0);
            }
        }

        public void Clear()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.interactable = false;
            }
            m_OnClick = null;
            m_IsClickable = false;
            if (m_IndexText != null)
                m_IndexText.text = string.Empty;
            if (m_LabelText != null)
                m_LabelText.text = string.Empty;
            if (m_StateText != null)
                m_StateText.text = string.Empty;
            if (m_Background != null)
                m_Background.color = new Color32(51, 62, 70, 255);
            if (m_SelectionOutline != null)
            {
                m_SelectionOutline.gameObject.SetActive(false);
                m_SelectionOutline.color = new Color32(229, 176, 71, 0);
            }
            gameObject.SetActive(false);
        }

        private void HandleClick()
        {
            if (m_IsClickable && m_OnClick != null)
                m_OnClick(m_Phase);
        }

        private static PhaseStepState ResolveState(PhaseStepView view)
        {
            if (view.IsLocked)
                return PhaseStepState.Locked;
            if (view.IsCurrent)
                return PhaseStepState.Current;
            if (view.IsVisited)
                return PhaseStepState.Complete;
            return view.State;
        }

        private static string StateLabel(PhaseStepState state)
        {
            switch (state)
            {
                case PhaseStepState.Current:
                    return ">";
                case PhaseStepState.Complete:
                    return "\u2713";
                case PhaseStepState.Locked:
                    return "\u9501\u5b9a";
                default:
                    return "\u5f85\u8fdb\u5165";
            }
        }

        private static Color StateColor(PhaseStepState state)
        {
            switch (state)
            {
                case PhaseStepState.Current:
                    return new Color32(83, 70, 42, 255);
                case PhaseStepState.Complete:
                    return new Color32(42, 78, 67, 255);
                case PhaseStepState.Locked:
                    return new Color32(39, 44, 49, 255);
                default:
                    return new Color32(51, 62, 70, 255);
            }
        }
    }
}
