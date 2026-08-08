using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Settlement;
using UnityEngine;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiUIDemoControllerOptions
    {
        public long RunSeed = 1L;
        public IBuqiRunStore Store;
        public IReadOnlyList<string> PveOpponentIds;
        public IReadOnlyList<string> PvpOpponentIds;
    }
}

namespace Game.Hot.Buqi.Run.Integration
{
    internal enum BuqiRunDemoPresentation
    {
        Encounter = 0,
        BattleReplay = 1,
        BattleSummary = 2,
        DaySettlement = 3,
        RunTerminal = 4,
        TribulationRoute = 5,
        TribulationStage = 6,
        OperationChoice = 7,
        PveSelection = 8,
    }

    internal sealed class BuqiRunDemoState
    {
        public BuqiRunEconomySnapshot Economy = null!;
        public BuqiRunEncounterState Encounter;
        public BuqiRunBattleSession Battle;
        public BuqiPveSelection PveSelection;
        public BuqiRunBattleSummary BattleSummary = new BuqiRunBattleSummary();
        public BuqiRunRawBattleOutcome LastRawOutcome;
        public BuqiRunDemoPresentation Presentation;
        public string LastResolutionId = string.Empty;
        public List<BuqiUIDemoPhase> VisitedPhases = new List<BuqiUIDemoPhase>();

        public BuqiRunDemoState Clone()
        {
            return new BuqiRunDemoState
            {
                Economy = Economy.Clone(),
                Encounter = Encounter?.Clone(),
                Battle = Battle == null ? null : BuqiRunDemoCodec.CloneBattleSession(Battle),
                PveSelection = PveSelection?.Clone(),
                BattleSummary = BuqiRunDemoCodec.CloneSummary(BattleSummary),
                LastRawOutcome = LastRawOutcome,
                Presentation = Presentation,
                LastResolutionId = LastResolutionId,
                VisitedPhases = new List<BuqiUIDemoPhase>(VisitedPhases),
            };
        }
    }

    internal sealed class BuqiRunDemoOrchestrator
    {
        private static readonly string[] s_DefaultPveOpponentIds =
        {
            "echo-fast-lesson",
            "echo-buffer-lesson",
            "echo-chain-lesson",
            "echo-heal-lesson",
            "echo-poison-lesson",
            "echo-burn-lesson",
            "echo-freeze-lesson",
            "echo-overload-lesson",
        };

        private static readonly string[] s_DefaultPvpOpponentIds =
        {
            "echo-fast-early",
            "echo-buffer-early",
            "echo-chain-early",
            "echo-heal-early",
            "echo-poison-early",
            "echo-burn-early",
            "echo-freeze-early",
            "echo-overload-early",
        };

        private readonly BuqiUIDemoCatalog m_Catalog;
        private readonly BuqiUIDemoControllerOptions m_Options;
        private readonly BuqiDefinitionProvider m_Definitions;
        private readonly BuqiRunItemCatalogAdapter m_ItemCatalog;
        private readonly BuqiRunEconomyService m_EconomyService;
        private readonly BuqiRunEncounterService m_EncounterService;
        private readonly BuqiRunEventResolver m_EventResolver;
        private readonly BuqiRunBattleService m_BattleService;
        private readonly BuqiRunSettlementCoordinator m_SettlementCoordinator;
        private readonly IBuqiRunStore m_Store;

        private BuqiRunDemoState m_State = null!;

        public BuqiRunDemoOrchestrator(BuqiUIDemoCatalog catalog, BuqiUIDemoControllerOptions options)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_Options = options ?? new BuqiUIDemoControllerOptions();
            if (catalog.SourceCatalog == null)
                throw new ArgumentException("Source catalog is required.", nameof(catalog));

            m_Definitions = new BuqiDefinitionProvider(catalog.SourceCatalog);
            m_ItemCatalog = new BuqiRunItemCatalogAdapter(catalog.SourceCatalog);
            m_EconomyService = new BuqiRunEconomyService(m_ItemCatalog);
            m_EncounterService = new BuqiRunEncounterService(catalog);
            m_EventResolver = new BuqiRunEventResolver(catalog);
            m_BattleService = new BuqiRunBattleService(CreateBattleProvider(catalog, m_Options));
            m_Store = m_Options.Store ?? CreateDefaultStore();
            m_SettlementCoordinator = new BuqiRunSettlementCoordinator(m_Store);
        }

        public BuqiRunDemoState State => m_State.Clone();

        public bool TryInitialize(out string error)
        {
            if (!m_Store.TryRead(out string json, out string readError))
            {
                if (!string.Equals(readError, "Save file does not exist.", StringComparison.Ordinal))
                {
                    error = readError;
                    return false;
                }

                return TryStartNewRun(out error);
            }

            if (!BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData saveData, out error))
                return false;
            if (!TryValidateContentVersion(saveData.ContentVersion, out error))
                return false;

            if (saveData.PendingSettlement != null)
            {
                BuqiRunSettlementResult resumed = m_SettlementCoordinator.ResumePendingSettlement();
                if (!resumed.Success)
                {
                    error = resumed.FailureReason;
                    return false;
                }

                if (!m_Store.TryRead(out json, out readError))
                {
                    error = readError;
                    return false;
                }

                if (!BuqiRunSaveCodec.TryFromJson(json, out saveData, out error))
                    return false;
                if (!TryValidateContentVersion(saveData.ContentVersion, out error))
                    return false;
            }

