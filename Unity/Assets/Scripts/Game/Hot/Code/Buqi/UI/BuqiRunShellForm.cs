using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.DemoUI.Interaction;
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
        private static readonly string[] s_RailLabelKeys =
        {
            "Buqi.RunShell.MorningOperation",
            "Buqi.RunShell.NoonOperation",
            "Buqi.RunShell.DuskPve",
            "Buqi.RunShell.NightPvp",
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
        private Button m_RestartButton = null;

        [SerializeField]
        private GameObject m_ErrorPanel = null;

        [SerializeField]
        private Text m_ErrorText = null;

        private BuqiConfigCatalog m_Catalog;
        private BuqiUIDemoCatalog m_DemoCatalog;
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
            m_DeployButton?.onClick.AddListener(OpenDeployment);
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
            BindBazaarSupplySource((userData as BuqiRunShellOpenData)?.BazaarSupplySource);
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

            if (!BuqiUIDemoController.TryCreate(demoCatalog, null, out BuqiUIDemoController controller, out error))
            {
                m_Controller = null;
                ShowError(error);
                return;
            }

            m_DemoCatalog = demoCatalog;
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
            m_RestartButton?.onClick.RemoveListener(Restart);
            base.OnDestroy();
        }

        private void Submit(BuqiUIDemoCommand command)
        {
            if (m_Controller == null)
                return;

            if (command != null && command.Type == BuqiUIDemoCommandType.BuyOffer)
            {
                OpenPurchaseConfirmation(command);
                return;
            }

            ExecuteCommand(command);
        }

        private void ExecuteCommand(BuqiUIDemoCommand command)
        {
            if (m_Controller == null)
                return;

            BuqiUIDemoCommandResult result = m_Controller.Execute(command);
            SetText(m_StatusText, result.Accepted ? string.Empty : result.Reason);
            if (!result.Accepted)
                return;

            if (command.Type == BuqiUIDemoCommandType.OpenDragDeploy)
            {
                OpenDragDeploy();
                return;
            }

            Render();
        }

        private void OpenPurchaseConfirmation(BuqiUIDemoCommand command)
        {
            BuqiDemoOfferView selectedOffer = null;
            foreach (BuqiDemoOfferView offer in m_Controller.View.ShopOffers)
            {
                if (string.Equals(offer.Id, command.PrimaryId, StringComparison.Ordinal))
                {
                    selectedOffer = offer;
                    break;
                }
            }

            if (selectedOffer == null)
            {
                ShowError("Selected shop offer is unavailable.");
                return;
            }

            string itemName = selectedOffer.Item?.Name ?? selectedOffer.Id;
            GameEntry.UI.OpenUIForm(UIFormId.BuqiConfirmForm, new BuqiConfirmOpenData
            {
                Title = "Confirm Purchase",
                Message = GameFramework.Utility.Text.Format(
                    "Buy {0} for {1} coins?",
                    itemName,
                    selectedOffer.Price),
                ConfirmLabel = "Buy",
                CancelLabel = "Keep Shopping",
                Confirm = () => ExecuteCommand(command),
            });
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

        private void GoBack()
        {
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

            SetText(m_TitleText, "Buqi Demo Run");
            SetText(m_ContextTitleText, view.ContextTitle);
            SetText(m_ContextBodyText, view.ContextBody);
            SetText(m_PrimaryLabel, view.PrimaryCommandLabel);
            if (m_PrimaryButton != null)
                m_PrimaryButton.gameObject.SetActive(!string.IsNullOrEmpty(view.PrimaryCommandLabel));
            if (m_DeployButton != null)
                m_DeployButton.gameObject.SetActive(CanConfigureDeployment(view));
            m_PhaseRail?.SetActive(view.Phase != BuqiUIDemoPhase.PveSelection);
            RenderResources(view);
            RenderPhaseRail(view);
            if (!m_Registry.Show(view, Submit))
            {
                ShowError(GameFramework.Utility.Text.Format("Missing stage widget for {0}.", view.Phase));
            }
        }

        private static bool CanConfigureDeployment(BuqiUIDemoView view)
        {
            return view != null && BuqiUIDemoController.CanConfigureDeployment(view.Phase);
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
            RenderChip(0, "Coins", view.Coins.ToString(), "+", ResourceChipState.Normal);
            RenderChip(1, "Day", GameFramework.Utility.Text.Format("{0}/9", view.Round), "D", ResourceChipState.Normal);
            RenderChip(2, "Lives", GameFramework.Utility.Text.Format("{0}/3", view.Lives), "L", view.Lives <= 1 ? ResourceChipState.Warning : ResourceChipState.Normal);
            RenderChip(3, "Dao/Omen", GameFramework.Utility.Text.Format("{0}/{1}", view.DaoSeals, view.TribulationOmen), "T", ResourceChipState.Normal);
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
            int count = Math.Min(m_PhaseSteps.Length, s_RailLabelKeys.Length);
            int currentIndex = ResolveRailIndex(view);
            for (int index = 0; index < count; index++)
            {
                m_PhaseSteps[index]?.Render(new PhaseStepView
                {
                    Phase = ResolveRailPhase(index),
                    Index = index + 1,
                    Label = GameEntry.Localization.GetString(s_RailLabelKeys[index]),
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
                ShowError("Battle replay is unavailable.");
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
            return Mathf.Clamp((int)view.Period, 0, s_RailLabelKeys.Length - 1);
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
                error = "Buqi tables are not initialized.";
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
