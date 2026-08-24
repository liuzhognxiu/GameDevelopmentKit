using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Core;
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
        public IBuqiBazaarSupplyViewSource BazaarSupplySource;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiRunShellForm : StarForceUIForm
    {
        private static readonly string[] s_RailLabels =
        {
            "一时 · 晨 · 经营",
            "二时 · 午 · 经营",
            "三时 · 昏 · 电脑对战",
            "四时 · 暮 · 经营",
            "五时 · 夜 · 经营",
            "六时 · 子 · 异步对战",
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
        private GameObject m_PhaseRail = null;

        [SerializeField]
        private MonoBehaviour[] m_StageComponents = Array.Empty<MonoBehaviour>();

        [SerializeField]
        private Button m_BackButton = null;

        [SerializeField]
        private Button m_PrimaryButton = null;

        [SerializeField]
        private Button m_DeployButton = null;

        [SerializeField]
        private Button m_PauseButton = null;

        [SerializeField]
        private Button m_RestartButton = null;

        [SerializeField]
        private GameObject m_ErrorPanel = null;

        [SerializeField]
        private BuqiRunPauseOverlay m_PauseOverlay = null;

        [SerializeField]
        private BuqiBattleResultOverlay m_BattleResultOverlay = null;

        [SerializeField]
        private Text m_ErrorText = null;

        private BuqiConfigCatalog m_Catalog;
        private BuqiUIDemoCatalog m_DemoCatalog;
        private BuqiUIDemoController m_Controller;
        private IBuqiBazaarSupplyRuntime m_BazaarSupplyRuntime;
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
            m_DeployButton?.onClick.AddListener(OpenDeployment);
            m_PauseButton?.onClick.AddListener(Pause);
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
            m_DemoCatalog = null;
            IBuqiBazaarSupplyViewSource supplySource =
                (userData as BuqiRunShellOpenData)?.BazaarSupplySource;
            m_BazaarSupplyRuntime = supplySource as IBuqiBazaarSupplyRuntime;
            BindBazaarSupplySource(supplySource);
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

            m_DemoCatalog = demoCatalog;

            if (supplySource == null)
            {
                if (!BuqiBazaarSupplyViewSource.TryCreate(
                        m_Catalog,
                        out BuqiBazaarSupplyViewSource productionSupply,
                        out error))
                {
                    m_Controller = null;
                    ShowError(error);
                    return;
                }
                m_BazaarSupplyRuntime = productionSupply;
                BindBazaarSupplySource(productionSupply);
            }

            if (!BuqiUIDemoController.TryCreate(
                    demoCatalog,
                    CreateControllerOptions(),
                    out BuqiUIDemoController controller,
                    out error))
            {
                m_Controller = null;
                ShowError(error);
                return;
            }

            m_Controller = controller;
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
            m_DemoCatalog = null;
            m_Catalog = null;
            m_BazaarSupplyRuntime = null;
            BindBazaarSupplySource(null);
            m_OpeningBattle = false;
            SetText(m_StatusText, string.Empty);
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_BackButton?.onClick.RemoveListener(GoBack);
            m_PrimaryButton?.onClick.RemoveListener(Advance);
            m_DeployButton?.onClick.RemoveListener(OpenDeployment);
            m_PauseButton?.onClick.RemoveListener(Pause);
            m_RestartButton?.onClick.RemoveListener(Restart);
            base.OnDestroy();
        }

        private void Submit(BuqiUIDemoCommand command)
        {
            if (m_Controller == null)
                return;

            ExecuteCommand(command);
        }

        private void Update()
        {
            if (m_BackButton != null)
                m_BackButton.interactable = !IsShopDragging();
        }

        private void ExecuteCommand(BuqiUIDemoCommand command)
        {
            if (m_Controller == null)
                return;

            BuqiUIDemoCommandResult result = m_Controller.Execute(command);
            SetText(m_StatusText, result.Accepted ? string.Empty : result.Reason);
            if (!result.Accepted)
                return;

            if (command.Type == BuqiUIDemoCommandType.Restart)
                HideError();

            if (command.Type == BuqiUIDemoCommandType.OpenDragDeploy)
            {
                OpenDragDeploy();
                return;
            }

            Render();
            if (command.Type == BuqiUIDemoCommandType.BuyOffer ||
                command.Type == BuqiUIDemoCommandType.SellItem)
            {
                NotifyShopTransactionSuccess();
            }
        }

        private void OpenDragDeploy()
        {
            if (m_DemoCatalog == null || m_Controller == null)
                return;

            BuqiUIDemoView view = m_Controller.View;
            GameEntry.UI.OpenUIForm(UIFormId.BuqiDragDeployForm, new BuqiDragDeployOpenData
            {
                Catalog = m_DemoCatalog,
                Board = view.BoardSlots,
                Storage = view.StorageSlots,
                Round = view.Round,
                Coins = view.Coins,
                Wins = view.Wins,
                Lives = view.Lives,
                OpponentName = view.Opponent?.Name ?? string.Empty,
                Confirmed = ApplyDeployment,
            });
        }

        private void ApplyDeployment(BuqiDeploymentSnapshot snapshot)
        {
            Submit(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = snapshot,
            });
        }

        private void OpenDeployment()
        {
            Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.OpenDragDeploy });
        }

        private void Pause()
        {
            Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.PauseRun });
        }

        private void GoBack()
        {
            if (IsShopDragging())
            {
                SetText(m_StatusText, "请先结束或取消当前拖拽。");
                return;
            }
            Close();
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
            BuqiUIDemoPhase? phase = m_Controller == null ? (BuqiUIDemoPhase?)null : m_Controller.View.Phase;
            BuqiRestartPolicy.TryDispatch(
                m_ErrorPanel != null && m_ErrorPanel.activeSelf,
                phase,
                RestartCore);
        }

        private void RestartCore()
        {
            if (m_Controller != null)
            {
                Submit(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.Restart });
                return;
            }

            if (m_DemoCatalog == null)
            {
                ShowError("重新开始失败，请检查配置表。");
                return;
            }

            if (!BuqiUIDemoController.TryCreateNewRun(
                    m_DemoCatalog,
                    CreateControllerOptions(),
                    out BuqiUIDemoController controller,
                    out string error))
            {
                ShowError(string.IsNullOrEmpty(error)
                    ? "重新开始失败，请检查存档文件和磁盘空间。"
                    : error);
                return;
            }

            m_Controller = controller;
            HideError();
            Render();
        }

        private BuqiUIDemoControllerOptions CreateControllerOptions()
        {
            return m_BazaarSupplyRuntime == null
                ? null
                : new BuqiUIDemoControllerOptions
                {
                    BazaarSupplyRuntime = m_BazaarSupplyRuntime,
                };
        }

        private void Render()
        {
            if (m_Controller == null)
                return;

            BuqiUIDemoView view = m_Controller.View;
            if (view.ExitRequested)
            {
                Close();
                return;
            }
            if (view.Phase == BuqiUIDemoPhase.BattleReplay)
            {
                OpenBattleReplay();
                return;
            }

            SetText(m_TitleText, "不器 · 九日试炼");
            SetText(m_ContextTitleText, view.ContextTitle);
            SetText(m_ContextBodyText, view.ContextBody);
            SetText(m_PrimaryLabel, view.PrimaryCommandLabel);
            SetText(m_RestartButton?.GetComponentInChildren<Text>(), "↻");
            m_RestartButton?.gameObject.SetActive(BuqiRestartPolicy.CanRestart(false, view.Phase));
            if (m_PrimaryButton != null)
                m_PrimaryButton.gameObject.SetActive(!string.IsNullOrEmpty(view.PrimaryCommandLabel));
            if (m_DeployButton != null)
                m_DeployButton.gameObject.SetActive(CanConfigureDeployment(view));
            if (m_BackButton != null)
                m_BackButton.interactable = !IsShopDragging();
            if (m_PauseButton != null)
                m_PauseButton.gameObject.SetActive(!view.IsPaused && view.Phase != BuqiUIDemoPhase.RunTerminal);
            m_PhaseRail?.SetActive(true);
            RenderResources(view);
            RenderPhaseRail(view);
            if (!m_Registry.Show(view, Submit))
            {
                ShowError("当前阶段界面不可用。"  );
            }
            m_PauseOverlay?.Render(view, Submit);
            m_BattleResultOverlay?.Render(view, Submit);
        }

        private static bool CanConfigureDeployment(BuqiUIDemoView view)
        {
            return view != null && BuqiUIDemoController.CanConfigureDeployment(view.Phase);
        }

        private bool IsShopDragging()
        {
            foreach (MonoBehaviour component in m_StageComponents)
            {
                if (component is ShopWidget shop)
                    return shop.IsDragging;
            }
            return false;
        }

        private void NotifyShopTransactionSuccess()
        {
            foreach (MonoBehaviour component in m_StageComponents)
            {
                if (component is ShopWidget shop)
                    shop.NotifyTransactionSuccess();
            }
        }

        private void BindBazaarSupplySource(IBuqiBazaarSupplyViewSource supplySource)
        {
            foreach (MonoBehaviour component in m_StageComponents)
            {
                if (component is ShopWidget shop)
                    shop.BindSupplySource(supplySource);
            }
        }

        private void RenderResources(BuqiUIDemoView view)
        {
            RenderChip(0, "金币", view.Coins.ToString(), "+", ResourceChipState.Normal);
            RenderChip(1, "回合", GameFramework.Utility.Text.Format("{0}/9", view.Round), "日", ResourceChipState.Normal);
            RenderChip(2, "生命", GameFramework.Utility.Text.Format("{0}/3", view.Lives), "命", view.Lives <= 1 ? ResourceChipState.Warning : ResourceChipState.Normal);
            RenderChip(3, "结算点/强度", GameFramework.Utility.Text.Format("{0}/{1}", view.DaoSeals, view.TribulationOmen), "点", ResourceChipState.Normal);
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
            int count = Math.Min(m_PhaseSteps.Length, s_RailLabels.Length);
            int currentIndex = ResolveRailIndex(view);
            for (int index = 0; index < count; index++)
            {
                m_PhaseSteps[index]?.Render(new PhaseStepView
                {
                    Phase = ResolveRailPhase(index),
                    Index = index + 1,
                    Label = s_RailLabels[index],
                    IsCurrent = index == currentIndex,
                    IsVisited = index <= currentIndex,
                    IsLocked = index > currentIndex,
                }, null);
            }
        }

        private void OpenBattleReplay()
        {
            if (m_OpeningBattle || m_Controller == null)
                return;

            BattleReplayData replay = m_Controller.CurrentReplay;
            if (replay == null)
            {
                ShowError("战斗回放不可用。"  );
                return;
            }

            m_OpeningBattle = true;
            GameEntry.UI.OpenUIForm(UIFormId.BattleForm, new BattleReplayOpenData
            {
                Replay = replay,
                Confirmed = CompleteBattleReplay,
            });
        }

        private void CompleteBattleReplay()
        {
            if (!m_OpeningBattle || m_Controller == null)
                return;

            m_OpeningBattle = false;
            BuqiUIDemoCommandResult result = m_Controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            });
            if (!result.Accepted)
            {
                ShowError(result.Reason);
                return;
            }

            Render();
        }

        private static int ResolveRailIndex(BuqiUIDemoView view)
        {
            return Mathf.Clamp((int)view.Period, 0, s_RailLabels.Length - 1);
        }

        private static BuqiUIDemoPhase ResolveRailPhase(int index)
        {
            switch (index)
            {
                case 0:
                case 1:
                    return BuqiUIDemoPhase.OperationChoice;
                case 2:
                    return BuqiUIDemoPhase.PveSelection;
                case 3:
                case 4:
                    return BuqiUIDemoPhase.OperationChoice;
                case 5:
                    return BuqiUIDemoPhase.BattleReplay;
                default:
                    return BuqiUIDemoPhase.RunTerminal;
            }
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
            SetText(m_RestartButton?.GetComponentInChildren<Text>(), "↻");
            m_RestartButton?.gameObject.SetActive(true);
            SetText(m_ErrorText, BuqiPlayerText.Error(error));
        }

        private void HideError()
        {
            m_ErrorPanel?.SetActive(false);
            m_RestartButton?.gameObject.SetActive(false);
            SetText(m_ErrorText, string.Empty);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
