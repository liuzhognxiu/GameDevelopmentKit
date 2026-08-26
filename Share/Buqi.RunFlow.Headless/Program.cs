using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Integration;
using Game.Hot.Buqi.Run.Settlement;
using Game.Hot.Buqi.Run.Training;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Battle;

namespace Buqi.RunFlow.Headless
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                RouteCandidatesAreFrozenAndExclusive();
                EventRuntimeAppliesConfiguredOutcomesOnceAndRestores();
                TrainingUsesTheProductionServiceAndIsIdempotent();
                TemporaryModifiersReachBattleAndExpireOnce();
                RewardPreviewAndClaimAreSeparateAndRecoverable();
                RewardCandidatesRequireApplicableTargets();
                SettlementWritesPreserveExtendedRunState();
                HeartTrialDefeatPersistsAsTerminalState();
                AuthoredCatalogMapsConfigToFormalRuntime();
                Console.WriteLine("[run-flow] all checks passed");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void RouteCandidatesAreFrozenAndExclusive()
        {
            BuqiRunState run = BuqiRunState.CreateInitial(17, "content-v1");
            var service = new BuqiRunRouteService();
            BuqiRunRouteState first = service.Open(run, 3);
            BuqiRunRouteState reopened = service.Open(run, 3);

            Equal(3, first.Nodes.Count, "operation route candidate count");
            Equal(first.RouteId, reopened.RouteId, "route id must be stable");
            for (int index = 0; index < first.Nodes.Count; index++)
            {
                Equal(first.Nodes[index].NodeId, reopened.Nodes[index].NodeId, "route node must be frozen");
                NotEmpty(first.Nodes[index].Benefit, "route benefit");
                NotEmpty(first.Nodes[index].Cost, "route cost");
                NotEmpty(first.Nodes[index].Condition, "route condition");
            }
            Equal(0, run.RngCursor, "opening a route must not consume core RNG");

            BuqiRunRouteResult selected = service.Select(first, first.Nodes[1].NodeId, "route-command-1");
            BuqiRunRouteResult replayed = service.Select(selected.State, first.Nodes[1].NodeId, "route-command-1");
            BuqiRunRouteResult conflicting = service.Select(selected.State, first.Nodes[0].NodeId, "route-command-2");
            True(selected.Success && !selected.Replayed, "first route selection");
            True(replayed.Success && replayed.Replayed, "same route command replay");
            True(!conflicting.Success, "route nodes must be mutually exclusive");
        }

        private static void EventRuntimeAppliesConfiguredOutcomesOnceAndRestores()
        {
            var items = new TestCatalog(Item("blade", "attack"), Item("charm", "support"));
            var option = new BuqiRunEventOptionDefinition { OptionId = "accept" };
            option.Actions.Add(Action("coins", BuqiRunEventActionKind.GrantCoins, amount: 3));
            option.Actions.Add(Action("life", BuqiRunEventActionKind.RestoreLife, amount: 1));
            option.Actions.Add(Action("item", BuqiRunEventActionKind.GrantItem, itemId: "charm"));
            option.Actions.Add(Action("xp", BuqiRunEventActionKind.AddExperience, amount: 4));
            option.Actions.Add(Action("refine", BuqiRunEventActionKind.ApplyRefinement, refinementId: "refine-a"));
            option.Actions.Add(Action("return", BuqiRunEventActionKind.ScheduleReturn, returnId: "event-return", scheduleId: "return-1"));
            var eventDefinition = new BuqiRunEventDefinition
            {
                EventId = "event-main",
                Options = { option, new BuqiRunEventOptionDefinition { OptionId = "wait" }, new BuqiRunEventOptionDefinition { OptionId = "leave" } },
            };
            var events = new TestCatalog(
                eventDefinition,
                new BuqiRunEventDefinition
                {
                    EventId = "event-return",
                    Eligibility = new BuqiRunEventEligibility { MinDay = 2, MaxDay = 9 },
                    Options =
                    {
                        new BuqiRunEventOptionDefinition { OptionId = "return-1" },
                        new BuqiRunEventOptionDefinition { OptionId = "return-2" },
                        new BuqiRunEventOptionDefinition { OptionId = "return-3" },
                    },
                });
            var adapter = new BuqiRunOperationFlowAdapter(events, items, new TestCatalog());
            BuqiRunEventRuntimeState source = adapter.CreateState(BuqiRunEconomySnapshot.CreateInitial(31, "content-v1"));
            source.Economy.Run.Lives = 2;
            AddOwnedItem(source.Economy, "owned-blade", "blade");

            BuqiRunOperationFlowResult opened = adapter.OpenEvent(source);
            True(opened.Success, "event open: " + opened.FailureReason);
            var request = new BuqiRunEventChoiceRequest
            {
                ResolutionId = "event-resolution-1",
                EventId = "event-main",
                OptionId = "accept",
                Targets = { new BuqiRunEventTargetSelection { ActionId = "refine", InstanceId = "owned-blade" } },
            };
            BuqiRunOperationFlowResult applied = adapter.ExecuteEvent(opened.State, request);
            BuqiRunOperationFlowResult replayed = adapter.ExecuteEvent(applied.State, request);

            True(applied.Success && !applied.Replayed, "event first execution: " + applied.FailureReason);
            True(replayed.Success && replayed.Replayed, "event replay");
            Equal(15, applied.State.Economy.Run.Coins, "event coins");
            Equal(3, applied.State.Economy.Run.Lives, "event life");
            Equal(4, applied.State.Experience, "event experience");
            Equal("refine-a", applied.State.Economy.Items["owned-blade"].RefinementId, "event refinement");
            Equal(1, applied.State.ScheduledReturns.Count, "scheduled return");
            Equal(applied.State.Economy.Run.Coins, replayed.State.Economy.Run.Coins, "replay coins");
            True(ContainsDefinition(applied.State.Economy, "charm"), "event item grant");

            BuqiRunEventSaveData save = adapter.CaptureSave(applied.State);
            True(adapter.TryRestore(applied.State.Economy, save, out BuqiRunEventRuntimeState restored, out string error), error);
            Equal(4, restored.Experience, "restored experience");
            Equal("event-return", restored.ScheduledReturns[0].EventId, "restored scheduled return");
            Equal(1, restored.AppliedResolutions.Count, "restored idempotency record");
        }

        private static void TrainingUsesTheProductionServiceAndIsIdempotent()
        {
            var items = new TestCatalog(Item("blade", "attack"));
            var training = new BuqiRunTrainingDefinition
            {
                TrainingId = "training-upgrade",
                Kind = BuqiRunTrainingKind.Upgrade,
                CoinCost = 2,
                RequiredBuildTag = "attack",
                QualitySteps = 1,
            };
            var adapter = new BuqiRunOperationFlowAdapter(new TestCatalog(), items, new TestCatalog(training));
            BuqiRunEventRuntimeState source = adapter.CreateState(BuqiRunEconomySnapshot.CreateInitial(41, "content-v1"));
            AddOwnedItem(source.Economy, "owned-blade", "blade");
            var request = new BuqiRunTrainingRequest { TrainingId = "training-upgrade", TargetInstanceId = "owned-blade" };

            BuqiRunOperationView composed = adapter.Compose(source);
            Equal("owned-blade", composed.TrainingOffers[0].CandidateInstanceIds[0], "training target projection");

            BuqiRunOperationFlowResult applied = adapter.ExecuteTraining(source, request);
            BuqiRunOperationFlowResult replayed = adapter.ExecuteTraining(applied.State, request);
            True(applied.Success && !applied.Replayed, "training first execution");
            True(replayed.Success && replayed.Replayed, "training replay");
            Equal(BuqiRunItemQuality.Improved, applied.State.Economy.Items["owned-blade"].Quality, "training quality");
            Equal(10, applied.State.Economy.Run.Coins, "training cost");
            Equal(applied.State.Economy.Run.Coins, replayed.State.Economy.Run.Coins, "training replay cost");
        }

        private static void RewardPreviewAndClaimAreSeparateAndRecoverable()
        {
            var catalog = new TestCatalog(Item("blade", "attack"), Item("charm", "support"));
            var settings = new BuqiRunRewardSettings
            {
                CandidateCount = 4,
                CoinAmount = 3,
                ExperienceAmount = 5,
                ExperiencePerLevel = 5,
                ItemDefinitionIds = { "charm" },
                RefinementIds = { "refine-a" },
            };
            var service = new BuqiRunRewardService(catalog, settings);
            var runtime = new BuqiRunEventRuntimeState
            {
                Economy = BuqiRunEconomySnapshot.CreateInitial(53, "content-v1"),
                Experience = 4,
            };
            AddOwnedItem(runtime.Economy, "owned-blade", "blade");
            BuqiRunRewardState opened = service.Open(runtime, "battle-1");
            Equal(4, opened.Candidates.Count, "configured reward count");
            BuqiRunRewardState previewed = service.Preview(opened, opened.Candidates[0].CandidateId);
            Equal(runtime.Economy.Run.Coins, 12, "preview coins unchanged");
            True(!previewed.Claimed, "preview does not claim");

            BuqiRunRewardCandidate xp = opened.Candidates.Find(value => value.Kind == BuqiRunRewardKind.Experience);
            NotNull(xp, "experience reward candidate");
            BuqiRunRewardState xpPreview = service.Preview(opened, xp.CandidateId);
            BuqiRunEventRuntimeState staleRuntime = runtime.Clone();
            staleRuntime.Economy.Run.Revision++;
            BuqiRunRewardResult staleClaim = service.Claim(staleRuntime, xpPreview, "stale-reward-command", "owned-blade");
            True(!staleClaim.Success, "stale reward revision must be rejected");
            BuqiRunRewardResult claimed = service.Claim(runtime, xpPreview, "reward-command-1", "owned-blade");
            BuqiRunRewardResult replayed = service.Claim(claimed.Runtime, claimed.Reward, "reward-command-1", "owned-blade");
            True(claimed.Success && claimed.LevelUp, "experience reward level-up");
            True(replayed.Success && replayed.Replayed, "reward replay");
            Equal(9, claimed.Runtime.Experience, "reward experience");
            Equal(claimed.Runtime.Experience, replayed.Runtime.Experience, "reward replay experience");

            BuqiRunRewardSaveData save = service.Capture(claimed.Reward);
            True(service.TryRestore(claimed.Runtime, save, out BuqiRunRewardState restored, out string error), error);
            True(restored.Claimed, "restored claimed reward");
            Equal(xp.CandidateId, restored.ClaimedCandidateId, "restored reward choice");
        }

        private static void TemporaryModifiersReachBattleAndExpireOnce()
        {
            var catalog = new TestCatalog(Item("blade", "attack"));
            BuqiRunEventRuntimeState runtime = BuqiRunEventRuntimeState.CreateInitial(47, "content-v1");
            runtime.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = "haste",
                BuildTag = "attack",
                Kind = BuqiRunModifierKind.CooldownPercent,
                Value = 1000,
                RemainingBattles = 1,
                DurationTicks = 30,
            });
            runtime.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = "buffer",
                Kind = BuqiRunModifierKind.StartingShield,
                Value = 6,
                RemainingBattles = 1,
            });
            runtime.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = "recovery",
                Kind = BuqiRunModifierKind.RecoveryPercent,
                Value = 4,
                RemainingBattles = 1,
            });
            var snapshot = new BuildSnapshot
            {
                InitialExecution = 100,
                Items = { new ItemInstance { InstanceId = "owned-blade", DefinitionId = "blade" } },
            };

            BuqiRunBattleModifierProjector.Apply(snapshot, runtime, catalog);
            Equal(6, snapshot.InitialBuffer, "temporary opening buffer");
            Equal(104, snapshot.InitialExecution, "temporary opening recovery");
            Equal(1, snapshot.Items[0].TemporaryModifiers.Count, "temporary cooldown projection");
            Equal(BuqiEffect.Haste, snapshot.Items[0].TemporaryModifiers[0].Effect, "temporary cooldown effect");
            Equal(1000, snapshot.Items[0].TemporaryModifiers[0].Bps, "temporary cooldown amount");
            Equal(30, snapshot.Items[0].TemporaryModifiers[0].RemainingTicks, "configured opening haste duration");

            BuqiRunEventRuntimeState afterBattle = BuqiRunEventTransitions.AfterBattle(runtime);
            Equal(0, afterBattle.TemporaryModifiers.Count, "temporary modifiers expire after battle");
            Equal(3, runtime.TemporaryModifiers.Count, "after battle keeps source immutable");
        }

        private static void RewardCandidatesRequireApplicableTargets()
        {
            var catalog = new TestCatalog(Item("blade", "attack"));
            var service = new BuqiRunRewardService(catalog, new BuqiRunRewardSettings
            {
                CandidateCount = 4,
                ItemDefinitionIds = { "blade" },
                RefinementIds = { "refine-a" },
            });
            BuqiRunEventRuntimeState runtime = BuqiRunEventRuntimeState.CreateInitial(59, "content-v1");
            AddOwnedItem(runtime.Economy, "owned-final", "blade");
            runtime.Economy.Items["owned-final"].Quality = BuqiRunItemQuality.Finalized;
            runtime.Economy.Items["owned-final"].RefinementId = "refine-a";
            for (int index = 0; index < runtime.Economy.Run.StorageInstanceIds.Count; index++)
                runtime.Economy.Run.StorageInstanceIds[index] = "occupied-" + index;

            for (int stage = 0; stage < 12; stage++)
            {
                BuqiRunRewardState opened = service.Open(runtime, "exhausted-" + stage);
                True(opened.Candidates.TrueForAll(candidate =>
                    candidate.Kind == BuqiRunRewardKind.Coins || candidate.Kind == BuqiRunRewardKind.Experience),
                    "unclaimable reward candidates must be replaced");
            }
        }

        private static void AuthoredCatalogMapsConfigToFormalRuntime()
        {
            var config = new BuqiConfigCatalog();
            config.Items.Add(new BuqiItemConfigRow
            {
                DefinitionId = "blade",
                BasePrice = 4,
                Tags = new List<string> { "attack" },
            });
            config.TrainingProjects.Add(new BuqiTrainingProjectConfigRow
            {
                ProjectId = "training-blade",
                EffectKind = "OpeningHaste",
                Cost = 2,
                RequiredTag = "attack",
                Amount = 1000,
                Duration = 30,
            });
            config.TrainingProjects.Add(new BuqiTrainingProjectConfigRow
            {
                ProjectId = "training-adjacent-haste",
                EffectKind = "AdjacentHaste",
                Cost = 2,
                RequiredTag = "attack",
                Amount = 30,
                Duration = 30,
            });
            config.TrainingProjects.Add(new BuqiTrainingProjectConfigRow
            {
                ProjectId = "training-upgrade-discount",
                EffectKind = "UpgradeDiscount",
                Cost = 2,
                RequiredTag = "attack",
                Amount = 1,
                Duration = 1,
            });
            config.Events.Add(new BuqiEventConfigRow { EventId = "event-authored", Weight = 1 });
            for (int index = 0; index < 3; index++)
            {
                config.EventOptions.Add(new BuqiEventOptionConfigRow
                {
                    EventId = "event-authored",
                    OptionId = "option-" + index,
                    Order = index,
                    Outcomes =
                    {
                        new BuqiEventOutcomeConfigRow { Kind = "Coins", Amount = index + 1, ReasonCode = "coins" },
                        new BuqiEventOutcomeConfigRow
                        {
                            Kind = "TemporaryHaste",
                            Amount = 1000,
                            DurationDays = 1,
                            ReasonCode = "haste",
                        },
                    },
                });
            }
            config.Events.Add(new BuqiEventConfigRow { EventId = "event-shop-only", Weight = 1 });
            for (int index = 0; index < 3; index++)
            {
                config.EventOptions.Add(new BuqiEventOptionConfigRow
                {
                    EventId = "event-shop-only",
                    OptionId = "shop-option-" + index,
                    Order = index,
                    Outcomes =
                    {
                        new BuqiEventOutcomeConfigRow
                        {
                            Kind = "FreeRefresh",
                            Amount = 1,
                            ReasonCode = "refresh",
                        },
                    },
                });
            }

            var authored = new BuqiRunAuthoredOperationCatalog(config);
            Equal(1, authored.Definitions.Count, "authored event count");
            Equal(1, authored.TrainingDefinitions.Count, "only exactly supported training effects are offered");
            True(authored.HasBuildTag("blade", "attack"), "authored build tag");
            True(authored.TryGet("event-authored", out BuqiRunEventDefinition definition), "authored event lookup");
            Equal(3, definition.Options.Count, "authored option count");
            Equal(BuqiRunModifierKind.CooldownPercent, definition.Options[0].Actions[1].ModifierKind, "event haste mapping");
            True(!authored.TryGet("event-shop-only", out BuqiRunEventDefinition _), "unconsumed shop event must be excluded");
            True(authored.TryGet("training-blade", out BuqiRunTrainingDefinition haste), "authored haste training");
            Equal(BuqiRunModifierKind.CooldownPercent, haste.ModifierKind, "training haste mapping");
            Equal(1, haste.ModifierDurationBattles, "tick duration must not become battle duration");
            Equal(30, haste.ModifierDurationTicks, "opening haste tick duration");
            True(!authored.TryGet("training-upgrade-discount", out BuqiRunTrainingDefinition _), "unconsumed discount training must be excluded");
            True(!authored.TryGet("training-adjacent-haste", out BuqiRunTrainingDefinition _), "conditional haste must not be approximated");
        }

        private static void SettlementWritesPreserveExtendedRunState()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(61, "content-v1");
            state.Phase = BuqiRunPhase.PveBattle;
            state.Period = BuqiRunPeriod.DuskPve;
            state.EncounterIndex = BuqiRunRules.OperationsBeforePve;
            var store = new MemoryRunStore();
            BuqiRunSaveData initial = BuqiRunSaveCodec.FromState(state, "economy", string.Empty, "battle");
            initial.HasOperationRuntime = true;
            initial.OperationRuntime = new BuqiRunEventSaveData
            {
                TemporaryModifiers =
                {
                    new BuqiRunTemporaryModifier
                    {
                        ModifierId = "settlement-modifier",
                        Kind = BuqiRunModifierKind.CooldownPercent,
                        RemainingBattles = 2,
                    },
                },
            };
            initial.HasRoute = true;
            initial.Route = new BuqiRunRouteState { RouteId = "route-preserved" };
            initial.HasReward = true;
            initial.Reward = new BuqiRunRewardSaveData();
            initial.IsPaused = true;
            initial.BattleResultVisible = true;
            initial.PeriodTransitionVisible = true;
            True(store.TryWrite(BuqiRunSaveCodec.ToJson(initial), out string writeError), writeError);

            var coordinator = new BuqiRunSettlementCoordinator(store);
            BuqiRunSettlementResult settled = coordinator.SettleBattle(
                state,
                "settlement-preserve",
                new BattleResult { Outcome = BattleOutcome.LeftWin, BattleLogHash = "hash-preserve" },
                Array.Empty<BattleEvent>(),
                "economy",
                string.Empty,
                "battle");
            True(settled.Success, "extended-state settlement: " + settled.FailureReason);
            True(store.TryRead(out string finalJson, out string readError), readError);
            True(BuqiRunSaveCodec.TryFromJson(finalJson, out BuqiRunSaveData saved, out string parseError), parseError);
            True(saved.HasOperationRuntime && saved.OperationRuntime != null, "operation runtime preserved");
            Equal(saved.Revision, saved.OperationRuntime.Revision, "operation runtime revision synchronized");
            Equal(1, saved.OperationRuntime.TemporaryModifiers.Count, "battle modifier remains after its first battle");
            Equal(1, saved.OperationRuntime.TemporaryModifiers[0].RemainingBattles, "battle modifier consumed once in atomic final save");
            Equal("route-preserved", saved.Route?.RouteId, "route preserved");
            True(saved.HasReward && saved.Reward != null, "reward preserved");
            True(saved.IsPaused, "pause flag preserved");
            True(saved.BattleResultVisible, "battle result flag preserved");
            True(!saved.PeriodTransitionVisible, "battle result supersedes a stale period transition");

            int writesAfterSettlement = store.WriteCount;
            store.FailNextRead = true;
            BuqiRunSettlementResult unreadableReplay = coordinator.SettleBattle(
                state,
                "settlement-preserve",
                new BattleResult { Outcome = BattleOutcome.LeftWin, BattleLogHash = "hash-preserve" },
                Array.Empty<BattleEvent>(),
                "economy",
                string.Empty,
                "battle");
            True(!unreadableReplay.Success, "settlement must fail closed when persisted state cannot be read");
            Equal(writesAfterSettlement, store.WriteCount, "failed settlement pre-read must not write");

            BuqiRunSettlementResult replayed = coordinator.SettleBattle(
                state,
                "settlement-preserve",
                new BattleResult { Outcome = BattleOutcome.LeftWin, BattleLogHash = "hash-preserve" },
                Array.Empty<BattleEvent>(),
                "economy",
                string.Empty,
                "battle");
            True(replayed.Success && replayed.Replayed, "persisted settlement must replay from an old caller state");
            Equal(writesAfterSettlement, store.WriteCount, "settlement replay must not write again");
            True(store.TryRead(out string replayJson, out string replayReadError), replayReadError);
            True(BuqiRunSaveCodec.TryFromJson(replayJson, out BuqiRunSaveData replaySave, out string replayParseError), replayParseError);
            Equal(1, replaySave.OperationRuntime.TemporaryModifiers[0].RemainingBattles, "settlement replay must not consume the modifier twice");
        }

        private static void HeartTrialDefeatPersistsAsTerminalState()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(63, "content-v1");
            state.Day = 8;
            state.Period = BuqiRunPeriod.NightPvp;
            state.Phase = BuqiRunPhase.PvpBattle;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.LifePool = 0;
            state.InTribulationTrial = true;
            state.HeartTrialUsed = true;
            var store = new MemoryRunStore();
            True(
                store.TryWrite(BuqiRunSaveCodec.ToJson(BuqiRunSaveCodec.FromState(state)), out string initialWriteError),
                initialWriteError);
            var coordinator = new BuqiRunSettlementCoordinator(store);

            BuqiRunSettlementResult settled = coordinator.SettleBattle(
                state,
                "heart-trial-defeat",
                new BattleResult { Outcome = BattleOutcome.RightWin, BattleLogHash = "heart-trial-defeat-hash" },
                Array.Empty<BattleEvent>(),
                "economy-heart-trial",
                "encounter-heart-trial",
                "battle-heart-trial");

            True(settled.Success, "heart-trial defeat settlement: " + settled.FailureReason);
            Equal(BuqiRunPhase.RunTerminal, settled.State.Phase, "heart-trial defeat phase");
            Equal(BuqiRunOutcome.Defeat, settled.State.Outcome, "heart-trial defeat outcome");
            True(!settled.State.InTribulationTrial, "terminal defeat must clear the heart-trial flag");
            True(store.TryRead(out string json, out string readError), readError);
            True(BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData saved, out string parseError), parseError);
            True(saved.PendingSettlement == null, "heart-trial defeat must clear pending settlement");
        }

        private static BuqiRunEventActionDefinition Action(
            string id,
            BuqiRunEventActionKind kind,
            int amount = 0,
            string itemId = "",
            string refinementId = "",
            string returnId = "",
            string scheduleId = "")
        {
            return new BuqiRunEventActionDefinition
            {
                ActionId = id,
                Kind = kind,
                Amount = amount,
                ItemDefinitionId = itemId,
                RefinementId = refinementId,
                ReturnEventId = returnId,
                ScheduleId = scheduleId,
                MinDayOffset = 1,
                MaxDayOffset = 2,
                WeightBonus = 5,
            };
        }

        private static BuqiRunItemDefinition Item(string id, string tag)
        {
            return new BuqiRunItemDefinition { DefinitionId = id, Size = 1, BuyPrice = 2, SellPrice = 1 };
        }

        private static void AddOwnedItem(BuqiRunEconomySnapshot economy, string instanceId, string definitionId)
        {
            economy.Items.Add(instanceId, new BuqiRunItemInstance { InstanceId = instanceId, DefinitionId = definitionId });
            economy.Run.StorageInstanceIds[0] = instanceId;
        }

        private static bool ContainsDefinition(BuqiRunEconomySnapshot economy, string definitionId)
        {
            foreach (BuqiRunItemInstance item in economy.Items.Values)
            {
                if (string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }

        private static void NotEmpty(string value, string message)
        {
            True(!string.IsNullOrWhiteSpace(value), message);
        }

        private static void NotNull(object value, string message)
        {
            True(value != null, message);
        }

        private sealed class TestCatalog : IBuqiRunEventDefinitionCatalog,
            IBuqiRunEventItemCatalog,
            IBuqiRunTrainingDefinitionCatalog
        {
            private readonly Dictionary<string, BuqiRunEventDefinition> m_Events = new Dictionary<string, BuqiRunEventDefinition>();
            private readonly Dictionary<string, BuqiRunItemDefinition> m_Items = new Dictionary<string, BuqiRunItemDefinition>();
            private readonly Dictionary<string, HashSet<string>> m_Tags = new Dictionary<string, HashSet<string>>();
            private readonly Dictionary<string, BuqiRunTrainingDefinition> m_Training = new Dictionary<string, BuqiRunTrainingDefinition>();

            public TestCatalog(params object[] definitions)
            {
                foreach (object definition in definitions)
                {
                    if (definition is BuqiRunEventDefinition eventDefinition)
                        m_Events.Add(eventDefinition.EventId, eventDefinition);
                    else if (definition is BuqiRunItemDefinition itemDefinition)
                    {
                        m_Items.Add(itemDefinition.DefinitionId, itemDefinition);
                        m_Tags.Add(itemDefinition.DefinitionId, new HashSet<string> { itemDefinition.DefinitionId == "blade" ? "attack" : "support" });
                    }
                    else if (definition is BuqiRunTrainingDefinition trainingDefinition)
                        m_Training.Add(trainingDefinition.TrainingId, trainingDefinition);
                }
            }

            public IReadOnlyList<BuqiRunEventDefinition> Definitions => new List<BuqiRunEventDefinition>(m_Events.Values);
            public IReadOnlyList<string> DefinitionIds => new List<string>(m_Items.Keys);
            public IReadOnlyList<BuqiRunTrainingDefinition> TrainingDefinitions => new List<BuqiRunTrainingDefinition>(m_Training.Values);
            public bool TryGet(string eventId, out BuqiRunEventDefinition definition) => m_Events.TryGetValue(eventId, out definition);
            public bool TryGet(string definitionId, out BuqiRunItemDefinition definition) => m_Items.TryGetValue(definitionId, out definition);
            public bool TryGet(string trainingId, out BuqiRunTrainingDefinition definition) => m_Training.TryGetValue(trainingId, out definition);
            public bool HasBuildTag(string definitionId, string buildTag) => m_Tags.TryGetValue(definitionId, out HashSet<string> tags) && tags.Contains(buildTag);
        }

        private sealed class MemoryRunStore : IBuqiRunStore
        {
            private string m_Json = string.Empty;
            public int WriteCount { get; private set; }
            public bool FailNextRead { get; set; }

            public bool TryRead(out string json, out string error)
            {
                if (FailNextRead)
                {
                    FailNextRead = false;
                    json = string.Empty;
                    error = "Transient read failure.";
                    return false;
                }
                json = m_Json;
                error = string.IsNullOrEmpty(m_Json) ? "Save does not exist." : string.Empty;
                return !string.IsNullOrEmpty(m_Json);
            }

            public bool TryWrite(string json, out string error)
            {
                m_Json = json;
                WriteCount++;
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                m_Json = string.Empty;
                error = string.Empty;
                return true;
            }
        }
    }
}
