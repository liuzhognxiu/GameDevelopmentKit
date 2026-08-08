using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Stages
{
    public abstract class BuqiStageWidgetBase : MonoBehaviour, IBuqiStageWidget
    {
        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_BodyText = null;

        [SerializeField]
        private Text m_MetaText = null;

        [SerializeField]
        private Button[] m_ActionButtons = Array.Empty<Button>();

        [SerializeField]
        private Text[] m_ActionLabels = Array.Empty<Text>();

        [SerializeField]
        private GameObject m_BoardPanel = null;

        [SerializeField]
        private Text[] m_BoardLabels = Array.Empty<Text>();

        private readonly List<BuqiUIDemoCommand> m_Commands = new List<BuqiUIDemoCommand>();
        private readonly List<string> m_Labels = new List<string>();

        public abstract BuqiUIDemoPhase Phase { get; }

        public GameObject Root => gameObject;

        public void Render(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            ClearActions();
            gameObject.SetActive(true);
            SetText(m_TitleText, ResolveTitle(view));
            SetText(m_BodyText, ResolveBody(view));
            SetText(m_MetaText, ResolveMeta(view));
            ConfigureActions(view);
            RenderBoard(view);

            int count = Math.Min(m_ActionButtons.Length, Math.Min(m_ActionLabels.Length, m_Commands.Count));
            for (int index = 0; index < m_ActionButtons.Length; index++)
            {
                bool visible = index < count;
                Button button = m_ActionButtons[index];
                if (button != null)
                    button.gameObject.SetActive(visible);
                if (index < m_ActionLabels.Length && m_ActionLabels[index] != null)
                    m_ActionLabels[index].text = visible ? m_Labels[index] : string.Empty;
                if (!visible || button == null)
                    continue;

                BuqiUIDemoCommand command = m_Commands[index];
                Action<BuqiUIDemoCommand> callback = submit;
                button.onClick.AddListener(() => callback?.Invoke(command));
            }
        }

        public void Clear()
        {
            ClearActions();
            SetText(m_TitleText, string.Empty);
            SetText(m_BodyText, string.Empty);
            SetText(m_MetaText, string.Empty);
            ClearBoard();
            gameObject.SetActive(false);
        }

        protected abstract void ConfigureActions(BuqiUIDemoView view);

        protected void AddAction(string label, BuqiUIDemoCommandType type, string primaryId = "", int slot = -1)
        {
            if (m_Commands.Count >= m_ActionButtons.Length)
                return;
            m_Labels.Add(label ?? string.Empty);
            m_Commands.Add(new BuqiUIDemoCommand
            {
                Type = type,
                PrimaryId = primaryId ?? string.Empty,
                Slot = slot,
            });
        }

        protected virtual string ResolveTitle(BuqiUIDemoView view)
        {
            return view == null ? string.Empty : view.ContextTitle;
        }

        protected virtual string ResolveBody(BuqiUIDemoView view)
        {
            return view == null ? string.Empty : view.ContextBody;
        }

        protected virtual string ResolveMeta(BuqiUIDemoView view)
        {
            return view == null
                ? string.Empty
                : GameFramework.Utility.Text.Format(
                    "第 {0} 回合   金币 {1}   胜场 {2}   单局生命 {3}",
                    view.Round,
                    view.Coins,
                    view.Wins,
                    view.Lives);
        }

        private void ClearActions()
        {
            foreach (Button button in m_ActionButtons)
            {
                if (button == null)
                    continue;
                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
            }
            m_Commands.Clear();
            m_Labels.Clear();
        }

        private void RenderBoard(BuqiUIDemoView view)
        {
            if (m_BoardPanel == null)
                return;

            m_BoardPanel.SetActive(true);
            IReadOnlyList<BuqiDemoItemView> slots = view?.BoardSlots ?? Array.Empty<BuqiDemoItemView>();
            for (int index = 0; index < m_BoardLabels.Length; index++)
            {
                BuqiDemoItemView item = index < slots.Count ? slots[index] : null;
                string label = (index + 1).ToString("00");
                if (item != null && !item.Empty && !string.IsNullOrEmpty(item.Name))
                    label = GameFramework.Utility.Text.Format("{0}\n{1}", label, item.Name);
                SetText(m_BoardLabels[index], label);
            }
        }

        private void ClearBoard()
        {
            foreach (Text label in m_BoardLabels)
                SetText(label, string.Empty);
            if (m_BoardPanel != null)
                m_BoardPanel.SetActive(false);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
