using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Training;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunOperationFlowAdapterTests
    {
        [Test]
        public void OpenEvent_FreezesThreeChoicesAndProjectsConfiguredTrainingWithoutReroll()
        {
            BuqiRunEventOptionDefinition commit = Option("commit");
            commit.CoinCost = 2;
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.random",
                Kind = BuqiRunEventActionKind.GrantRandomItem,
                BuildTag = "attack",
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.upgrade",
                Kind = BuqiRunEventActionKind.UpgradeItem,
                BuildTag = "attack",
                QualitySteps = 1,
            });
            BuqiRunEventDefinition lesson = Event(
                "event.attack.lesson",
                new BuqiRunEventEligibility
                {
                    MinDay = 2,
                    MaxDay = 2,
                    Periods = BuqiRunPeriodMask.Morning,
                    RequiredFlags = { "lesson.open" },
                },
                commit,
                Option("observe"),
                Option("leave"));
            TestEventCatalog events = new TestEventCatalog(lesson);
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            TestTrainingCatalog training = new TestTrainingCatalog(
                Training("training.attack", BuqiRunTrainingKind.Upgrade, 3, "attack"),
                new BuqiRunTrainingDefinition
                {
                    TrainingId = "training.invalid-counter",
                    Kind = BuqiRunTrainingKind.Experience,
                    RewardCounterAmount = 1,
                },
                new BuqiRunTrainingDefinition
                {
                    TrainingId = "training.locked",
                    Kind = BuqiRunTrainingKind.Experience,
                    CoinCost = 1,
                    ExperienceReward = 1,
                    Eligibility = new BuqiRunEventEligibility
                    {
                        MinDay = 2,
                        MaxDay = 2,
                        Periods = BuqiRunPeriodMask.Morning,
                        RequiredFlags = { "missing.flag" },
                    },
                });
            BuqiRunEventRuntimeState source = State(2, BuqiRunPeriod.MorningOperation);
            source.Flags.Add("lesson.open");
            source.Economy.Run.Coins = 10;
            AddItem(source, "owned-blade", "blade.a");
            int sourceCursor = source.Economy.Run.RngCursor;
            var adapter = new BuqiRunOperationFlowAdapter(events, items, training);

            BuqiRunOperationView operationChoice = adapter.Compose(source);
            BuqiRunOperationFlowResult first = adapter.OpenEvent(source);
            BuqiRunOperationFlowResult reopened = adapter.OpenEvent(first.State);
            BuqiRunEventSaveData pendingSave = adapter.CaptureSave(first.State);
            bool restoredPending = adapter.TryRestore(
                first.State.Economy,
                pendingSave,
                out BuqiRunEventRuntimeState restoredPendingState,
                out string restorePendingError);
            BuqiRunOperationView restoredPendingView = adapter.Compose(restoredPendingState);
            BuqiRunOperationFlowResult blockedTraining = adapter.ExecuteTraining(
                first.State,
                new BuqiRunTrainingRequest
                {
                    ResolutionId = "resolution.blocked.training",
                    TrainingId = "training.attack",
                    TargetInstanceId = "owned-blade",
                });

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(first.Created, Is.True);
            Assert.That(first.View.Consumed, Is.False);
            Assert.That(first.View.Event.EventId, Is.EqualTo("event.attack.lesson"));
            Assert.That(first.View.Event.OptionIds, Is.EqualTo(new[] { "commit", "observe", "leave" }).AsCollection);
            Assert.That(first.View.Event.Options, Has.Count.EqualTo(3));
            Assert.That(first.View.Event.Options[0].OptionId, Is.EqualTo("commit"));
            Assert.That(first.View.Event.Options[0].CoinCost, Is.EqualTo(2));
            Assert.That(first.View.Event.Options[0].Affordable, Is.True);
            Assert.That(first.View.Event.Options[0].Targets, Has.Count.EqualTo(1));
            Assert.That(first.View.Event.Options[0].Targets[0].ActionId, Is.EqualTo("commit.upgrade"));
            Assert.That(first.View.Event.Options[0].Targets[0].CandidateInstanceIds,
                Is.EqualTo(new[] { "owned-blade" }).AsCollection);
            Assert.That(first.View.Event.FrozenResults, Has.Count.EqualTo(1));
            Assert.That(first.View.Event.FrozenResults[0].ActionId, Is.EqualTo("commit.random"));
            Assert.That(first.View.Flags, Is.EqualTo(new[] { "lesson.open" }).AsCollection);
            Assert.That(first.View.TrainingOffers, Is.Empty);
            Assert.That(operationChoice.TrainingOffers, Has.Count.EqualTo(3));
            Assert.That(operationChoice.TrainingOffers[0].TrainingId, Is.EqualTo("training.attack"));
            Assert.That(operationChoice.TrainingOffers[0].Eligible, Is.True);
            Assert.That(operationChoice.TrainingOffers[0].Affordable, Is.True);
            Assert.That(operationChoice.TrainingOffers[0].HasEligibleTarget, Is.True);
            Assert.That(operationChoice.TrainingOffers[0].Available, Is.True);
            Assert.That(operationChoice.TrainingOffers[1].TrainingId, Is.EqualTo("training.invalid-counter"));
            Assert.That(operationChoice.TrainingOffers[1].Eligible, Is.True);
            Assert.That(operationChoice.TrainingOffers[1].Available, Is.False);
            Assert.That(operationChoice.TrainingOffers[2].TrainingId, Is.EqualTo("training.locked"));
            Assert.That(operationChoice.TrainingOffers[2].Eligible, Is.False);
            Assert.That(operationChoice.TrainingOffers[2].Available, Is.False);
            Assert.That(source.Economy.Run.RngCursor, Is.EqualTo(sourceCursor));
            Assert.That(restoredPending, Is.True, restorePendingError);
            Assert.That(restoredPendingView.Event.FrozenResults[0].Value,
                Is.EqualTo(first.View.Event.FrozenResults[0].Value));

            Assert.That(reopened.Success, Is.True, reopened.FailureReason);
            Assert.That(reopened.Created, Is.False);
            Assert.That(reopened.View.Event.OptionIds, Is.EqualTo(first.View.Event.OptionIds).AsCollection);
            Assert.That(reopened.State.Economy.Run.RngCursor, Is.EqualTo(first.State.Economy.Run.RngCursor));
            Assert.That(blockedTraining.Success, Is.False);
            Assert.That(blockedTraining.State.PendingEvent.EventId, Is.EqualTo("event.attack.lesson"));
            Assert.That(blockedTraining.State.Economy.Run.Coins, Is.EqualTo(10));
            Assert.That(blockedTraining.State.Economy.Items["owned-blade"].Quality, Is.EqualTo(BuqiRunItemQuality.Common));
        }

        [Test]
        public void SynchronizeEconomy_PreservesPersistentStateAndRejectsChangingAFrozenPeriod()
        {
            TestEventCatalog events = new TestEventCatalog();
            TestItemCatalog items = new TestItemCatalog();
            var adapter = new BuqiRunOperationFlowAdapter(events, items, new TestTrainingCatalog());
            BuqiRunEventRuntimeState source = State(1, BuqiRunPeriod.MorningOperation);
            source.Flags.Add("route.attack");
            source.Counters.Add(new BuqiRunEventCounter { CounterId = "insight", Value = 2 });
            source.ScheduledReturns.Add(new BuqiRunScheduledReturn
            {
                ScheduleId = "attack.return",
                EventId = "event.attack.return",
                EarliestDay = 2,
                LatestDay = 3,
                WeightBonus = 10,
            });
            BuqiRunEconomySnapshot canonical = source.Economy.Clone();
            canonical.Run.Coins = 37;
            canonical.Run.Revision++;

            BuqiRunOperationFlowResult synchronized = adapter.SynchronizeEconomy(
                source,
                source.Economy,
                canonical);

            Assert.That(synchronized.Success, Is.True, synchronized.FailureReason);
            Assert.That(synchronized.State.Economy.Run.Coins, Is.EqualTo(37));
            Assert.That(synchronized.State.Flags, Does.Contain("route.attack"));
            Assert.That(synchronized.State.Counters[0].Value, Is.EqualTo(2));
            Assert.That(synchronized.State.ScheduledReturns[0].ScheduleId, Is.EqualTo("attack.return"));
            Assert.That(source.Economy.Run.Coins, Is.Not.EqualTo(37));
            Assert.That(synchronized.State.ScheduledReturns[0], Is.Not.SameAs(source.ScheduledReturns[0]));

            source.PendingEvent = new BuqiRunPendingEvent
            {
                EventId = "event.frozen",
                Day = 1,
                Period = BuqiRunPeriod.MorningOperation,
                OptionIds = { "one", "two", "three" },
            };
            canonical.Run.Period = BuqiRunPeriod.NoonOperation;
            BuqiRunOperationFlowResult rejected = adapter.SynchronizeEconomy(
                source,
                source.Economy,
                canonical);

            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.State.PendingEvent.EventId, Is.EqualTo("event.frozen"));
            Assert.That(rejected.State.Economy.Run.Period, Is.EqualTo(BuqiRunPeriod.MorningOperation));

            BuqiRunEventRuntimeState invalidPending = source.Clone();
            invalidPending.PendingEvent.Day = 2;
            Assert.Throws<System.ArgumentException>(() => adapter.Compose(invalidPending));

            BuqiRunEventRuntimeState returnState = synchronized.State.Clone();
            returnState.ScheduledReturns[0].LatestDay = 3;
            BuqiRunEconomySnapshot dayFour = returnState.Economy.Clone();
            dayFour.Run.Day = 4;
            dayFour.Run.Period = BuqiRunPeriod.MorningOperation;
            dayFour.Run.Revision++;
            BuqiRunOperationFlowResult cleaned = adapter.SynchronizeEconomy(
                returnState,
                returnState.Economy,
                dayFour);
            Assert.That(cleaned.Success, Is.True, cleaned.FailureReason);
            Assert.That(cleaned.View.ScheduledReturns, Is.Empty);
        }

        [Test]
        public void ResolveEvent_PersistsFlagAndSurfacesScheduledReturnOnTheNextDay()
        {
            BuqiRunEventOptionDefinition accept = Option("accept");
            accept.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "accept.flag",
                Kind = BuqiRunEventActionKind.SetFlag,
                FlagId = "route.attack",
            });
            accept.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "accept.return",
                Kind = BuqiRunEventActionKind.ScheduleReturn,
                ScheduleId = "route.attack.return",
                ReturnEventId = "event.attack.return",
                MinDayOffset = 1,
                MaxDayOffset = 1,
                WeightBonus = 50,
            });
            BuqiRunEventDefinition start = Event(
                "event.attack.start",
                new BuqiRunEventEligibility { MinDay = 1, MaxDay = 1 },
                accept,
                Option("observe"),
                Option("leave"));
            BuqiRunEventDefinition followUp = Event(
                "event.attack.return",
                new BuqiRunEventEligibility
                {
                    MinDay = 2,
                    MaxDay = 2,
                    RequiredFlags = { "route.attack" },
                },
                Option("claim"),
                Option("temper"),
                Option("release"));
            TestEventCatalog events = new TestEventCatalog(start, followUp);
            TestItemCatalog items = new TestItemCatalog();
            var adapter = new BuqiRunOperationFlowAdapter(events, items, new TestTrainingCatalog());
            BuqiRunEventRuntimeState source = State(1, BuqiRunPeriod.MorningOperation);

            BuqiRunOperationFlowResult opened = adapter.OpenEvent(source);
            BuqiRunOperationFlowResult resolved = adapter.ExecuteEvent(
                opened.State,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = "resolution.attack.start",
                    EventId = "event.attack.start",
                    OptionId = "accept",
                });
            BuqiRunOperationFlowResult duplicateOpen = adapter.OpenEvent(resolved.State);
            BuqiRunEconomySnapshot nextDay = resolved.State.Economy.Clone();
            nextDay.Run.Day = 2;
            nextDay.Run.Period = BuqiRunPeriod.MorningOperation;
            nextDay.Run.Revision++;
            BuqiRunOperationFlowResult synchronized = adapter.SynchronizeEconomy(
                resolved.State,
                resolved.State.Economy,
                nextDay);
            BuqiRunOperationFlowResult revisited = adapter.OpenEvent(synchronized.State);

            Assert.That(resolved.Success, Is.True, resolved.FailureReason);
            Assert.That(resolved.View.Consumed, Is.True);
            Assert.That(resolved.State.AppliedResolutions[0].ResolutionId, Is.EqualTo(opened.View.OperationId));
            Assert.That(resolved.View.Flags, Does.Contain("route.attack"));
            Assert.That(resolved.View.ScheduledReturns, Has.Count.EqualTo(1));
            Assert.That(duplicateOpen.Success, Is.False);
            Assert.That(revisited.Success, Is.True, revisited.FailureReason);
            Assert.That(revisited.View.Event.EventId, Is.EqualTo("event.attack.return"));
            Assert.That(revisited.View.Event.TriggeredScheduleId, Is.EqualTo("route.attack.return"));
            Assert.That(revisited.View.Event.IsScheduledReturn, Is.True);
            Assert.That(revisited.View.Event.OptionIds, Is.EqualTo(new[] { "claim", "temper", "release" }).AsCollection);
            Assert.That(revisited.View.Flags, Does.Contain("route.attack"));
        }

        [Test]
        public void ExecuteTrainingAndSaveRestore_KeepConfiguredResultAtomicAndIdempotent()
        {
            TestEventCatalog events = new TestEventCatalog();
            TestItemCatalog items = new TestItemCatalog();
            TestTrainingCatalog training = new TestTrainingCatalog(new BuqiRunTrainingDefinition
            {
                TrainingId = "training.insight",
                Kind = BuqiRunTrainingKind.Experience,
                CoinCost = 3,
                ExperienceReward = 5,
            });
            var adapter = new BuqiRunOperationFlowAdapter(events, items, training);
            BuqiRunEventRuntimeState source = State(3, BuqiRunPeriod.NoonOperation);
            source.Economy.Run.Coins = 10;
            source.Flags.Add("route.restore");
            var request = new BuqiRunTrainingRequest
            {
                ResolutionId = "resolution.training.insight",
                TrainingId = "training.insight",
            };

            BuqiRunOperationFlowResult trained = adapter.ExecuteTraining(source, request);
            BuqiRunOperationFlowResult replayed = adapter.ExecuteTraining(
                trained.State,
                new BuqiRunTrainingRequest
                {
                    ResolutionId = "caller.supplied.different.id",
                    TrainingId = "training.insight",
                });
            BuqiRunEconomySnapshot staleReplacement = source.Economy.Clone();
            staleReplacement.Run.Coins = 50;
            staleReplacement.Run.Revision = trained.State.Economy.Run.Revision + 1;
            BuqiRunOperationFlowResult staleSync = adapter.SynchronizeEconomy(
                trained.State,
                source.Economy,
                staleReplacement);
            BuqiRunEventSaveData save = adapter.CaptureSave(trained.State);
            bool restored = adapter.TryRestore(
                trained.State.Economy,
                save,
                out BuqiRunEventRuntimeState restoredState,
                out string restoreError);
            BuqiRunOperationView restoredView = adapter.Compose(restoredState);

            Assert.That(trained.Success, Is.True, trained.FailureReason);
            Assert.That(trained.State.Economy.Run.Coins, Is.EqualTo(7));
            Assert.That(trained.State.Experience, Is.EqualTo(5));
            Assert.That(trained.View.Consumed, Is.True);
            Assert.That(trained.View.TrainingOffers, Is.Empty);
            Assert.That(source.Economy.Run.Coins, Is.EqualTo(10));
            Assert.That(source.Experience, Is.Zero);
            Assert.That(replayed.Success, Is.True, replayed.FailureReason);
            Assert.That(replayed.Replayed, Is.True);
            Assert.That(replayed.State.Economy.Run.Coins, Is.EqualTo(7));
            Assert.That(replayed.State.Experience, Is.EqualTo(5));
            Assert.That(staleSync.Success, Is.False);
            Assert.That(staleSync.State.Economy.Run.Coins, Is.EqualTo(7));
            Assert.That(staleSync.State.Experience, Is.EqualTo(5));
            Assert.That(restored, Is.True, restoreError);
            Assert.That(restoredState.Flags, Does.Contain("route.restore"));
            Assert.That(restoredState.AppliedResolutions, Has.Count.EqualTo(1));
            Assert.That(restoredView.Consumed, Is.True);
            Assert.That(restoredView.TrainingOffers, Is.Empty);
        }

        private static BuqiRunEventRuntimeState State(int day, BuqiRunPeriod period)
        {
            BuqiRunEventRuntimeState state = BuqiRunEventRuntimeState.CreateInitial(4901L, "content.operation.adapter");
            state.Economy.Run.Day = day;
            state.Economy.Run.Period = period;
            return state;
        }

        private static void AddItem(BuqiRunEventRuntimeState state, string instanceId, string definitionId)
        {
            state.Economy.Items.Add(instanceId, new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = BuqiRunItemQuality.Common,
            });
            state.Economy.Run.StorageInstanceIds[0] = instanceId;
        }

        private static BuqiRunEventDefinition Event(
            string eventId,
            BuqiRunEventEligibility eligibility,
            params BuqiRunEventOptionDefinition[] options)
        {
            var definition = new BuqiRunEventDefinition
            {
                EventId = eventId,
                BaseWeight = 10,
                Eligibility = eligibility,
            };
            definition.Options.AddRange(options);
            return definition;
        }

        private static BuqiRunEventOptionDefinition Option(string optionId)
        {
            return new BuqiRunEventOptionDefinition { OptionId = optionId };
        }

        private static BuqiRunTrainingDefinition Training(
            string trainingId,
            BuqiRunTrainingKind kind,
            int coinCost,
            string requiredBuildTag)
        {
            return new BuqiRunTrainingDefinition
            {
                TrainingId = trainingId,
                Kind = kind,
                CoinCost = coinCost,
                RequiredBuildTag = requiredBuildTag,
                QualitySteps = 1,
                Eligibility = new BuqiRunEventEligibility
                {
                    MinDay = 2,
                    MaxDay = 2,
                    Periods = BuqiRunPeriodMask.Morning,
                },
            };
        }

        private static TestItemDefinition Item(string definitionId, params string[] buildTags)
        {
            return new TestItemDefinition
            {
                Definition = new BuqiRunItemDefinition
                {
                    DefinitionId = definitionId,
                    Size = 1,
                },
                BuildTags = new List<string>(buildTags),
            };
        }

        private sealed class TestEventCatalog : IBuqiRunEventDefinitionCatalog
        {
            private readonly List<BuqiRunEventDefinition> m_Definitions = new List<BuqiRunEventDefinition>();

            public TestEventCatalog(params BuqiRunEventDefinition[] definitions)
            {
                m_Definitions.AddRange(definitions);
            }

            public IReadOnlyList<BuqiRunEventDefinition> Definitions => m_Definitions;

            public bool TryGet(string eventId, out BuqiRunEventDefinition definition)
            {
                definition = m_Definitions.Find(value => value.EventId == eventId);
                return definition != null;
            }
        }

        private sealed class TestTrainingCatalog : IBuqiRunTrainingDefinitionCatalog
        {
            private readonly List<BuqiRunTrainingDefinition> m_Definitions = new List<BuqiRunTrainingDefinition>();

            public TestTrainingCatalog(params BuqiRunTrainingDefinition[] definitions)
            {
                m_Definitions.AddRange(definitions);
            }

            public IReadOnlyList<BuqiRunTrainingDefinition> TrainingDefinitions => m_Definitions;

            public bool TryGet(string trainingId, out BuqiRunTrainingDefinition definition)
            {
                definition = m_Definitions.Find(value => value.TrainingId == trainingId);
                return definition != null;
            }
        }

        private sealed class TestItemCatalog : IBuqiRunEventItemCatalog
        {
            private readonly Dictionary<string, TestItemDefinition> m_Items =
                new Dictionary<string, TestItemDefinition>();

            public TestItemCatalog(params TestItemDefinition[] items)
            {
                foreach (TestItemDefinition item in items)
                    m_Items.Add(item.Definition.DefinitionId, item);
            }

            public IReadOnlyList<string> DefinitionIds => new List<string>(m_Items.Keys);

            public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
            {
                if (m_Items.TryGetValue(definitionId, out TestItemDefinition item))
                {
                    definition = item.Definition;
                    return true;
                }

                definition = null;
                return false;
            }

            public bool HasBuildTag(string definitionId, string buildTag)
            {
                return m_Items.TryGetValue(definitionId, out TestItemDefinition item) &&
                       item.BuildTags.Contains(buildTag);
            }
        }

        private sealed class TestItemDefinition
        {
            public BuqiRunItemDefinition Definition;
            public List<string> BuildTags = new List<string>();
        }
    }
}
