using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Demo;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class BattleForm : StarForceUIForm
    {
        private const int VisibleLogRows = 12;

        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_LeftNameText = null;

        [SerializeField]
        private Text m_RightNameText = null;

        [SerializeField]
        private Text m_LeftStatsText = null;

        [SerializeField]
        private Text m_RightStatsText = null;

        [SerializeField]
        private Text m_TickText = null;

        [SerializeField]
        private Text m_CurrentEventText = null;

        [SerializeField]
        private Text m_OutcomeText = null;

        [SerializeField]
        private Text m_PageText = null;

        [SerializeField]
        private Text m_PlayPauseText = null;

        [SerializeField]
        private Text[] m_FactTexts = Array.Empty<Text>();

        [SerializeField]
        private ItemCardWidget[] m_LeftCards = Array.Empty<ItemCardWidget>();

        [SerializeField]
        private ItemCardWidget[] m_RightCards = Array.Empty<ItemCardWidget>();

        [SerializeField]
        private BattleLogWidget[] m_LogRows = Array.Empty<BattleLogWidget>();

        [SerializeField]
        private Image m_TimelineFill = null;

        [SerializeField]
        private GameObject m_ErrorPanel = null;

        [SerializeField]
        private Text m_ErrorText = null;

        [SerializeField]
        private Button m_BackButton = null;

        [SerializeField]
        private Button m_PlayPauseButton = null;

        [SerializeField]
        private Button m_Speed1Button = null;

        [SerializeField]
        private Button m_Speed2Button = null;

        [SerializeField]
        private Button m_Speed4Button = null;

        [SerializeField]
        private Button m_SkipButton = null;

        [SerializeField]
        private Button m_ReplayButton = null;

        [SerializeField]
        private Button m_PreviousPageButton = null;

        [SerializeField]
        private Button m_NextPageButton = null;

        private BattleReplayController m_Controller;
        private int m_LogPage;

        public bool HasReplay => m_Controller != null;

        public int ReplaySpeed => m_Controller == null ? 0 : m_Controller.Speed;

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            m_BackButton?.onClick.AddListener(Close);
            m_PlayPauseButton?.onClick.AddListener(TogglePause);
            m_Speed1Button?.onClick.AddListener(SetSpeed1);
            m_Speed2Button?.onClick.AddListener(SetSpeed2);
            m_Speed4Button?.onClick.AddListener(SetSpeed4);
            m_SkipButton?.onClick.AddListener(SkipToEnd);
            m_ReplayButton?.onClick.AddListener(Replay);
            m_PreviousPageButton?.onClick.AddListener(PreviousPage);
            m_NextPageButton?.onClick.AddListener(NextPage);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            m_LogPage = 0;
            if (!TryResolveReplay(userData, out BattleReplayData replay, out string error))
            {
                m_Controller = null;
                ShowError(error);
                return;
            }

            try
            {
                m_Controller = new BattleReplayController(replay);
                HideError();
                Render();
            }
            catch (Exception exception)
            {
                m_Controller = null;
                ShowError(exception.Message);
            }
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#else
        protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#endif
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            if (m_Controller == null)
                return;
            m_Controller.Advance(realElapseSeconds);
            Render();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            m_Controller = null;
            m_LogPage = 0;
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_BackButton?.onClick.RemoveListener(Close);
            m_PlayPauseButton?.onClick.RemoveListener(TogglePause);
            m_Speed1Button?.onClick.RemoveListener(SetSpeed1);
            m_Speed2Button?.onClick.RemoveListener(SetSpeed2);
            m_Speed4Button?.onClick.RemoveListener(SetSpeed4);
            m_SkipButton?.onClick.RemoveListener(SkipToEnd);
            m_ReplayButton?.onClick.RemoveListener(Replay);
            m_PreviousPageButton?.onClick.RemoveListener(PreviousPage);
            m_NextPageButton?.onClick.RemoveListener(NextPage);
            base.OnDestroy();
        }

        private static bool TryResolveReplay(object userData, out BattleReplayData replay, out string error)
        {
            if (userData is BattleReplayData supplied)
            {
                replay = supplied;
                error = string.Empty;
                return true;
            }

            if (HotEntry.Tables == null)
            {
                replay = null;
                error = "不器配置表尚未初始化。";
                return false;
            }
            if (!BuqiGeneratedConfigAdapter.TryReadFromTables(
                    HotEntry.Tables,
                    out BuqiConfigCatalog catalog,
                    out List<string> adapterErrors))
            {
                replay = null;
                error = string.Join("\n", adapterErrors);
                return false;
            }
            return BuqiBattleDemoFactory.TryCreate(catalog, out replay, out error);
        }

        private void TogglePause()
        {
            if (m_Controller == null)
                return;
            m_Controller.SetPaused(!m_Controller.IsPaused);
            Render();
        }

        private void SetSpeed1()
        {
            SetSpeed(1);
        }

        private void SetSpeed2()
        {
            SetSpeed(2);
        }

        private void SetSpeed4()
        {
            SetSpeed(4);
        }

        private void SetSpeed(int speed)
        {
            if (m_Controller == null)
                return;
            m_Controller.SetSpeed(speed);
            Render();
        }

        private void SkipToEnd()
        {
            if (m_Controller == null)
                return;
            m_Controller.SkipToEnd();
            m_LogPage = Math.Max(0, m_Controller.GetLogPage(int.MaxValue).PageCount - 1);
            Render();
        }

        private void Replay()
        {
            if (m_Controller == null)
                return;
            m_Controller.Replay();
            m_LogPage = 0;
            Render();
        }

        private void PreviousPage()
        {
            m_LogPage = Math.Max(0, m_LogPage - 1);
            Render();
        }

        private void NextPage()
        {
            if (m_Controller == null)
                return;
            m_LogPage = Math.Min(m_Controller.GetLogPage(m_LogPage).PageCount - 1, m_LogPage + 1);
            Render();
        }

        private void Render()
        {
            BattleReplayFrame frame = m_Controller.Frame;
            BattleReplayData data = m_Controller.Data;
            SetText(m_TitleText, data.Title);
            SetText(m_LeftNameText, data.LeftName);
            SetText(m_RightNameText, data.RightName);
            SetText(m_LeftStatsText, FormatStats(frame.Left));
            SetText(m_RightStatsText, FormatStats(frame.Right));
            SetText(m_TickText, BuqiText.Format("第 {0:000} tick / {1:000}", frame.Tick, data.Result.DurationTicks));
            SetText(m_CurrentEventText, FormatCurrentEvent(frame.CurrentEvent));
            SetText(m_OutcomeText, frame.IsFinished ? FormatOutcome(data.Result) : "\u6218\u6597\u63A8\u6F14\u4E2D");
            SetText(m_PlayPauseText, m_Controller.IsPaused ? "\u7EE7\u7EED" : "\u6682\u505C");

            if (m_TimelineFill != null)
            {
                m_TimelineFill.fillAmount = data.Result.DurationTicks > 0
                    ? Mathf.Clamp01((float)frame.Tick / data.Result.DurationTicks)
                    : 0f;
            }

            RenderTrack(m_LeftCards, frame.Left, data.Definitions);
            RenderTrack(m_RightCards, frame.Right, data.Definitions);
            RenderLogs(frame);
            RenderFacts(frame.IsFinished);
            RenderSpeedState();
            if (string.IsNullOrEmpty(frame.Error))
                HideError();
            else
                ShowError(frame.Error);
        }

        private static void RenderTrack(
            ItemCardWidget[] cards,
            BattleReplaySideFrame side,
            IItemDefinitionProvider definitions)
        {
            foreach (ItemCardWidget card in cards)
                card?.Clear();
            foreach (BattleReplayItemFrame item in side.Items)
            {
                if (item.AnchorSlot >= 0 && item.AnchorSlot < cards.Length)
                    cards[item.AnchorSlot]?.Render(item, definitions);
            }
        }

        private void RenderLogs(BattleReplayFrame frame)
        {
            foreach (BattleLogWidget row in m_LogRows)
                row?.Clear();

            if (!frame.IsFinished)
            {
                var visible = new List<BattleEvent>(VisibleLogRows);
                for (int index = m_Controller.Data.Log.Count - 1; index >= 0 && visible.Count < VisibleLogRows; index--)
                {
                    BattleEvent battleEvent = m_Controller.Data.Log[index];
                    if (battleEvent.Tick <= frame.Tick)
                        visible.Add(battleEvent);
                }
                visible.Reverse();
                for (int index = 0; index < visible.Count && index < m_LogRows.Length; index++)
                    m_LogRows[index]?.Render(visible[index]);
                SetText(m_PageText, "实时");
                return;
            }

            BattleReplayLogPage page = m_Controller.GetLogPage(m_LogPage);
            m_LogPage = page.PageIndex;
            for (int index = 0; index < page.Rows.Count && index < m_LogRows.Length; index++)
                m_LogRows[index]?.Render(page.Rows[index].Event);
            SetText(m_PageText, BuqiText.Format("{0}/{1}", page.PageIndex + 1, page.PageCount));
        }

        private void RenderFacts(bool visible)
        {
            IReadOnlyList<BattleReplayFact> facts = m_Controller.GetFacts();
            for (int index = 0; index < m_FactTexts.Length; index++)
            {
                string value = visible && index < facts.Count ? facts[index].Summary : "--";
                SetText(m_FactTexts[index], value);
            }
        }

        private void RenderSpeedState()
        {
            Color selected = new Color32(230, 177, 73, 255);
            Color normal = new Color32(59, 72, 82, 255);
            SetButtonColor(m_Speed1Button, m_Controller.Speed == 1 ? selected : normal);
            SetButtonColor(m_Speed2Button, m_Controller.Speed == 2 ? selected : normal);
            SetButtonColor(m_Speed4Button, m_Controller.Speed == 4 ? selected : normal);
        }

        private static string FormatStats(BattleReplaySideFrame side)
        {
            return BuqiText.Format(
                "\u751F\u547D\u503C {0}/{1}   \u62A4\u76FE {2}   \u8FC7\u8F7D {3}/10",
                side.Execution,
                side.MaxExecution,
                side.Buffer,
                side.Noise);
        }

        private static string FormatCurrentEvent(BattleEvent battleEvent)
        {
            return battleEvent == null
                ? "\u5C1A\u65E0\u4E8B\u4EF6"
                : BuqiText.Format("第 {0} tick  {1}  {2}", battleEvent.Tick, FormatReason(battleEvent.ReasonCode), battleEvent.Amount);
        }

        private static string FormatOutcome(BattleResult result)
        {
            return BuqiText.Format("{0}  |  {1}", result.Outcome, result.TerminationReason);
        }

        private void ShowError(string error)
        {
            m_ErrorPanel?.SetActive(true);
            SetText(m_ErrorText, string.IsNullOrEmpty(error) ? "未知的战斗回放错误。" : error);
        }

        private void HideError()
        {
            m_ErrorPanel?.SetActive(false);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.image != null)
                button.image.color = color;
        }
    }
}
