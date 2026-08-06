using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Demo;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI.Stages;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    public sealed class BuqiRunShellOpenData
    {
        public BuqiConfigCatalog Catalog;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiRunShellForm : StarForceUIForm
    {
        private static readonly string[] phaseLabels =
        {
            "\u8D77\u59CB\u9009\u62E9", "\u5BF9\u624B\u5FEB\u7167", "\u6218\u524D\u51C6\u5907", "\u5546\u5E97", "\u4E8B\u4EF6", "\u6539\u9020",
            "\u68CB\u76D8\u7F16\u8F91", "\u80DC\u8D1F\u9884\u6D4B", "\u6218\u6597\u56DE\u653E", "\u6218\u6597\u603B\u7ED3", "\u56DE\u5408\u7ED3\u7B97", "\u5355\u5C40\u7ED3\u675F",
        };

        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_ContextTitleText = null;

        [SerializeField]
        private Text m_ContextBodyText = null;

        [SerializeField]
        private Text m_StatusText = null;

        [SerializeField]
        private Text m_PrimaryLabel = null;

        [SerializeField]
        private ResourceChipWidget[] m_ResourceChips = Array.Empty<ResourceChipWidget>();

        [SerializeField]
        private PhaseStepWidget[] m_PhaseSteps = Array.Empty<PhaseStepWidget>();

        [SerializeField]
        private MonoBehaviour[] m_StageComponents = Array.Empty<MonoBehaviour>();

        [SerializeField]
        private Button m_BackButton = null;

        [SerializeField]
        private Button m_PrimaryButton = null;

        [SerializeField]
        private Button m_RestartButton = null;

        [SerializeField]
        private GameObject m_ErrorPanel = null;

        [SerializeField]
        private Text m_ErrorText = null;

        private BuqiConfigCatalog m_Catalog;
        private BuqiUIDemoController m_Controller;
        private BuqiStageWidgetRegistry m_Registry;
        private bool m_OpeningBattle;

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            m_BackButton?.onClick.AddListener(GoBack);
            m_PrimaryButton?.onClick.AddListener(Advance);
            m_RestartButton?.onClick.AddListener(Restart);
            m_Registry = new BuqiStageWidgetRegistry(m_StageComponents);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            m_OpeningBattle = false;
            if (!TryResolveCatalog(userData, out m_Catalog, out string error))
            {
                m_Controller = null;
                ShowError(error);
                return;
            }
            if (!BuqiUIDemoCatalog.TryCreate(m_Catalog, out BuqiUIDemoCatalog demoCatalog, out error))
            {
                m_Controller = null;
                ShowError(error);
                return;
            }

            m_Controller = BuqiUIDemoController.Create(demoCatalog);
            HideError();
            Render();
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            m_Registry?.Clear();
            foreach (ResourceChipWidget chip in m_ResourceChips)
                chip?.Clear();
            foreach (PhaseStepWidget step in m_PhaseSteps)
                step?.Clear();
            m_Controller = null;
            m_Catalog = null;
            m_OpeningBattle = false;
            SetText(m_StatusText, string.Empty);
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_BackButton?.onClick.RemoveListener(GoBack);
            m_PrimaryButton?.onClick.RemoveListener(Advance);
            m_RestartButton?.onClick.RemoveListener(Restart);
            base.OnDestroy();
        }

        private void Submit(BuqiUIDemoCommand command)
        {
            if (m_Controller == null)
                return;
            BuqiUIDemoCommandResult result = m_Controller.Execute(command);
            SetText(m_StatusText, result.Accepted ? string.Empty : result.Reason);
            if (result.Accepted)
                Render();
        }

        private void GoBack()
        {
            Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.PreviousPhase });
        }

        private void Advance()
        {
            if (m_Controller?.View.Phase == BuqiUIDemoPhase.RunTerminal)
                Restart();
            else
                Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
        }

        private void Restart()
        {
            Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.Restart });
        }

        private void Render()
        {
            if (m_Controller == null)
                return;
            BuqiUIDemoView view = m_Controller.View;
            if (view.Phase == BuqiUIDemoPhase.BattleReplay)
            {
                OpenBattleReplay();
                return;
            }

            SetText(m_TitleText, "不器  |  演示界面总览");
            SetText(m_ContextTitleText, view.ContextTitle);
            SetText(m_ContextBodyText, view.ContextBody);
            SetText(m_PrimaryLabel, view.PrimaryCommandLabel);
            RenderResources(view);
            RenderPhaseRail(view);
            if (!m_Registry.Show(view, Submit))
                ShowError(GameFramework.Utility.Text.Format("缺少阶段预制体：{0}", view.Phase));
        }

        private void RenderResources(BuqiUIDemoView view)
        {
            RenderChip(0, "\u91D1\u5E01", view.Coins.ToString(), "+", ResourceChipState.Normal);
            RenderChip(1, "\u80DC\u573A", view.Wins.ToString(), "胜", ResourceChipState.Normal);
            RenderChip(2, "\u5355\u5C40\u751F\u547D", view.Lives.ToString(), "命", view.Lives <= 1 ? ResourceChipState.Warning : ResourceChipState.Normal);
            RenderChip(3, "\u56DE\u5408", view.Round.ToString(), "合", ResourceChipState.Normal);
        }

        private void RenderChip(int index, string label, string value, string icon, ResourceChipState state)
        {
            if (index < 0 || index >= m_ResourceChips.Length || m_ResourceChips[index] == null)
                return;
            m_ResourceChips[index].Render(new ResourceChipView
            {
                Label = label,
                Value = value,
                Icon = icon,
                State = state,
            });
        }

        private void RenderPhaseRail(BuqiUIDemoView view)
        {
            int count = Math.Min(m_PhaseSteps.Length, phaseLabels.Length);
            for (int index = 0; index < count; index++)
            {
                BuqiUIDemoPhase phase = (BuqiUIDemoPhase)index;
                m_PhaseSteps[index]?.Render(new PhaseStepView
                {
                    Phase = phase,
                    Index = index + 1,
                    Label = phaseLabels[index],
                    IsCurrent = phase == view.Phase,
                    IsVisited = view.VisitedPhases.Contains(phase),
                    IsLocked = phase > view.Phase && !view.VisitedPhases.Contains(phase),
                }, null);
            }
        }

        private void OpenBattleReplay()
        {
            if (m_OpeningBattle)
                return;
            m_OpeningBattle = true;
            if (!BuqiBattleDemoFactory.TryCreate(m_Catalog, out BattleReplayData replay, out string error))
            {
                m_OpeningBattle = false;
                ShowError(error);
                return;
            }

            GameEntry.UI.OpenUIForm(UIFormId.BattleForm, replay);
            BuqiUIDemoCommandResult result = m_Controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            });
            m_OpeningBattle = false;
            if (!result.Accepted)
            {
                ShowError(result.Reason);
                return;
            }
            Render();
        }

        private static bool TryResolveCatalog(object userData, out BuqiConfigCatalog catalog, out string error)
        {
            if (userData is BuqiRunShellOpenData data && data.Catalog != null)
            {
                catalog = data.Catalog;
                error = string.Empty;
                return true;
            }
            if (HotEntry.Tables == null)
            {
                catalog = null;
                error = "不器配置表尚未初始化。";
                return false;
            }
            if (!BuqiGeneratedConfigAdapter.TryReadFromTables(HotEntry.Tables, out catalog, out List<string> errors))
            {
                error = string.Join("\n", errors);
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void ShowError(string error)
        {
            m_ErrorPanel?.SetActive(true);
            SetText(m_ErrorText, error);
        }

        private void HideError()
        {
            m_ErrorPanel?.SetActive(false);
            SetText(m_ErrorText, string.Empty);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
