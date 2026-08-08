using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Integration;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiUIDemoController
    {
        private readonly BuqiUIDemoCatalog m_Catalog;
        private readonly BuqiRunDemoOrchestrator m_Orchestrator;

        private BuqiUIDemoController(BuqiUIDemoCatalog catalog, BuqiRunDemoOrchestrator orchestrator)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            RefreshView();
        }

        public BuqiUIDemoView View { get; private set; }

        public BattleReplayData CurrentReplay => m_Orchestrator.BuildReplayData();

        public static BuqiUIDemoController Create(BuqiUIDemoCatalog catalog)
        {
            if (!TryCreate(catalog, null, out BuqiUIDemoController controller, out string error))
                throw new InvalidOperationException(error);
            return controller;
        }

        public static bool TryCreate(
            BuqiUIDemoCatalog catalog,
            BuqiUIDemoControllerOptions options,
            out BuqiUIDemoController controller,
            out string error)
        {
            controller = null;
            if (catalog == null)
            {
                error = "Demo catalog is unavailable.";
                return false;
            }

            try
            {
                var orchestrator = new BuqiRunDemoOrchestrator(catalog, options);
                if (!orchestrator.TryInitialize(out error))
                    return false;

                controller = new BuqiUIDemoController(catalog, orchestrator);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public BuqiUIDemoCommandResult Execute(BuqiUIDemoCommand command)
        {
            if (command == null)
                return Rejected("Command is null.");

            switch (command.Type)
            {
                case BuqiUIDemoCommandType.OpenDragDeploy:
                    return CanOpenDeploy()
                        ? AcceptedWithoutMutation()
                        : Rejected("Current phase cannot open deployment.");

                case BuqiUIDemoCommandType.PreviousPhase:
                    return Rejected("Current phase does not support going back.");

                case BuqiUIDemoCommandType.Restart:
                    if (!m_Orchestrator.Restart(out string restartError))
                        return Rejected(restartError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.BuyOffer:
                    if (!m_Orchestrator.TryPurchase(command.PrimaryId, out string buyError))
                        return Rejected(buyError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SelectChoice:
                    if (!m_Orchestrator.TryResolveEvent(command.PrimaryId, out string eventError))
                        return Rejected(eventError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.ApplyDeployment:
                    if (!m_Orchestrator.TryApplyDeployment(command.Deployment, out string deploymentError))
                        return Rejected(deploymentError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.NextPhase:
                    if (!m_Orchestrator.TryAdvance(out string advanceError))
                        return Rejected(advanceError);
                    RefreshView();
                    return Accepted();

                default:
                    return Rejected("Unsupported runtime command.");
            }
        }

        private bool CanOpenDeploy()
        {
            return View.Phase == BuqiUIDemoPhase.Shop
                || View.Phase == BuqiUIDemoPhase.Event
                || View.Phase == BuqiUIDemoPhase.RoundSettlement;
        }

        private void RefreshView()
        {
            BuqiRunDemoState state = m_Orchestrator.State;
            m_Catalog.SetRuntimeItemDefinitions(
                state.Economy.Items.Values.Select(item =>
                    new KeyValuePair<string, string>(item.InstanceId, item.DefinitionId)));
            BuqiUIDemoPhase phase = ResolvePhase(state);
            BuqiUIDemoView view = new BuqiUIDemoView
            {
                Phase = phase,
                Coins = state.Economy.Run.Coins,
                Wins = state.Economy.Run.Wins,
                Lives = state.Economy.Run.Lives,
                Round = state.Economy.Run.Day,
                ContextTitle = BuildTitle(state, phase),
                ContextBody = BuildBody(state, phase),
                PrimaryCommandLabel = BuildPrimaryLabel(phase),
                SecondaryCommandLabel = string.Empty,
                VisitedPhases = new List<BuqiUIDemoPhase>(state.VisitedPhases),
                BoardSlots = BuildBoardSlots(state.Economy),
                StorageSlots = BuildStorageSlots(state.Economy),
                Choices = BuildChoices(state, phase),
                ShopOffers = BuildOffers(state, phase),
                Opponent = BuildOpponent(state),
                Facts = BuildFacts(state),
            };

            View = view;
        }

        private IReadOnlyList<BuqiDemoItemView> BuildBoardSlots(BuqiRunEconomySnapshot economy)
        {
            var result = new BuqiDemoItemView[economy.Run.BoardInstanceIds.Count];
            for (int slot = 0; slot < economy.Run.BoardInstanceIds.Count; slot++)
            {
                if (result[slot] != null)
                    continue;

                string instanceId = economy.Run.BoardInstanceIds[slot];
                if (string.IsNullOrEmpty(instanceId) ||
                    !economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    result[slot] = new BuqiDemoItemView
                    {
                        Empty = true,
                        Slot = slot,
                    };
                    continue;
                }

                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                int size = definition?.Size ?? 1;
                for (int offset = 0; offset < size && slot + offset < result.Length; offset++)
                {
                    result[slot + offset] = new BuqiDemoItemView
                    {
                        Id = instanceId,
                        Name = definition?.Name ?? item.DefinitionId,
                        Description = BuildItemDescription(item, definition),
                        Size = size,
                        Price = definition?.Price ?? 0,
                        Slot = slot + offset,
                    };
                }
            }

            for (int slot = 0; slot < result.Length; slot++)
            {
                result[slot] ??= new BuqiDemoItemView
                {
                    Empty = true,
                    Slot = slot,
                };
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoItemView> BuildStorageSlots(BuqiRunEconomySnapshot economy)
        {
            var result = new List<BuqiDemoItemView>(economy.Run.StorageInstanceIds.Count);
            for (int slot = 0; slot < economy.Run.StorageInstanceIds.Count; slot++)
            {
                string instanceId = economy.Run.StorageInstanceIds[slot];
                if (string.IsNullOrEmpty(instanceId) ||
                    !economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    result.Add(new BuqiDemoItemView
                    {
                        Empty = true,
                        Slot = slot,
                    });
                    continue;
                }

                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                result.Add(new BuqiDemoItemView
                {
                    Id = instanceId,
                    Name = definition?.Name ?? item.DefinitionId,
                    Description = BuildItemDescription(item, definition),
                    Size = definition?.Size ?? 1,
                    Price = definition?.Price ?? 0,
                    Slot = slot,
                });
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoChoiceView> BuildChoices(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            if (phase != BuqiUIDemoPhase.Event || state.Encounter == null)
                return Array.Empty<BuqiDemoChoiceView>();

            var result = new List<BuqiDemoChoiceView>();
            foreach (string eventId in state.Encounter.CandidateIds)
            {
                BuqiDemoChoiceView source = m_Catalog.EventChoices.FirstOrDefault(choice =>
                    string.Equals(choice.Id, eventId, StringComparison.Ordinal));
                result.Add(new BuqiDemoChoiceView
                {
                    Id = eventId,
                    Title = source?.Title ?? eventId,
                    Description = source?.Description ?? string.Empty,
                });
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoOfferView> BuildOffers(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            if (phase != BuqiUIDemoPhase.Shop || state.Encounter == null)
                return Array.Empty<BuqiDemoOfferView>();

            var result = new List<BuqiDemoOfferView>();
            foreach (string definitionId in state.Encounter.CandidateIds)
            {
                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(definitionId);
                if (definition == null)
                    continue;

                result.Add(new BuqiDemoOfferView
                {
                    Id = definitionId,
                    Item = BuqiUIDemoCatalog.ItemView(definition),
                    Price = definition.Price,
                    Sold = false,
                });
            }

            return result;
        }

        private BuqiDemoOpponentView BuildOpponent(BuqiRunDemoState state)
        {
            if (state.Battle == null || state.Battle.Request?.Right == null)
                return null;

            var items = new List<BuqiDemoItemView>();
            foreach (ItemInstance item in state.Battle.Request.Right.Items.OrderBy(value => value.AnchorSlot))
            {
                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                items.Add(new BuqiDemoItemView
                {
                    Id = item.InstanceId,
                    Name = definition?.Name ?? item.DefinitionId,
                    Description = BuildBattleItemDescription(item, definition),
                    Size = definition?.Size ?? 1,
                    Slot = item.AnchorSlot,
                });
            }

            return new BuqiDemoOpponentView
            {
                Id = state.Battle.OpponentId,
                Name = state.Battle.Replay?.RightName ?? state.Battle.OpponentId,
                Build = state.Battle.Request.Right.SnapshotId,
                Items = items,
            };
        }

        private IReadOnlyList<BuqiDemoFactView> BuildFacts(BuqiRunDemoState state)
        {
            if (state.BattleSummary == null || state.BattleSummary.FactLines == null)
                return Array.Empty<BuqiDemoFactView>();

            var facts = new List<BuqiDemoFactView>();
            for (int index = 0; index < state.BattleSummary.FactLines.Length; index++)
            {
                facts.Add(new BuqiDemoFactView
                {
                    Title = index == 0 ? OutcomeTitle(state) : $"Fact {index + 1}",
                    Body = state.BattleSummary.FactLines[index],
                    Tick = index,
                });
            }

            if (facts.Count == 0 && state.Battle != null)
            {
                facts.Add(new BuqiDemoFactView
                {
                    Title = OutcomeTitle(state),
                    Body = state.Battle.Result?.TerminationReason ?? string.Empty,
                    Tick = 0,
                });
            }

            return facts;
        }

        private static string OutcomeTitle(BuqiRunDemoState state)
        {
            if (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw)
                return "Draw";
            return state.LastRawOutcome == BuqiRunRawBattleOutcome.OpponentWin ? "Defeat" : "Victory";
        }

        private static BuqiUIDemoPhase ResolvePhase(BuqiRunDemoState state)
        {
            switch (state.Presentation)
            {
                case BuqiRunDemoPresentation.Encounter:
                    return state.Encounter != null && state.Encounter.Kind == BuqiRunEncounterKind.Event
                        ? BuqiUIDemoPhase.Event
                        : BuqiUIDemoPhase.Shop;
                case BuqiRunDemoPresentation.BattleReplay:
                    return BuqiUIDemoPhase.BattleReplay;
                case BuqiRunDemoPresentation.BattleSummary:
                    return BuqiUIDemoPhase.BattleSummary;
                case BuqiRunDemoPresentation.DaySettlement:
                    return BuqiUIDemoPhase.RoundSettlement;
                case BuqiRunDemoPresentation.RunTerminal:
                default:
                    return BuqiUIDemoPhase.RunTerminal;
            }
        }

        private static string BuildTitle(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.Shop:
                case BuqiUIDemoPhase.Event:
                    return $"Day {state.Economy.Run.Day} Encounter {state.Economy.Run.EncounterIndex + 1}/{BuqiRunRules.EncountersPerDay}";
                case BuqiUIDemoPhase.BattleReplay:
                    return state.Economy.Run.Phase == BuqiRunPhase.PveBattle ? "PVE Battle" : "PVP Battle";
                case BuqiUIDemoPhase.BattleSummary:
                    string summaryKind = state.Battle == null
                        ? "Battle"
                        : state.Battle.Kind == Game.Hot.Buqi.Run.Core.BuqiRunBattleKind.Pve ? "PVE" : "PVP";
                    return state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                        ? $"{summaryKind} Summary (Draw counts as player win)"
                        : $"{summaryKind} Summary";
                case BuqiUIDemoPhase.RoundSettlement:
                    return $"Day {state.Economy.Run.Day} Settlement";
                case BuqiUIDemoPhase.RunTerminal:
                    return state.Economy.Run.Outcome == BuqiRunOutcome.Victory ? "Run Victory" : "Run Defeat";
                default:
                    return string.Empty;
            }
        }

        private static string BuildBody(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.Shop:
                    return "Frozen local shop. Buy one item or skip; successful commands advance immediately.";
                case BuqiUIDemoPhase.Event:
                    return "Frozen local event. Choose one explicit result; successful commands advance immediately.";
                case BuqiUIDemoPhase.BattleReplay:
                    return state.Economy.Run.Phase == BuqiRunPhase.PveBattle
                        ? "Player build is on the left; local preset PVE opponent is on the right."
                        : "Player build is on the left; local preset player PVP opponent is on the right.";
                case BuqiUIDemoPhase.BattleSummary:
                    return state.Battle == null || state.Battle.Kind == Game.Hot.Buqi.Run.Core.BuqiRunBattleKind.Pve
                        ? (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                            ? "Raw PVE result is Draw, but run settlement counts it as a player win."
                            : "PVE replay/log summary from the generated battle payload.")
                        : (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                            ? "Raw PVP result is Draw, but run settlement counts it as a player win."
                            : "PVP replay/log summary from the generated battle payload.");
                case BuqiUIDemoPhase.RoundSettlement:
                    return "Daily PVE/PVP results have been applied. Continue to generate the next day.";
                case BuqiUIDemoPhase.RunTerminal:
                    return state.Economy.Run.Outcome == BuqiRunOutcome.Victory
                        ? "Nine wins reached immediately."
                        : "Run lives reached zero immediately.";
                default:
                    return string.Empty;
            }
        }

        private static string BuildPrimaryLabel(BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.Shop:
                    return "Skip";
                case BuqiUIDemoPhase.Event:
                    return "Choose";
                case BuqiUIDemoPhase.RunTerminal:
                    return "Restart";
                default:
                    return "Continue";
            }
        }

        private static string BuildItemDescription(BuqiRunItemInstance item, BuqiUIDemoItemDefinition definition)
        {
            string quality = item.Quality.ToString();
            string refinement = string.IsNullOrEmpty(item.RefinementId) ? "none" : item.RefinementId;
            string baseText = definition == null ? item.DefinitionId : definition.Description;
            return $"{baseText} | {quality} | refine {refinement}";
        }

        private static string BuildBattleItemDescription(ItemInstance item, BuqiUIDemoItemDefinition definition)
        {
            string baseText = definition == null ? item.DefinitionId : definition.Description;
            return $"{baseText} | quality {item.Quality}";
        }

        private BuqiUIDemoCommandResult Accepted()
        {
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoCommandResult AcceptedWithoutMutation()
        {
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoCommandResult Rejected(string reason)
        {
            return new BuqiUIDemoCommandResult { Accepted = false, Reason = reason, View = View };
        }
    }
}