            return TryLoadFromSave(saveData, out error);
        }

        public bool Restart(out string error)
        {
            return TryStartNewRun(out error);
        }

        public bool TrySkipShopEncounter(out string error)
        {
            if (!IsEncounterShop(m_State))
            {
                error = "Current phase is not a shop encounter.";
                return false;
            }

            return TryResolveEncounterCommand(
                m_State.Clone(),
                CreateCommandId(m_State.Economy.Run, "shop-skip"),
                BuqiText.Format("{0}:skip", m_State.Encounter.EncounterId),
                out error);
        }

        public bool TryPurchase(string definitionId, out string error)
        {
            if (!IsEncounterShop(m_State))
            {
                error = "Current phase is not a shop encounter.";
                return false;
            }

            if (m_State.Encounter == null || !m_State.Encounter.CandidateIds.Contains(definitionId))
            {
                error = "Offer is not available in the frozen shop.";
                return false;
            }

            BuqiRunEconomyResult purchase = m_EconomyService.Purchase(m_State.Economy, definitionId);
            if (!purchase.Success)
            {
                error = purchase.FailureReason;
                return false;
            }

            BuqiRunDemoState working = m_State.Clone();
            working.Economy = purchase.Snapshot;
            working.LastResolutionId = definitionId;
            return TryResolveEncounterCommand(
                working,
                CreateCommandId(m_State.Economy.Run, "shop-buy", definitionId),
                definitionId,
                out error);
        }

        public bool TryResolveEvent(string eventId, out string error)
        {
            if (!IsEncounterEvent(m_State))
            {
                error = "Current phase is not an event encounter.";
                return false;
            }

            if (!m_EventResolver.TryResolve(
                    m_State.Encounter,
                    eventId,
                    out BuqiRunEncounterState resolved,
                    out BuqiRunEncounterDelta delta,
                    out error))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(delta.GrantedRefinementId))
            {
                error = $"Granted refinement '{delta.GrantedRefinementId}' is not supported in the demo run.";
                return false;
            }

            BuqiRunEconomySnapshot workingEconomy = m_State.Economy.Clone();
            workingEconomy.Run.Coins = Math.Max(0, workingEconomy.Run.Coins + delta.Coins);
            workingEconomy.Run.Lives = Math.Min(
                BuqiRunRules.StartingLives,
                Math.Max(0, workingEconomy.Run.Lives + delta.Lives));

            if (!string.IsNullOrEmpty(delta.GrantedItemDefinitionId))
            {
                BuqiRunEconomyResult granted = m_EconomyService.GrantFreeItem(
                    workingEconomy,
                    delta.GrantedItemDefinitionId);
                if (!granted.Success)
                {
                    error = granted.FailureReason;
                    return false;
                }

                workingEconomy = granted.Snapshot;
            }

            BuqiRunDemoState working = m_State.Clone();
            working.Economy = workingEconomy;
            working.Encounter = resolved;
            working.LastResolutionId = resolved.ResolutionId;
            return TryResolveEncounterCommand(
                working,
                CreateCommandId(m_State.Economy.Run, "event", eventId),
                resolved.ResolutionId,
                out error);
        }

        public bool TryApplyDeployment(BuqiDeploymentSnapshot deployment, out string error)
        {
            if (deployment == null)
            {
                error = "Deployment snapshot is unavailable.";
                return false;
            }

            if (m_State.Economy.Run.Phase != BuqiRunPhase.Encounter)
            {
                error = "Deployment is not available in the current phase.";
                return false;
            }

            string[] board = deployment.BoardSlots.ToArray();
            string[] storage = deployment.StorageSlots.ToArray();
            if (!TryValidateDeployment(board, storage, m_State.Economy, out List<BoardPlacement> placements, out error))
                return false;

            string[] normalizedBoard = CreateEmptyBoardSlots();
            foreach (BoardPlacement placement in placements)
                normalizedBoard[placement.AnchorSlot] = placement.Item.InstanceId;

            BuqiRunDemoState working = m_State.Clone();
            working.Economy.Run.BoardInstanceIds = new List<string>(normalizedBoard);
            working.Economy.Run.StorageInstanceIds = new List<string>(storage);
            return TryCommitState(working, out error);
        }

        public bool TryAdvance(out string error)
        {
            switch (m_State.Presentation)
            {
                case BuqiRunDemoPresentation.Encounter:
                    if (IsEncounterShop(m_State))
                        return TrySkipShopEncounter(out error);
                    error = "Current event requires an explicit choice.";
                    return false;

                case BuqiRunDemoPresentation.OperationChoice:
                    error = "Operation requires an explicit choice.";
                    return false;

                case BuqiRunDemoPresentation.PveSelection:
                    error = "PVE difficulty requires an explicit choice.";
                    return false;

                case BuqiRunDemoPresentation.BattleReplay:
                    return TrySettleCurrentBattle(out error);

                case BuqiRunDemoPresentation.BattleSummary:
                    return TryAdvanceAfterBattleSummary(out error);

                case BuqiRunDemoPresentation.DaySettlement:
                    return TryCompleteDay(out error);

                case BuqiRunDemoPresentation.TribulationRoute:
                    error = "Tribulation route requires an explicit choice.";
                    return false;

                case BuqiRunDemoPresentation.TribulationStage:
                    error = "Tribulation stage requires an explicit result.";
                    return false;

                case BuqiRunDemoPresentation.RunTerminal:
                    error = "Run has already ended.";
                    return false;

                default:
                    error = "Unknown presentation state.";
                    return false;
            }
        }

        public bool TrySelectOperation(string operationId, out string error)
        {
            if (m_State.Presentation != BuqiRunDemoPresentation.OperationChoice ||
                m_State.Economy.Run.Phase != BuqiRunPhase.Encounter ||
                m_State.Encounter != null)
            {
                error = "Current phase is not operation selection.";
                return false;
            }

            if (string.Equals(operationId, "meditate", StringComparison.Ordinal))
            {
                return TryResolveEncounterCommand(
                    m_State.Clone(),
                    CreateCommandId(m_State.Economy.Run, "operation-meditate"),
                    "meditate",
                    out error);
            }

            BuqiRunEncounterKind kind;
            if (string.Equals(operationId, "bazaar", StringComparison.Ordinal))
                kind = BuqiRunEncounterKind.Shop;
            else if (string.Equals(operationId, "event", StringComparison.Ordinal))
                kind = BuqiRunEncounterKind.Event;
            else
            {
                error = "Operation choice is invalid.";
                return false;
            }

            BuqiRunDemoState working = m_State.Clone();
            if (!m_EncounterService.TryGetOrCreateForKind(
                    working.Economy.Run,
                    null,
                    kind,
                    out BuqiRunEncounterState encounter,
                    out error))
            {
                return false;
            }

            working.Encounter = encounter;
            working.Presentation = BuqiRunDemoPresentation.Encounter;
            return TryCommitState(working, out error);
        }

        public bool TrySelectPveDifficulty(string choiceId, out string error)
        {
            if (m_State.Presentation != BuqiRunDemoPresentation.PveSelection ||
                m_State.PveSelection == null || m_State.Battle != null)
            {
                error = "Current phase is not PVE selection.";
                return false;
            }

            BuqiPveChoiceCard card = m_State.PveSelection.Cards.Find(
                candidate => string.Equals(candidate.ChoiceId, choiceId, StringComparison.Ordinal));
            if (card == null)
            {
                error = "PVE difficulty is unavailable.";
                return false;
            }

            BuqiRunDemoState working = m_State.Clone();
            if (!TryBuildPlayerSnapshot(working.Economy, out BuildSnapshot playerBuild, out error))
                return false;
            if (!m_BattleService.TrySelectPveDifficultyAndSimulate(
                    working.Economy.Run,
                    working.PveSelection,
                    card.Difficulty,
                    playerBuild,
                    m_Definitions,
                    out BuqiRunBattleSession battle,
                    out error))
            {
                return false;
            }

            working.Economy.Run.RngCursor = battle.NextRngCursor;
            working.Battle = battle;
            working.PveSelection = null;
            working.Presentation = BuqiRunDemoPresentation.BattleReplay;
            return TryCommitState(working, out error);
        }

        public bool TrySelectTribulationRoute(
            BuqiTribulationRoute route,
            int daoSealsToSpend,
            out string error)
        {
            BuqiRunDemoState working = m_State.Clone();
            var controller = new BuqiRunController(working.Economy.Run);
            BuqiRunTransitionResult result = controller.SelectTribulationRoute(
                CreateCommandId(working.Economy.Run, "tribulation-route", route.ToString()),
                working.Economy.Run.Revision,
                route,
                daoSealsToSpend);
            if (!result.Success)
            {
                error = result.FailureReason;
                return false;
            }

            working.Economy.Run = result.State.Clone();
            working.Battle = null;
            working.BattleSummary = new BuqiRunBattleSummary();
            working.LastRawOutcome = default;
            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        public bool TryResolveTribulationStage(bool survived, out string error)
        {
            BuqiRunDemoState working = m_State.Clone();
            var controller = new BuqiRunController(working.Economy.Run);
            BuqiRunTransitionResult result = controller.ResolveTribulationStage(
                CreateCommandId(
                    working.Economy.Run,
                    "tribulation-stage",
                    working.Economy.Run.TribulationStage.ToString()),
                working.Economy.Run.Revision,
                survived);
            if (!result.Success)
            {
                error = result.FailureReason;
                return false;
            }

            working.Economy.Run = result.State.Clone();
            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        public bool TryResolveCurrentTribulationStage(out string error)
        {
            return TryResolveTribulationStage(true, out error);
        }

        public BattleReplayData BuildReplayData()
        {
            if (m_State.Battle == null)
                return null;

            return new BattleReplayData
            {
                Title = m_State.Battle.Replay?.Title ?? BuqiText.Format("{0} Battle", m_State.Battle.Kind),
                LeftName = m_State.Battle.Replay?.LeftName ?? "Player",
                RightName = m_State.Battle.Replay?.RightName ?? m_State.Battle.OpponentId,
                LeftBuild = BuqiRunDemoCodec.CloneBuildSnapshot(m_State.Battle.Request.Left),
                RightBuild = BuqiRunDemoCodec.CloneBuildSnapshot(m_State.Battle.Request.Right),
                Result = BuqiRunDemoCodec.CloneBattleResult(m_State.Battle.Result),
                Log = BuqiRunDemoCodec.CloneBattleLog(m_State.Battle.Log),
                Definitions = m_Definitions,
            };
        }

        private bool TryStartNewRun(out string error)
        {
            BuqiRunEconomySnapshot economy = BuqiRunEconomySnapshot.CreateInitial(
                m_Options.RunSeed,
                m_Definitions.ContentVersion);
            if (!TrySeedStarterBuild(economy, out error))
                return false;

            var working = new BuqiRunDemoState
            {
                Economy = economy,
                Encounter = null,
                Battle = null,
                BattleSummary = new BuqiRunBattleSummary(),
                LastRawOutcome = default,
                Presentation = BuqiRunDemoPresentation.OperationChoice,
            };
            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        private bool TryLoadFromSave(BuqiRunSaveData saveData, out string error)
        {
            if (!BuqiRunSaveCodec.TryToState(saveData, out BuqiRunState runState, out error))
                return false;
            if (!BuqiRunDemoCodec.TryDecodeEconomy(runState, saveData.EconomyPayload, out BuqiRunEconomySnapshot economy, out error))
                return false;
            if (!BuqiRunDemoCodec.TryDecodeEncounter(runState, saveData.EncounterPayload, out BuqiRunEncounterState encounter, out error))
                return false;
            if (!BuqiRunDemoCodec.TryDecodeBattle(runState, saveData.BattlePayload, m_Definitions, out BuqiRunBattleSession battle, out error))
                return false;
            if (!TryRestorePresentation(runState, encounter, battle, out BuqiRunDemoPresentation presentation, out error))
                return false;

            var working = new BuqiRunDemoState
            {
                Economy = economy,
                Encounter = encounter,
                Battle = battle,
                PveSelection = null,
                Presentation = presentation,
                BattleSummary = battle == null
                    ? new BuqiRunBattleSummary()
                    : BuqiRunBattleSummaryBuilder.Build(battle.Result, battle.Log),
                LastRawOutcome = battle == null ? default : battle.RawOutcome,
            };

            if (!EnsureCurrentContent(working, false, out error))
                return false;

            m_State = working;
            EnsureVisitedPhase(m_State, CurrentViewPhase(m_State));
            error = string.Empty;
            return true;
        }

        private bool TryRestorePresentation(
            BuqiRunState runState,
            BuqiRunEncounterState encounter,
            BuqiRunBattleSession battle,
            out BuqiRunDemoPresentation presentation,
            out string error)
        {
            switch (runState.Phase)
            {
                case BuqiRunPhase.Encounter:
                    presentation = encounter == null
                        ? BuqiRunDemoPresentation.OperationChoice
                        : BuqiRunDemoPresentation.Encounter;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.PveBattle:
                    presentation = battle == null
                        ? BuqiRunDemoPresentation.PveSelection
                        : BuqiRunDemoPresentation.BattleReplay;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.PvpBattle:
                    if (battle == null)
                    {
                        presentation = default;
                        error = "Battle payload is required for the active battle phase.";
                        return false;
                    }

                    presentation = runState.Phase == BuqiRunPhase.PvpBattle &&
                                   battle.Kind == BuqiRunBattleKind.Pve &&
                                   BuqiRunDemoCodec.IsSettledBattle(runState, battle.BattleId)
                        ? BuqiRunDemoPresentation.BattleSummary
                        : BuqiRunDemoPresentation.BattleReplay;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.DaySettlement:
                    presentation = battle == null
                        ? BuqiRunDemoPresentation.DaySettlement
                        : BuqiRunDemoPresentation.BattleSummary;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.TribulationRoute:
                    if (battle != null &&
                        (battle.Kind != BuqiRunBattleKind.Pvp ||
                         !BuqiRunDemoCodec.IsSettledBattle(runState, battle.BattleId)))
                    {
                        presentation = default;
                        error = "Only the settled day nine PVP battle can remain at route choice.";
                        return false;
                    }

                    presentation = battle == null
                        ? BuqiRunDemoPresentation.TribulationRoute
                        : BuqiRunDemoPresentation.BattleSummary;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.TribulationStage:
                    if (battle != null)
                    {
                        presentation = default;
                        error = "Battle payload is not valid during tribulation stages.";
                        return false;
                    }

                    presentation = BuqiRunDemoPresentation.TribulationStage;
                    error = string.Empty;
                    return true;

                case BuqiRunPhase.RunTerminal:
                    presentation = BuqiRunDemoPresentation.RunTerminal;
                    error = string.Empty;
                    return true;

                default:
                    presentation = default;
                    error = "Run phase is invalid.";
                    return false;
            }
        }

        private bool EnsureCurrentContent(BuqiRunDemoState state, bool allowGeneration, out string error)
        {
            switch (state.Economy.Run.Phase)
            {
                case BuqiRunPhase.Encounter:
                    state.Battle = null;
                    state.PveSelection = null;
                    state.Presentation = state.Encounter == null
                        ? BuqiRunDemoPresentation.OperationChoice
                        : BuqiRunDemoPresentation.Encounter;
                    break;

                case BuqiRunPhase.PveBattle:
                    state.Encounter = null;
                    state.Presentation = state.Battle == null
                        ? BuqiRunDemoPresentation.PveSelection
                        : BuqiRunDemoPresentation.BattleReplay;
                    if (state.Battle == null)
                    {
                        if (!TryBuildPlayerSnapshot(state.Economy, out BuildSnapshot playerBuild, out error))
                            return false;
                        if (!m_BattleService.TryGetOrCreatePveSelection(
                                state.Economy.Run,
                                state.PveSelection,
                                playerBuild,
                                out BuqiPveSelection selection,
                                out error))
                        {
                            return false;
                        }
                        state.PveSelection = selection;
                    }
                    break;

                case BuqiRunPhase.PvpBattle:
                    state.Encounter = null;
                    state.PveSelection = null;
                    state.Presentation = state.Battle != null &&
                                         state.Battle.Kind == BuqiRunBattleKind.Pve &&
                                         BuqiRunDemoCodec.IsSettledBattle(
                                             state.Economy.Run,
                                             state.Battle.BattleId)
                        ? BuqiRunDemoPresentation.BattleSummary
                        : BuqiRunDemoPresentation.BattleReplay;
                    if (state.Battle == null)
                    {
                        if (!allowGeneration)
                        {
                            error = "Battle payload is missing for the PVP phase.";
                            return false;
                        }

                        if (!TryGenerateBattle(state, BuqiRunBattleKind.Pvp, out error))
                            return false;
                    }
                    break;

                case BuqiRunPhase.DaySettlement:
                    state.Encounter = null;
                    state.Presentation = state.Battle == null
                        ? BuqiRunDemoPresentation.DaySettlement
                        : BuqiRunDemoPresentation.BattleSummary;
                    break;

                case BuqiRunPhase.TribulationRoute:
                    state.Encounter = null;
                    state.Presentation = state.Battle == null
                        ? BuqiRunDemoPresentation.TribulationRoute
                        : BuqiRunDemoPresentation.BattleSummary;
                    break;

                case BuqiRunPhase.TribulationStage:
                    state.Encounter = null;
                    state.Battle = null;
                    state.Presentation = BuqiRunDemoPresentation.TribulationStage;
                    break;

                case BuqiRunPhase.RunTerminal:
                    state.Encounter = null;
                    state.Presentation = BuqiRunDemoPresentation.RunTerminal;
                    break;
            }

            EnsureVisitedPhase(state, CurrentViewPhase(state));
            error = string.Empty;
            return true;
        }

        private bool TryGenerateBattle(
            BuqiRunDemoState state,
            BuqiRunBattleKind kind,
            out string error)
        {
            if (!TryBuildPlayerSnapshot(state.Economy, out BuildSnapshot playerBuild, out error))
                return false;

            if (!m_BattleService.TryCreateAndSimulate(
                    state.Economy.Run,
                    kind,
                    playerBuild,
                    m_Definitions,
                    out BuqiRunBattleSession session,
                    out error))
            {
                return false;
            }

            state.Economy.Run.RngCursor = session.NextRngCursor;
            state.Battle = session;
            state.Encounter = null;
            return true;
        }

        private bool TrySettleCurrentBattle(out string error)
        {
            if (m_State.Battle == null)
            {
                error = "Battle payload is unavailable.";
                return false;
            }

            string settlementId = CreateSettlementId(m_State.Battle);
            BuqiRunSettlementResult settlement = m_SettlementCoordinator.SettleBattle(
                m_State.Economy.Run,
                settlementId,
                m_State.Battle.Result,
                m_State.Battle.Log,
                BuqiRunDemoCodec.EncodeEconomy(m_State.Economy),
                string.Empty,
                BuqiRunDemoCodec.EncodeBattle(m_State.Battle));
            if (!settlement.Success)
            {
                error = settlement.FailureReason;
                return false;
            }

            BuqiRunDemoState working = m_State.Clone();
            working.Economy.Run = settlement.State.Clone();
            working.BattleSummary = settlement.Summary;
            working.LastRawOutcome = settlement.RawOutcome;
            working.Presentation = settlement.State.Phase == BuqiRunPhase.RunTerminal
                ? BuqiRunDemoPresentation.RunTerminal
                : BuqiRunDemoPresentation.BattleSummary;
            EnsureVisitedPhase(working, CurrentViewPhase(working));
            m_State = working;
            error = string.Empty;
            return true;
        }

        private bool TryAdvanceAfterBattleSummary(out string error)
        {
            if (m_State.Economy.Run.Phase == BuqiRunPhase.DaySettlement)
                return TryCompleteDay(out error);

            BuqiRunDemoState working = m_State.Clone();
            working.Battle = null;
            working.BattleSummary = new BuqiRunBattleSummary();
            working.LastRawOutcome = default;

            if (working.Economy.Run.Phase == BuqiRunPhase.RunTerminal)
            {
                working.Presentation = BuqiRunDemoPresentation.RunTerminal;
                return TryCommitState(working, out error);
            }

            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        private bool TryCompleteDay(out string error)
        {
            BuqiRunDemoState working = m_State.Clone();
            var controller = new BuqiRunController(working.Economy.Run);
            BuqiRunTransitionResult result = controller.CompleteDay(
                CreateCommandId(working.Economy.Run, "day-complete"),
                working.Economy.Run.Revision);
            if (!result.Success)
            {
                error = result.FailureReason;
                return false;
            }

            working.Economy.Run = result.State.Clone();
            working.Battle = null;
            working.BattleSummary = new BuqiRunBattleSummary();
            working.LastRawOutcome = default;
            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        private bool TryResolveEncounterCommand(
            BuqiRunDemoState working,
            string commandId,
            string resolutionId,
            out string error)
        {
            var controller = new BuqiRunController(working.Economy.Run);
            BuqiRunTransitionResult result = controller.ResolveEncounter(
                commandId,
                working.Economy.Run.Revision);
            if (!result.Success)
            {
                error = result.FailureReason;
                return false;
            }

            working.Economy.Run = result.State.Clone();
            if (working.Encounter != null)
            {
                BuqiRunEncounterState resolved = working.Encounter.Clone();
                resolved.Resolved = true;
                resolved.ResolutionId = string.IsNullOrEmpty(resolutionId)
                    ? BuqiText.Format("{0}:resolve", resolved.EncounterId)
                    : resolutionId;
                working.Economy.Run.RngCursor = resolved.NextRngCursor;
                working.Encounter = null;
            }

            working.LastResolutionId = resolutionId ?? string.Empty;
            if (!EnsureCurrentContent(working, true, out error))
                return false;

            return TryCommitState(working, out error);
        }

        private bool TryCommitState(BuqiRunDemoState working, out string error)
        {
            try
            {
                EnsureVisitedPhase(working, CurrentViewPhase(working));
                BuqiRunSaveData saveData = BuqiRunSaveCodec.FromState(
                    working.Economy.Run,
                    BuqiRunDemoCodec.EncodeEconomy(working.Economy),
                    BuqiRunDemoCodec.EncodeEncounter(working.Encounter),
                    BuqiRunDemoCodec.EncodeBattle(working.Battle),
                    null);
                string json = BuqiRunSaveCodec.ToJson(saveData);
                if (!m_Store.TryWrite(json, out error))
                    return false;

                m_State = working;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool TrySeedStarterBuild(BuqiRunEconomySnapshot economy, out string error)
        {
            BuqiUIDemoItemDefinition starter = m_Catalog.Items
                .Where(item => item != null && item.Size == 1)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (starter == null)
            {
                starter = m_Catalog.Items
                    .Where(item => item != null && item.Size >= 1 && item.Size <= BuqiRunRules.BoardSlotCount)
                    .OrderBy(item => item.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
            }

            if (starter == null)
            {
                error = "No legal starter item is available in local config.";
                return false;
            }

            string instanceId = economy.CreateInstanceId();
            economy.Items[instanceId] = new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = starter.Id,
                Quality = BuqiRunItemQuality.Common,
            };
            economy.Run.BoardInstanceIds[0] = instanceId;
            error = string.Empty;
            return true;
        }

        private bool TryBuildPlayerSnapshot(
            BuqiRunEconomySnapshot economy,
            out BuildSnapshot snapshot,
            out string error)
        {
            snapshot = new BuildSnapshot
            {
                SnapshotId = $"run-{economy.Run.RunSeed}-day-{economy.Run.Day}-{economy.Run.Phase.ToString().ToLowerInvariant()}",
                ContentVersion = economy.Run.ContentVersion,
                ArchetypeId = "local-player-run",
                InitialExecution = Math.Max(
                    1,
                    m_Catalog.SourceCatalog.Global.InitialExecution == 0
                        ? 100
                        : m_Catalog.SourceCatalog.Global.InitialExecution),
            };

            if (!TryReadAnchoredBoardPlacements(economy.Run.BoardInstanceIds, economy, out List<BoardPlacement> placements, out error))
            {
                snapshot = null;
                return false;
            }

            foreach (BoardPlacement placement in placements)
            {
                if (!TryMapQuality(placement.Item.Quality, out int quality, out error))
                {
                    snapshot = null;
                    return false;
                }

                snapshot.Items.Add(new ItemInstance
                {
                    InstanceId = placement.Item.InstanceId,
                    DefinitionId = placement.Item.DefinitionId,
                    Quality = quality,
                    AnchorSlot = placement.AnchorSlot,
                    AnnotationId = placement.Item.RefinementId ?? string.Empty,
                });
            }

            if (snapshot.Items.Count == 0)
            {
                error = "Player build must contain at least one board item.";
                snapshot = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateDeployment(
            IReadOnlyList<string> board,
            IReadOnlyList<string> storage,
            BuqiRunEconomySnapshot economy,
            out List<BoardPlacement> boardPlacements,
            out string error)
        {
            error = string.Empty;
            boardPlacements = null;
            if (board == null || board.Count != BuqiRunRules.BoardSlotCount)
            {
                error = "Deployment board slot count is invalid.";
                return false;
            }

            if (storage == null || storage.Count != BuqiRunRules.StorageSlotCount)
            {
                error = "Deployment storage slot count is invalid.";
                return false;
            }

            if (!TryReadExpandedBoardPlacements(board, economy, out boardPlacements, out error))
                return false;

            var proposedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BoardPlacement placement in boardPlacements)
            {
                if (!proposedIds.Add(placement.Item.InstanceId))
                {
                    error = "Deployment cannot place the same board instance more than once.";
                    return false;
                }
            }

            for (int index = 0; index < storage.Count; index++)
            {
                string instanceId = storage[index];
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    error = $"Storage slot {index} contains an invalid instance id.";
                    return false;
                }

                if (!economy.Items.ContainsKey(instanceId))
                {
                    error = "Deployment references an unknown item instance.";
                    return false;
                }

                if (!proposedIds.Add(instanceId))
                {
                    error = "Deployment cannot place the same item in board and storage.";
                    return false;
                }
            }

            if (!proposedIds.SetEquals(economy.Items.Keys))
            {
                error = "Deployment snapshot does not exactly match the owned item instances.";
                return false;
            }

            return true;
        }

        private bool TryReadExpandedBoardPlacements(
            IReadOnlyList<string> board,
            BuqiRunEconomySnapshot economy,
            out List<BoardPlacement> placements,
            out string error)
        {
            placements = new List<BoardPlacement>();
            error = string.Empty;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int slot = 0; slot < board.Count; slot++)
            {
                string instanceId = board[slot];
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    error = $"Board slot {slot} contains an invalid instance id.";
                    return false;
                }

                if (!economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    error = "Board references an unknown item instance.";
                    return false;
                }

                if (!m_ItemCatalog.TryGet(item.DefinitionId, out BuqiRunItemDefinition definition))
                {
                    error = $"Definition '{item.DefinitionId}' is unavailable.";
                    return false;
                }

                if (definition.Size < 1 || definition.Size > BuqiRunRules.BoardSlotCount)
                {
                    error = $"Definition '{item.DefinitionId}' has an invalid size.";
                    return false;
                }

                if (!seen.Add(instanceId))
                {
                    error = "Board cannot place the same item instance more than once.";
                    return false;
                }

                if (slot + definition.Size > board.Count)
                {
                    error = "Board placement exceeds the board bounds.";
                    return false;
                }

                for (int offset = 0; offset < definition.Size; offset++)
                {
                    if (!string.Equals(board[slot + offset], instanceId, StringComparison.Ordinal))
                    {
                        error = "Board placement does not cover the full item span.";
                        return false;
                    }
                }

                placements.Add(new BoardPlacement(item, slot));
                slot += definition.Size - 1;
            }

            return true;
        }

        private bool TryReadAnchoredBoardPlacements(
            IReadOnlyList<string> board,
            BuqiRunEconomySnapshot economy,
            out List<BoardPlacement> placements,
            out string error)
        {
            placements = new List<BoardPlacement>();
            error = string.Empty;
            var occupied = new bool[BuqiRunRules.BoardSlotCount];
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int slot = 0; slot < board.Count; slot++)
            {
                string instanceId = board[slot];
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    error = $"Board slot {slot} contains an invalid instance id.";
                    return false;
                }

                if (!economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    error = "Board references an unknown item instance.";
                    return false;
                }

                if (!m_ItemCatalog.TryGet(item.DefinitionId, out BuqiRunItemDefinition definition))
                {
                    error = $"Definition '{item.DefinitionId}' is unavailable.";
                    return false;
                }

                if (!seen.Add(instanceId))
                {
                    error = "Board cannot anchor the same item more than once.";
                    return false;
                }

                if (definition.Size < 1 || slot + definition.Size > board.Count)
                {
                    error = "Board placement exceeds the board bounds.";
                    return false;
                }

                for (int offset = 0; offset < definition.Size; offset++)
                {
                    if (occupied[slot + offset])
                    {
                        error = "Board placements overlap.";
                        return false;
                    }

                    occupied[slot + offset] = true;
                }

                placements.Add(new BoardPlacement(item, slot));
            }

            return true;
        }

        private bool TryMapQuality(BuqiRunItemQuality quality, out int mappedQuality, out string error)
        {
            switch (quality)
            {
                case BuqiRunItemQuality.Common:
                    mappedQuality = (int)BuqiQuality.Normal;
                    error = string.Empty;
                    return true;
                case BuqiRunItemQuality.Improved:
                    mappedQuality = (int)BuqiQuality.Improved;
                    error = string.Empty;
                    return true;
                case BuqiRunItemQuality.Finalized:
                    mappedQuality = (int)BuqiQuality.Fixed;
                    error = string.Empty;
                    return true;
                default:
                    mappedQuality = default;
                    error = $"Unknown item quality '{quality}' is not supported.";
                    return false;
            }
        }

        private bool TryValidateContentVersion(string contentVersion, out string error)
        {
            if (string.Equals(contentVersion, m_Definitions.ContentVersion, StringComparison.Ordinal))
            {
                error = string.Empty;
                return true;
            }

            error = $"Content version mismatch. Save has '{contentVersion}', expected '{m_Definitions.ContentVersion}'.";
            return false;
        }

        private static BuqiLocalOpponentProvider CreateBattleProvider(
            BuqiUIDemoCatalog catalog,
            BuqiUIDemoControllerOptions options)
        {
            IReadOnlyList<string> pveIds = options.PveOpponentIds ?? s_DefaultPveOpponentIds;
            IReadOnlyList<string> pvpIds = options.PvpOpponentIds ?? s_DefaultPvpOpponentIds;
            var adapter = new BuqiLocalOpponentPoolAdapter(pveIds, pvpIds);
            if (!adapter.TryCreate(catalog.SourceCatalog, out BuqiLocalOpponentPool pool, out string error))
                throw new ArgumentException(error, nameof(options));

            return new BuqiLocalOpponentProvider(pool);
        }

        private static IBuqiRunStore CreateDefaultStore()
        {
            string root = Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new BuqiFileRunStore(Path.Combine(root, "buqi-day-run-demo-save.json"));
        }

        private static bool IsEncounterShop(BuqiRunDemoState state)
        {
            return state.Presentation == BuqiRunDemoPresentation.Encounter &&
                   state.Encounter != null &&
                   state.Encounter.Kind == BuqiRunEncounterKind.Shop;
        }

        private static bool IsEncounterEvent(BuqiRunDemoState state)
        {
            return state.Presentation == BuqiRunDemoPresentation.Encounter &&
                   state.Encounter != null &&
                   state.Encounter.Kind == BuqiRunEncounterKind.Event;
        }

        private static BuqiUIDemoPhase CurrentViewPhase(BuqiRunDemoState state)
        {
            switch (state.Presentation)
            {
                case BuqiRunDemoPresentation.OperationChoice:
                    return BuqiUIDemoPhase.OperationChoice;
                case BuqiRunDemoPresentation.Encounter:
                    return IsEncounterEvent(state) ? BuqiUIDemoPhase.Event : BuqiUIDemoPhase.Shop;
                case BuqiRunDemoPresentation.PveSelection:
                    return BuqiUIDemoPhase.PveSelection;
                case BuqiRunDemoPresentation.BattleReplay:
                    return BuqiUIDemoPhase.BattleReplay;
                case BuqiRunDemoPresentation.BattleSummary:
                    return BuqiUIDemoPhase.BattleSummary;
                case BuqiRunDemoPresentation.DaySettlement:
                    return BuqiUIDemoPhase.RoundSettlement;
                case BuqiRunDemoPresentation.TribulationRoute:
                    return BuqiUIDemoPhase.TribulationRoute;
                case BuqiRunDemoPresentation.TribulationStage:
                    return BuqiUIDemoPhase.TribulationStage;
                case BuqiRunDemoPresentation.RunTerminal:
                default:
                    return BuqiUIDemoPhase.RunTerminal;
            }
        }

        private static void EnsureVisitedPhase(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            if (!state.VisitedPhases.Contains(phase))
                state.VisitedPhases.Add(phase);
        }

        private static string CreateCommandId(BuqiRunState state, string verb, string payload = "")
        {
            return string.Join(
                ":",
                "cmd",
                state.RunSeed,
                state.Day,
                state.Phase,
                state.Revision,
                verb,
                payload ?? string.Empty);
        }

        private static string CreateSettlementId(BuqiRunBattleSession session)
        {
            return BuqiText.Format("settlement:{0}", session.BattleId);
        }

        private static string[] CreateEmptyBoardSlots()
        {
            var result = new string[BuqiRunRules.BoardSlotCount];
            for (int index = 0; index < result.Length; index++)
                result[index] = string.Empty;
            return result;
        }

        private sealed class BoardPlacement
        {
            public BoardPlacement(BuqiRunItemInstance item, int anchorSlot)
            {
                Item = item;
                AnchorSlot = anchorSlot;
            }

            public BuqiRunItemInstance Item { get; }
            public int AnchorSlot { get; }
        }
    }

    internal static class BuqiRunDemoCodec
    {
        public static string EncodeEconomy(BuqiRunEconomySnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            var payload = new EconomyPayload { NextItemOrdinal = snapshot.NextItemOrdinal };
            foreach (BuqiRunItemInstance item in snapshot.Items.Values.OrderBy(value => value.InstanceId, StringComparer.Ordinal))
            {
                payload.Items.Add(new EconomyItemPayload
                {
                    InstanceId = item.InstanceId,
                    DefinitionId = item.DefinitionId,
                    Quality = (int)item.Quality,
                    RefinementId = item.RefinementId ?? string.Empty,
                });
            }

            return JsonUtility.ToJson(payload);
        }

        public static bool TryDecodeEconomy(
            BuqiRunState run,
            string json,
            out BuqiRunEconomySnapshot snapshot,
            out string error)
        {
            snapshot = null!;
            error = string.Empty;
            if (run == null)
            {
                error = "Run state is missing.";
                return false;
            }

            EconomyPayload payload;
            try
            {
                payload = string.IsNullOrWhiteSpace(json)
                    ? new EconomyPayload()
                    : JsonUtility.FromJson<EconomyPayload>(json);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            payload ??= new EconomyPayload();
            payload.Items ??= new List<EconomyItemPayload>();

            snapshot = new BuqiRunEconomySnapshot
            {
                Run = run.Clone(),
                NextItemOrdinal = Math.Max(1, payload.NextItemOrdinal),
            };

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyItemPayload item in payload.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || string.IsNullOrWhiteSpace(item.DefinitionId))
                {
                    error = "Economy payload contains an invalid item entry.";
                    snapshot = null!;
                    return false;
                }
                if (!Enum.IsDefined(typeof(BuqiRunItemQuality), item.Quality))
                {
                    error = $"Economy payload contains an invalid quality for '{item.InstanceId}'.";
                    snapshot = null!;
                    return false;
                }
                if (!seenIds.Add(item.InstanceId))
                {
                    error = $"Economy payload duplicates instance id '{item.InstanceId}'.";
                    snapshot = null!;
                    return false;
                }

                snapshot.Items[item.InstanceId] = new BuqiRunItemInstance
                {
                    InstanceId = item.InstanceId,
                    DefinitionId = item.DefinitionId,
                    Quality = (BuqiRunItemQuality)item.Quality,
                    RefinementId = item.RefinementId ?? string.Empty,
                };
            }

            foreach (string instanceId in run.BoardInstanceIds.Concat(run.StorageInstanceIds))
            {
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (!snapshot.Items.ContainsKey(instanceId))
                {
                    error = $"Economy payload is missing owned instance '{instanceId}'.";
                    snapshot = null!;
                    return false;
                }
            }

            return true;
        }

        public static string EncodeEncounter(BuqiRunEncounterState encounter)
        {
            if (encounter == null)
                return string.Empty;

            var payload = new EncounterPayload
            {
                EncounterId = encounter.EncounterId,
                Kind = (int)encounter.Kind,
                Day = encounter.Day,
                EncounterIndex = encounter.EncounterIndex,
                NextRngCursor = encounter.NextRngCursor,
                Resolved = encounter.Resolved,
                ResolutionId = encounter.ResolutionId ?? string.Empty,
                SelectedChoiceId = encounter.SelectedChoiceId ?? string.Empty,
            };
            payload.CandidateIds.AddRange(encounter.CandidateIds);
            return JsonUtility.ToJson(payload);
        }

        public static bool TryDecodeEncounter(
            BuqiRunState run,
            string json,
            out BuqiRunEncounterState encounter,
            out string error)
        {
            encounter = null;
            error = string.Empty;
            if (run == null)
            {
                error = "Run state is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
                return true;

            EncounterPayload payload;
            try
            {
                payload = JsonUtility.FromJson<EncounterPayload>(json);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (run.Phase != BuqiRunPhase.Encounter)
            {
                error = "Encounter payload is only valid during the encounter phase.";
                return false;
            }
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncounterId))
            {
                error = "Encounter payload is incomplete.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiRunEncounterKind), payload.Kind))
            {
                error = "Encounter payload kind is invalid.";
                return false;
            }
            if (payload.Day != run.Day || payload.EncounterIndex != run.EncounterIndex)
            {
                error = "Encounter payload does not match the active day or encounter index.";
                return false;
            }
            if (payload.NextRngCursor < 0)
            {
                error = "Encounter payload has an invalid RNG cursor.";
                return false;
            }
            if (payload.Resolved)
            {
                error = "Encounter payload cannot already be resolved.";
                return false;
            }
            if (payload.CandidateIds == null || payload.CandidateIds.Count == 0)
            {
                error = "Encounter payload candidates are missing.";
                return false;
            }
            var candidateIds = new List<string>(payload.CandidateIds.Count);
            var seenCandidateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string candidateId in payload.CandidateIds)
            {
                if (string.IsNullOrWhiteSpace(candidateId))
                {
                    error = "Encounter payload contains an invalid candidate id.";
                    return false;
                }
                if (!seenCandidateIds.Add(candidateId))
                {
                    error = $"Encounter payload duplicates candidate id '{candidateId}'.";
                    return false;
                }

                candidateIds.Add(candidateId);
            }

            encounter = new BuqiRunEncounterState
            {
                EncounterId = payload.EncounterId,
                Kind = (BuqiRunEncounterKind)payload.Kind,
                Day = payload.Day,
                EncounterIndex = payload.EncounterIndex,
                NextRngCursor = payload.NextRngCursor,
                Resolved = payload.Resolved,
                ResolutionId = payload.ResolutionId ?? string.Empty,
                SelectedChoiceId = payload.SelectedChoiceId ?? string.Empty,
                CandidateIds = candidateIds,
            };
            return true;
        }

        public static string EncodeBattle(BuqiRunBattleSession session)
        {
            if (session == null)
                return string.Empty;

            var payload = new BattlePayload
            {
                BattleId = session.BattleId,
                Kind = (int)session.Kind,
                OpponentId = session.OpponentId ?? string.Empty,
                NextRngCursor = session.NextRngCursor,
                RawOutcome = (int)session.RawOutcome,
                HasPveDifficulty = session.PveDifficulty.HasValue,
                PveDifficulty = session.PveDifficulty.HasValue ? (int)session.PveDifficulty.Value : 0,
                ReplayTitle = session.Replay?.Title ?? BuqiText.Format("{0} Battle", session.Kind),
                ReplayLeftName = session.Replay?.LeftName ?? "Player",
                ReplayRightName = session.Replay?.RightName ?? session.OpponentId ?? string.Empty,
                Request = BuildRequestPayload(session.Request),
                Result = BuildResultPayload(session.Result),
            };
            foreach (BattleEvent battleEvent in session.Log)
                payload.Log.Add(BuildEventPayload(battleEvent));
            return JsonUtility.ToJson(payload);
        }

        public static bool TryDecodeBattle(
            BuqiRunState run,
            string json,
            IItemDefinitionProvider definitions,
            out BuqiRunBattleSession session,
            out string error)
        {
            session = null;
            error = string.Empty;
            if (run == null)
            {
                error = "Run state is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                if (run.Phase == BuqiRunPhase.PvpBattle)
                {
                    error = "Battle payload is required for the active battle phase.";
                    return false;
                }

                return true;
            }

            BattlePayload payload;
            try
            {
                payload = JsonUtility.FromJson<BattlePayload>(json);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.BattleId) || payload.Request == null || payload.Result == null)
            {
                error = "Battle payload is incomplete.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiRunBattleKind), payload.Kind))
            {
                error = "Battle payload kind is invalid.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiRunRawBattleOutcome), payload.RawOutcome))
            {
                error = "Battle payload raw outcome is invalid.";
                return false;
            }
            if (payload.HasPveDifficulty && !Enum.IsDefined(typeof(BuqiPveDifficulty), payload.PveDifficulty))
            {
                error = "Battle payload PVE difficulty is invalid.";
                return false;
            }
            if (payload.Log == null)
            {
                error = "Battle payload log is missing.";
                return false;
            }

            BuqiRunBattleKind kind = (BuqiRunBattleKind)payload.Kind;
            bool settled = IsSettledBattle(run, payload.BattleId);
            if (run.Phase == BuqiRunPhase.PveBattle && kind != BuqiRunBattleKind.Pve)
            {
                error = "Battle payload kind does not match the active PVE phase.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.PvpBattle &&
                kind != BuqiRunBattleKind.Pvp &&
                !(kind == BuqiRunBattleKind.Pve && settled))
            {
                error = "Battle payload kind does not match the active PVP phase.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.Encounter)
            {
                error = "Battle payload is not valid during the encounter phase.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.DaySettlement &&
                (kind != BuqiRunBattleKind.Pvp || !settled))
            {
                error = "Only a settled PVP battle payload is valid during day settlement.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.TribulationRoute &&
                (kind != BuqiRunBattleKind.Pvp || !settled))
            {
                error = "Only the settled day nine PVP battle is valid during route choice.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.TribulationStage)
            {
                error = "Battle payload is not valid during tribulation stages.";
                return false;
            }
            if (run.Phase == BuqiRunPhase.RunTerminal)
            {
                bool validEarlyDefeatBattle = run.TribulationRoute == BuqiTribulationRoute.None &&
                                               settled &&
                                               ((run.Period == BuqiRunPeriod.DuskPve && kind == BuqiRunBattleKind.Pve) ||
                                                (run.Period == BuqiRunPeriod.NightPvp && kind == BuqiRunBattleKind.Pvp));
                if (!validEarlyDefeatBattle)
                {
                    error = "Only the settled life-depletion battle is valid after an early defeat.";
                    return false;
                }
            }

            if (!TryValidateBattleRequest(payload.Request, out error) ||
                !TryValidateBattleResult(payload.Result, out error) ||
                !TryValidateBattleLog(payload.Log, out error))
            {
                return false;
            }

            BattleRequest request = ReadRequestPayload(payload.Request);
            BattleResult result = ReadResultPayload(payload.Result);
            List<BattleEvent> log = payload.Log.Select(ReadEventPayload).ToList();

            session = new BuqiRunBattleSession
            {
                BattleId = payload.BattleId,
                Kind = kind,
                PveDifficulty = payload.HasPveDifficulty
                    ? (BuqiPveDifficulty?)payload.PveDifficulty
                    : null,
                OpponentId = payload.OpponentId ?? string.Empty,
                NextRngCursor = payload.NextRngCursor,
                Request = request,
                Result = result,
                Log = log,
                Replay = new BattleReplayData
                {
                    Title = string.IsNullOrEmpty(payload.ReplayTitle)
                        ? BuqiText.Format("{0} Battle", (BuqiRunBattleKind)payload.Kind)
                        : payload.ReplayTitle,
                    LeftName = string.IsNullOrEmpty(payload.ReplayLeftName)
                        ? "Player"
                        : payload.ReplayLeftName,
                    RightName = string.IsNullOrEmpty(payload.ReplayRightName)
                        ? payload.OpponentId ?? string.Empty
                        : payload.ReplayRightName,
                    LeftBuild = CloneBuildSnapshot(request.Left),
                    RightBuild = CloneBuildSnapshot(request.Right),
                    Result = CloneBattleResult(result),
                    Log = CloneBattleLog(log),
                    Definitions = definitions,
                },
                RawOutcome = (BuqiRunRawBattleOutcome)payload.RawOutcome,
            };
            return true;
        }

        internal static bool IsSettledBattle(BuqiRunState run, string battleId)
        {
            return run != null &&
                   !string.IsNullOrWhiteSpace(battleId) &&
                   run.AppliedSettlementIds.Contains(BuqiText.Format("settlement:{0}", battleId));
        }

        private static bool TryValidateBattleRequest(RequestPayload payload, out string error)
        {
            error = string.Empty;
            if (payload.Left == null || payload.Right == null)
            {
                error = "Battle payload request sides are missing.";
                return false;
            }

            return TryValidateBuildPayload(payload.Left, "left", out error) &&
                   TryValidateBuildPayload(payload.Right, "right", out error);
        }

        private static bool TryValidateBuildPayload(BuildPayload payload, string label, out string error)
        {
            error = string.Empty;
            if (payload == null || string.IsNullOrWhiteSpace(payload.SnapshotId) || payload.Items == null)
            {
                error = $"Battle payload {label} build is incomplete.";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemPayload item in payload.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || string.IsNullOrWhiteSpace(item.DefinitionId))
                {
                    error = $"Battle payload {label} build contains an invalid item.";
                    return false;
                }
                if (!seenIds.Add(item.InstanceId))
                {
                    error = $"Battle payload {label} build duplicates instance id '{item.InstanceId}'.";
                    return false;
                }
                if (!Enum.IsDefined(typeof(BuqiQuality), item.Quality))
                {
                    error = $"Battle payload {label} build contains an invalid quality.";
                    return false;
                }
                if (item.TemporaryModifiers == null)
                {
                    error = $"Battle payload {label} build modifiers are missing.";
                    return false;
                }
                foreach (ModifierPayload modifier in item.TemporaryModifiers)
                {
                    if (modifier == null ||
                        !Enum.IsDefined(typeof(BuqiEffect), modifier.Effect) ||
                        modifier.RemainingTicks < 0)
                    {
                        error = $"Battle payload {label} build contains an invalid modifier.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryValidateBattleResult(ResultPayload payload, out string error)
        {
            error = string.Empty;
            if (payload == null || !Enum.IsDefined(typeof(BattleOutcome), payload.Outcome))
            {
                error = "Battle payload result is invalid.";
                return false;
            }

            return true;
        }

        private static bool TryValidateBattleLog(List<EventPayload> payloads, out string error)
        {
            error = string.Empty;
            foreach (EventPayload payload in payloads)
            {
                if (payload == null ||
                    !Enum.IsDefined(typeof(BuqiEventPhase), payload.Phase) ||
                    !Enum.IsDefined(typeof(BuqiEventType), payload.Type))
                {
                    error = "Battle payload log contains an invalid event.";
                    return false;
                }
            }

            return true;
        }

        public static BuqiRunBattleSession CloneBattleSession(BuqiRunBattleSession source)
        {
            if (source == null)
                return null;

            return new BuqiRunBattleSession
            {
                BattleId = source.BattleId,
                Kind = source.Kind,
                PveDifficulty = source.PveDifficulty,
                OpponentId = source.OpponentId,
                NextRngCursor = source.NextRngCursor,
                Request = ReadRequestPayload(BuildRequestPayload(source.Request)),
                Result = CloneBattleResult(source.Result),
                Log = CloneBattleLog(source.Log),
                Replay = source.Replay == null ? null : new BattleReplayData
                {
                    Title = source.Replay.Title,
                    LeftName = source.Replay.LeftName,
                    RightName = source.Replay.RightName,
                    LeftBuild = CloneBuildSnapshot(source.Replay.LeftBuild),
                    RightBuild = CloneBuildSnapshot(source.Replay.RightBuild),
                    Result = CloneBattleResult(source.Replay.Result),
                    Log = CloneBattleLog(source.Replay.Log),
                    Definitions = source.Replay.Definitions,
                },
                RawOutcome = source.RawOutcome,
            };
        }

        public static BuildSnapshot CloneBuildSnapshot(BuildSnapshot source)
        {
            if (source == null)
                return null;

            return ReadBuildPayload(BuildBuildPayload(source));
        }

        public static BattleResult CloneBattleResult(BattleResult source)
        {
            if (source == null)
                return null;

            return ReadResultPayload(BuildResultPayload(source));
        }

        public static List<BattleEvent> CloneBattleLog(IReadOnlyList<BattleEvent> source)
        {
            var result = new List<BattleEvent>();
            if (source == null)
                return result;

            foreach (BattleEvent battleEvent in source)
                result.Add(ReadEventPayload(BuildEventPayload(battleEvent)));
            return result;
        }

        public static BuqiRunBattleSummary CloneSummary(BuqiRunBattleSummary source)
        {
            if (source == null)
                return new BuqiRunBattleSummary();

            return new BuqiRunBattleSummary
            {
                RawOutcome = source.RawOutcome,
                BattleLogHash = source.BattleLogHash,
                TopSourceInstanceId = source.TopSourceInstanceId,
                TopContribution = source.TopContribution,
                KeyInterruptionReason = source.KeyInterruptionReason,
                OverloadLoss = source.OverloadLoss,
                FactLines = source.FactLines == null
                    ? new List<string>()
                    : new List<string>(source.FactLines),
            };
        }

        private static RequestPayload BuildRequestPayload(BattleRequest request)
        {
            if (request == null)
                return null;

            return new RequestPayload
            {
                RuleVersion = request.RuleVersion,
                BattleSeed = request.BattleSeed.ToString(),
                RoundIndex = request.RoundIndex,
                Left = BuildBuildPayload(request.Left),
                Right = BuildBuildPayload(request.Right),
            };
        }

        private static BattleRequest ReadRequestPayload(RequestPayload payload)
        {
            return new BattleRequest
            {
                RuleVersion = payload.RuleVersion ?? string.Empty,
                BattleSeed = ulong.TryParse(payload.BattleSeed, out ulong seed) ? seed : 0UL,
                RoundIndex = payload.RoundIndex,
                Left = ReadBuildPayload(payload.Left),
                Right = ReadBuildPayload(payload.Right),
            };
        }

        private static BuildPayload BuildBuildPayload(BuildSnapshot build)
        {
            if (build == null)
                return null;

            var payload = new BuildPayload
            {
                SnapshotId = build.SnapshotId ?? string.Empty,
                ContentVersion = build.ContentVersion ?? string.Empty,
                ArchetypeId = build.ArchetypeId ?? string.Empty,
                InitialExecution = build.InitialExecution,
                InitialBuffer = build.InitialBuffer,
                InitialNoiseDebt = build.InitialNoiseDebt,
            };
            foreach (ItemInstance item in build.Items)
                payload.Items.Add(BuildItemPayload(item));
            return payload;
        }

        private static BuildSnapshot ReadBuildPayload(BuildPayload payload)
        {
            if (payload == null)
                return null;

            var build = new BuildSnapshot
            {
                SnapshotId = payload.SnapshotId ?? string.Empty,
                ContentVersion = payload.ContentVersion ?? string.Empty,
                ArchetypeId = payload.ArchetypeId ?? string.Empty,
                InitialExecution = payload.InitialExecution,
                InitialBuffer = payload.InitialBuffer,
                InitialNoiseDebt = payload.InitialNoiseDebt,
            };
            foreach (ItemPayload item in payload.Items)
                build.Items.Add(ReadItemPayload(item));
            return build;
        }

        private static ItemPayload BuildItemPayload(ItemInstance item)
        {
            if (item == null)
                return null;

            var payload = new ItemPayload
            {
                InstanceId = item.InstanceId ?? string.Empty,
                DefinitionId = item.DefinitionId ?? string.Empty,
                Quality = item.Quality,
                AnchorSlot = item.AnchorSlot,
                AnnotationId = item.AnnotationId ?? string.Empty,
            };
            foreach (TemporaryModifier modifier in item.TemporaryModifiers)
                payload.TemporaryModifiers.Add(BuildModifierPayload(modifier));
            return payload;
        }

        private static ItemInstance ReadItemPayload(ItemPayload payload)
        {
            var item = new ItemInstance
            {
                InstanceId = payload.InstanceId ?? string.Empty,
                DefinitionId = payload.DefinitionId ?? string.Empty,
                Quality = payload.Quality,
                AnchorSlot = payload.AnchorSlot,
                AnnotationId = payload.AnnotationId ?? string.Empty,
            };
            foreach (ModifierPayload modifier in payload.TemporaryModifiers)
                item.TemporaryModifiers.Add(ReadModifierPayload(modifier));
            return item;
        }

        private static ModifierPayload BuildModifierPayload(TemporaryModifier modifier)
        {
            if (modifier == null)
                return null;

            return new ModifierPayload
            {
                Effect = (int)modifier.Effect,
                SourceInstanceId = modifier.SourceInstanceId ?? string.Empty,
                RemainingTicks = modifier.RemainingTicks,
                Bps = modifier.Bps,
            };
        }

        private static TemporaryModifier ReadModifierPayload(ModifierPayload payload)
        {
            return new TemporaryModifier
            {
                Effect = (Game.Hot.Buqi.Battle.BuqiEffect)payload.Effect,
                SourceInstanceId = payload.SourceInstanceId ?? string.Empty,
                RemainingTicks = payload.RemainingTicks,
                Bps = payload.Bps,
            };
        }

        private static ResultPayload BuildResultPayload(BattleResult result)
        {
            if (result == null)
                return null;

            return new ResultPayload
            {
                RuleVersion = result.RuleVersion ?? string.Empty,
                SimulationVersion = result.SimulationVersion ?? string.Empty,
                ContentVersion = result.ContentVersion ?? string.Empty,
                BattleSeed = result.BattleSeed.ToString(),
                RoundIndex = result.RoundIndex,
                Outcome = (int)result.Outcome,
                DurationTicks = result.DurationTicks,
                LeftExecution = result.LeftExecution,
                RightExecution = result.RightExecution,
                LeftBuffer = result.LeftBuffer,
                RightBuffer = result.RightBuffer,
                LeftNoise = result.LeftNoise,
                RightNoise = result.RightNoise,
                TerminationReason = result.TerminationReason ?? string.Empty,
                BattleLogHash = result.BattleLogHash ?? string.Empty,
                LeftSnapshotHash = result.LeftSnapshotHash ?? string.Empty,
                RightSnapshotHash = result.RightSnapshotHash ?? string.Empty,
            };
        }

        private static BattleResult ReadResultPayload(ResultPayload payload)
        {
            return new BattleResult
            {
                RuleVersion = payload.RuleVersion ?? string.Empty,
                SimulationVersion = payload.SimulationVersion ?? string.Empty,
                ContentVersion = payload.ContentVersion ?? string.Empty,
                BattleSeed = ulong.TryParse(payload.BattleSeed, out ulong seed) ? seed : 0UL,
                RoundIndex = payload.RoundIndex,
                Outcome = (BattleOutcome)payload.Outcome,
                DurationTicks = payload.DurationTicks,
                LeftExecution = payload.LeftExecution,
                RightExecution = payload.RightExecution,
                LeftBuffer = payload.LeftBuffer,
                RightBuffer = payload.RightBuffer,
                LeftNoise = payload.LeftNoise,
                RightNoise = payload.RightNoise,
                TerminationReason = payload.TerminationReason ?? string.Empty,
                BattleLogHash = payload.BattleLogHash ?? string.Empty,
                LeftSnapshotHash = payload.LeftSnapshotHash ?? string.Empty,
                RightSnapshotHash = payload.RightSnapshotHash ?? string.Empty,
            };
        }

        private static EventPayload BuildEventPayload(BattleEvent battleEvent)
        {
            if (battleEvent == null)
                return null;

            return new EventPayload
            {
                Sequence = battleEvent.Sequence,
                Tick = battleEvent.Tick,
                Phase = (int)battleEvent.Phase,
                ChainDepth = battleEvent.ChainDepth,
                ChainId = battleEvent.ChainId ?? string.Empty,
                ActorInstanceId = battleEvent.ActorInstanceId ?? string.Empty,
                SourceInstanceId = battleEvent.SourceInstanceId ?? string.Empty,
                TargetInstanceId = battleEvent.TargetInstanceId ?? string.Empty,
                Type = (int)battleEvent.Type,
                Amount = battleEvent.Amount,
                EffectId = battleEvent.EffectId ?? string.Empty,
                ReasonCode = battleEvent.ReasonCode ?? string.Empty,
            };
        }

        private static BattleEvent ReadEventPayload(EventPayload payload)
        {
            return new BattleEvent
            {
                Sequence = payload.Sequence,
                Tick = payload.Tick,
                Phase = (BuqiEventPhase)payload.Phase,
                ChainDepth = payload.ChainDepth,
                ChainId = payload.ChainId ?? string.Empty,
                ActorInstanceId = payload.ActorInstanceId ?? string.Empty,
                SourceInstanceId = payload.SourceInstanceId ?? string.Empty,
                TargetInstanceId = payload.TargetInstanceId ?? string.Empty,
                Type = (BuqiEventType)payload.Type,
                Amount = payload.Amount,
                EffectId = payload.EffectId ?? string.Empty,
                ReasonCode = payload.ReasonCode ?? string.Empty,
            };
        }

        [Serializable]
        private sealed class EconomyPayload
        {
            public int NextItemOrdinal = 1;
            public List<EconomyItemPayload> Items = new List<EconomyItemPayload>();
        }

        [Serializable]
        private sealed class EconomyItemPayload
        {
            public string InstanceId = string.Empty;
            public string DefinitionId = string.Empty;
            public int Quality;
            public string RefinementId = string.Empty;
        }

        [Serializable]
        private sealed class EncounterPayload
        {
            public string EncounterId = string.Empty;
            public int Kind;
            public int Day;
            public int EncounterIndex;
            public int NextRngCursor;
            public bool Resolved;
            public string ResolutionId = string.Empty;
            public string SelectedChoiceId = string.Empty;
            public List<string> CandidateIds = new List<string>();
        }

        [Serializable]
        private sealed class BattlePayload
        {
            public string BattleId = string.Empty;
            public int Kind;
            public string OpponentId = string.Empty;
            public int NextRngCursor;
            public int RawOutcome;
            public bool HasPveDifficulty;
            public int PveDifficulty;
            public string ReplayTitle = string.Empty;
            public string ReplayLeftName = string.Empty;
            public string ReplayRightName = string.Empty;
            public RequestPayload Request = null!;
            public ResultPayload Result = null!;
            public List<EventPayload> Log = new List<EventPayload>();
        }

        [Serializable]
        private sealed class RequestPayload
        {
            public string RuleVersion = string.Empty;
            public string BattleSeed = string.Empty;
            public int RoundIndex;
            public BuildPayload Left = null!;
            public BuildPayload Right = null!;
        }

        [Serializable]
        private sealed class BuildPayload
        {
            public string SnapshotId = string.Empty;
            public string ContentVersion = string.Empty;
            public string ArchetypeId = string.Empty;
            public int InitialExecution = 100;
            public int InitialBuffer;
            public int InitialNoiseDebt;
            public List<ItemPayload> Items = new List<ItemPayload>();
        }

        [Serializable]
        private sealed class ItemPayload
        {
            public string InstanceId = string.Empty;
            public string DefinitionId = string.Empty;
            public int Quality;
            public int AnchorSlot;
            public string AnnotationId = string.Empty;
            public List<ModifierPayload> TemporaryModifiers = new List<ModifierPayload>();
        }

        [Serializable]
        private sealed class ModifierPayload
        {
            public int Effect;
            public string SourceInstanceId = string.Empty;
            public int RemainingTicks;
            public int Bps;
        }

        [Serializable]
        private sealed class ResultPayload
        {
            public string RuleVersion = string.Empty;
            public string SimulationVersion = string.Empty;
            public string ContentVersion = string.Empty;
            public string BattleSeed = string.Empty;
            public int RoundIndex;
            public int Outcome;
            public int DurationTicks;
            public int LeftExecution;
            public int RightExecution;
            public int LeftBuffer;
            public int RightBuffer;
            public int LeftNoise;
            public int RightNoise;
            public string TerminationReason = string.Empty;
            public string BattleLogHash = string.Empty;
            public string LeftSnapshotHash = string.Empty;
            public string RightSnapshotHash = string.Empty;
        }

        [Serializable]
        private sealed class EventPayload
        {
            public int Sequence;
            public int Tick;
            public int Phase;
            public int ChainDepth;
            public string ChainId = string.Empty;
            public string ActorInstanceId = string.Empty;
            public string SourceInstanceId = string.Empty;
            public string TargetInstanceId = string.Empty;
            public int Type;
            public int Amount;
            public string EffectId = string.Empty;
            public string ReasonCode = string.Empty;
        }
    }
}
