using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEventRuntimeTests
    {
        [Test]
        public void Select_FreezesEventOptionsAndRandomRewardsWithoutMutatingSource()
        {
            TestEventCatalog events = new TestEventCatalog(CreateEvent(
                "event.attack.lesson",
                new BuqiRunEventEligibility
                {
                    MinDay = 2,
                    MaxDay = 4,
                    Periods = BuqiRunPeriodMask.Morning,
                    RequiredFlags = { "lesson.open" },
                    RequiredBuildTags = { "attack" },
                },
                CreateRandomRewardOption("take", "attack"),
                CreateOption("wait"),
                CreateOption("leave")));
            TestItemCatalog items = new TestItemCatalog(
                Item("blade.a", "attack"),
                Item("blade.b", "attack"),
                Item("ward.a", "shield"));
            BuqiRunEventRuntimeState source = CreateState(day: 2, BuqiRunPeriod.MorningOperation);
            source.Flags.Add("lesson.open");
            AddOwnedItem(source, "owned-blade", "blade.a");
            int sourceCursor = source.Economy.Run.RngCursor;

            var selector = new BuqiRunEventSelector(events, items);
            BuqiRunEventSelectionResult first = selector.Select(source);
            BuqiRunEventSelectionResult replay = selector.Select(first.State);

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(first.Created, Is.True);
            Assert.That(first.Pending.EventId, Is.EqualTo("event.attack.lesson"));
            Assert.That(first.Pending.OptionIds, Is.EqualTo(new[] { "take", "wait", "leave" }).AsCollection);
            Assert.That(first.Pending.RandomResults, Has.Count.EqualTo(1));
            Assert.That(first.Pending.RandomResults[0].ActionId, Is.EqualTo("take.reward"));
            Assert.That(
                first.Pending.RandomResults[0].Value == "blade.a" ||
                first.Pending.RandomResults[0].Value == "blade.b",
                Is.True);
            Assert.That(source.Economy.Run.RngCursor, Is.EqualTo(sourceCursor));

            Assert.That(replay.Success, Is.True, replay.FailureReason);
            Assert.That(replay.Created, Is.False);
            Assert.That(replay.Pending.RandomResults[0].Value, Is.EqualTo(first.Pending.RandomResults[0].Value));
            Assert.That(replay.State.Economy.Run.RngCursor, Is.EqualTo(first.State.Economy.Run.RngCursor));
        }

        [Test]
        public void Execute_ValidationFailureRollsBackEveryEffect()
        {
            BuqiRunEventOptionDefinition costly = CreateOption("pay");
            costly.CoinCost = 99;
            costly.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "pay.flag",
                Kind = BuqiRunEventActionKind.SetFlag,
                FlagId = "should.not.commit",
            });
            TestEventCatalog events = new TestEventCatalog(CreateEvent(
                "event.costly",
                new BuqiRunEventEligibility(),
                costly,
                CreateOption("wait"),
                CreateOption("leave")));
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            BuqiRunEventRuntimeState source = CreateState(day: 1, BuqiRunPeriod.MorningOperation);
            source.Economy.Run.Coins = 5;
            BuqiRunEventSelectionResult selected = new BuqiRunEventSelector(events, items).Select(source);
            BuqiRunEventRuntimeState frozen = selected.State;

            BuqiRunEventExecutionResult result = new BuqiRunEventExecutor(events, items).Execute(
                frozen,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = "resolution-costly-1",
                    EventId = "event.costly",
                    OptionId = "pay",
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.State, Is.Not.SameAs(frozen));
            Assert.That(result.State.Economy.Run.Coins, Is.EqualTo(5));
            Assert.That(result.State.Flags, Does.Not.Contain("should.not.commit"));
            Assert.That(result.State.PendingEvent.EventId, Is.EqualTo("event.costly"));
            Assert.That(frozen.Economy.Run.Coins, Is.EqualTo(5));
            Assert.That(frozen.Flags, Does.Not.Contain("should.not.commit"));
        }

        [Test]
        public void Execute_AppliesCompositeEffectsAndResolutionIsIdempotent()
        {
            BuqiRunEventOptionDefinition commit = CreateOption("commit");
            commit.CoinCost = 7;
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.coins",
                Kind = BuqiRunEventActionKind.GrantCoins,
                Amount = 3,
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.grant",
                Kind = BuqiRunEventActionKind.GrantItem,
                ItemDefinitionId = "ward.a",
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.upgrade",
                Kind = BuqiRunEventActionKind.UpgradeItem,
                BuildTag = "attack",
                QualitySteps = 1,
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.sacrifice",
                Kind = BuqiRunEventActionKind.SacrificeItem,
                BuildTag = "recovery",
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.modifier",
                Kind = BuqiRunEventActionKind.AddTemporaryModifier,
                ModifierId = "attack.lesson",
                BuildTag = "attack",
                ModifierKind = BuqiRunModifierKind.DamagePercent,
                Amount = 15,
                DurationBattles = 2,
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.flag",
                Kind = BuqiRunEventActionKind.SetFlag,
                FlagId = "attack.lesson.finished",
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.counter",
                Kind = BuqiRunEventActionKind.AddCounter,
                CounterId = "attack.lesson.count",
                Amount = 2,
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.experience",
                Kind = BuqiRunEventActionKind.AddExperience,
                Amount = 4,
            });
            commit.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = "commit.return",
                Kind = BuqiRunEventActionKind.ScheduleReturn,
                ScheduleId = "attack.lesson.return",
                ReturnEventId = "event.attack.return",
                MinDayOffset = 1,
                MaxDayOffset = 2,
                WeightBonus = 50,
            });

            BuqiRunEventDefinition start = CreateEvent(
                "event.attack.start",
                new BuqiRunEventEligibility(),
                commit,
                CreateOption("wait"),
                CreateOption("leave"));
            BuqiRunEventDefinition followUp = CreateEvent(
                "event.attack.return",
                new BuqiRunEventEligibility { MinDay = 2, MaxDay = 9 },
                CreateOption("claim"),
                CreateOption("delay"),
                CreateOption("refuse"));
            TestEventCatalog events = new TestEventCatalog(start, followUp);
            TestItemCatalog items = new TestItemCatalog(
                Item("blade.a", "attack"),
                Item("herb.a", "recovery"),
                Item("ward.a", "shield"));
            BuqiRunEventRuntimeState source = CreateState(day: 1, BuqiRunPeriod.MorningOperation);
            source.Economy.Run.Coins = 20;
            AddOwnedItem(source, "blade-owned", "blade.a", 0);
            AddOwnedItem(source, "herb-owned", "herb.a", 1);
            BuqiRunEventSelectionResult selected = new BuqiRunEventSelector(events, items).Select(source);
            var request = new BuqiRunEventChoiceRequest
            {
                ResolutionId = "resolution-attack-start",
                EventId = "event.attack.start",
                OptionId = "commit",
                Targets =
                {
                    new BuqiRunEventTargetSelection
                    {
                        ActionId = "commit.upgrade",
                        InstanceId = "blade-owned",
                    },
                    new BuqiRunEventTargetSelection
                    {
                        ActionId = "commit.sacrifice",
                        InstanceId = "herb-owned",
                    },
                },
            };
            var executor = new BuqiRunEventExecutor(events, items);

            BuqiRunEventExecutionResult result = executor.Execute(selected.State, request);
            BuqiRunEventExecutionResult replay = executor.Execute(result.State, request);
            BuqiRunEventExecutionResult conflict = executor.Execute(
                result.State,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = request.ResolutionId,
                    EventId = "event.different",
                    OptionId = request.OptionId,
                });
            BuqiRunEventExecutionResult targetConflict = executor.Execute(
                result.State,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = request.ResolutionId,
                    EventId = request.EventId,
                    OptionId = request.OptionId,
                    Targets =
                    {
                        new BuqiRunEventTargetSelection
                        {
                            ActionId = "commit.upgrade",
                            InstanceId = "different-target",
                        },
                        new BuqiRunEventTargetSelection
                        {
                            ActionId = "commit.sacrifice",
                            InstanceId = "herb-owned",
                        },
                    },
                });

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.State.Economy.Run.Coins, Is.EqualTo(16));
            Assert.That(result.State.Economy.Items["blade-owned"].Quality, Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(result.State.Economy.Items.ContainsKey("herb-owned"), Is.False);
            Assert.That(ContainsDefinition(result.State, "ward.a"), Is.True);
            Assert.That(result.State.Flags, Does.Contain("attack.lesson.finished"));
            Assert.That(result.State.Counters[0].Value, Is.EqualTo(2));
            Assert.That(result.State.Experience, Is.EqualTo(4));
            Assert.That(result.State.TemporaryModifiers, Has.Count.EqualTo(1));
            Assert.That(result.State.TemporaryModifiers[0].RemainingBattles, Is.EqualTo(2));
            Assert.That(result.State.ScheduledReturns[0].EarliestDay, Is.EqualTo(2));
            Assert.That(result.State.ScheduledReturns[0].LatestDay, Is.EqualTo(3));
            Assert.That(result.State.PendingEvent.IsActive, Is.False);
            Assert.That(result.State.History, Has.Count.EqualTo(1));
            Assert.That(result.State.AppliedResolutions, Has.Count.EqualTo(1));
            Assert.That(selected.State.Economy.Run.Coins, Is.EqualTo(20));

            Assert.That(replay.Success, Is.True, replay.FailureReason);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(replay.State.Economy.Run.Coins, Is.EqualTo(16));
            Assert.That(replay.State.History, Has.Count.EqualTo(1));
            Assert.That(replay.State.ScheduledReturns, Has.Count.EqualTo(1));
            Assert.That(conflict.Success, Is.False);
            Assert.That(targetConflict.Success, Is.False);
        }

        [Test]
        public void AfterBattle_ConsumesTemporaryModifiersOnAClone()
        {
            BuqiRunEventRuntimeState source = CreateState(day: 3, BuqiRunPeriod.NoonOperation);
            source.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = "one-battle",
                RemainingBattles = 1,
            });
            source.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = "two-battles",
                RemainingBattles = 2,
            });

            BuqiRunEventRuntimeState result = BuqiRunEventTransitions.AfterBattle(source);

            Assert.That(result.TemporaryModifiers, Has.Count.EqualTo(1));
            Assert.That(result.TemporaryModifiers[0].ModifierId, Is.EqualTo("two-battles"));
            Assert.That(result.TemporaryModifiers[0].RemainingBattles, Is.EqualTo(1));
            Assert.That(source.TemporaryModifiers, Has.Count.EqualTo(2));
            Assert.That(source.TemporaryModifiers[0].RemainingBattles, Is.EqualTo(1));
        }

        [Test]
        public void Select_UsesConditionsUniquenessCooldownAndStableWeights()
        {
            BuqiRunEventDefinition weightedA = CreateEvent(
                "event.a",
                new BuqiRunEventEligibility
                {
                    MinDay = 2,
                    MaxDay = 2,
                    Periods = BuqiRunPeriodMask.Morning,
                    RequiredFlags = { "route.open" },
                    RequiredBuildTags = { "attack" },
                },
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            weightedA.BaseWeight = 2;
            BuqiRunEventDefinition weightedB = CreateEvent(
                "event.b",
                weightedA.Eligibility.Clone(),
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            weightedB.BaseWeight = 8;
            BuqiRunEventDefinition unique = CreateEvent(
                "event.unique",
                weightedA.Eligibility.Clone(),
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            unique.UniquePerRun = true;
            BuqiRunEventDefinition cooling = CreateEvent(
                "event.cooling",
                weightedA.Eligibility.Clone(),
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            cooling.CooldownDays = 2;
            BuqiRunEventDefinition wrongPeriod = CreateEvent(
                "event.noon",
                new BuqiRunEventEligibility { Periods = BuqiRunPeriodMask.Noon },
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            TestEventCatalog events = new TestEventCatalog(
                wrongPeriod, cooling, weightedB, unique, weightedA);
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            BuqiRunEventRuntimeState source = CreateState(day: 2, BuqiRunPeriod.MorningOperation);
            source.Flags.Add("route.open");
            AddOwnedItem(source, "blade-owned", "blade.a");
            source.History.Add(new BuqiRunEventHistoryEntry { EventId = "event.unique", Day = 1 });
            source.History.Add(new BuqiRunEventHistoryEntry { EventId = "event.cooling", Day = 1 });
            int expectedCursor = source.Economy.Run.RngCursor;
            int roll = BuqiRunRandom.Next(source.Economy.Run.RunSeed, ref expectedCursor, 10);
            string expectedEventId = roll < 2 ? "event.a" : "event.b";

            BuqiRunEventSelectionResult result = new BuqiRunEventSelector(events, items).Select(source);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.Pending.EventId, Is.EqualTo(expectedEventId));
            Assert.That(result.State.Economy.Run.RngCursor, Is.EqualTo(expectedCursor));
        }

        [Test]
        public void Select_RejectsWholeEventWhenOneOfThreeOptionsIsIneligible()
        {
            BuqiRunEventOptionDefinition hidden = CreateRandomRewardOption("hidden", "attack");
            hidden.Eligibility.RequiredFlags.Add("missing.option.flag");
            BuqiRunEventDefinition rejected = CreateEvent(
                "event.rejected",
                new BuqiRunEventEligibility(),
                CreateOption("first"),
                hidden,
                CreateOption("third"));
            rejected.BaseWeight = 10000;
            BuqiRunEventDefinition valid = CreateEvent(
                "event.valid",
                new BuqiRunEventEligibility(),
                CreateOption("first"),
                CreateOption("second"),
                CreateOption("third"));
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            BuqiRunEventRuntimeState source = CreateState(day: 1, BuqiRunPeriod.MorningOperation);
            int expectedCursor = source.Economy.Run.RngCursor;
            BuqiRunRandom.Next(source.Economy.Run.RunSeed, ref expectedCursor, valid.BaseWeight);

            BuqiRunEventSelectionResult result = new BuqiRunEventSelector(
                new TestEventCatalog(rejected, valid),
                items).Select(source);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.Pending.EventId, Is.EqualTo("event.valid"));
            Assert.That(result.Pending.OptionIds, Has.Count.EqualTo(3));
            Assert.That(result.Pending.RandomResults, Is.Empty);
            Assert.That(result.State.Economy.Run.RngCursor, Is.EqualTo(expectedCursor));
        }

        [Test]
        public void Select_DueDayNineReturnIsFrozenAndConsumedOnlyOnSuccess()
        {
            BuqiRunEventDefinition returnEvent = CreateEvent(
                "event.day-nine.return",
                new BuqiRunEventEligibility { MinDay = 9, MaxDay = 9 },
                CreateOption("claim"), CreateOption("temper"), CreateOption("release"));
            BuqiRunEventDefinition distraction = CreateEvent(
                "event.day-nine.distraction",
                new BuqiRunEventEligibility { MinDay = 9, MaxDay = 9 },
                CreateOption("one"), CreateOption("two"), CreateOption("three"));
            distraction.BaseWeight = 10000;
            TestEventCatalog events = new TestEventCatalog(distraction, returnEvent);
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            BuqiRunEventRuntimeState source = CreateState(day: 9, BuqiRunPeriod.MorningOperation);
            source.ScheduledReturns.Add(new BuqiRunScheduledReturn
            {
                ScheduleId = "line.attack.day-nine",
                EventId = returnEvent.EventId,
                EarliestDay = 9,
                LatestDay = 9,
                WeightBonus = 100,
            });

            BuqiRunEventSelectionResult selected = new BuqiRunEventSelector(events, items).Select(source);
            BuqiRunEventExecutionResult failed = new BuqiRunEventExecutor(events, items).Execute(
                selected.State,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = "day-nine-failed",
                    EventId = "event.day-nine.return",
                    OptionId = "missing",
                });
            BuqiRunEventExecutionResult resolved = new BuqiRunEventExecutor(events, items).Execute(
                selected.State,
                new BuqiRunEventChoiceRequest
                {
                    ResolutionId = "day-nine-resolved",
                    EventId = "event.day-nine.return",
                    OptionId = "claim",
                });

            Assert.That(selected.Success, Is.True, selected.FailureReason);
            Assert.That(selected.Pending.EventId, Is.EqualTo("event.day-nine.return"));
            Assert.That(selected.Pending.TriggeredScheduleId, Is.EqualTo("line.attack.day-nine"));
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.State.ScheduledReturns, Has.Count.EqualTo(1));
            Assert.That(resolved.Success, Is.True, resolved.FailureReason);
            Assert.That(resolved.State.ScheduledReturns, Is.Empty);
        }

        private static BuqiRunEventRuntimeState CreateState(int day, BuqiRunPeriod period)
        {
            BuqiRunEventRuntimeState state = BuqiRunEventRuntimeState.CreateInitial(814L);
            state.Economy.Run.Day = day;
            state.Economy.Run.Period = period;
            return state;
        }

        private static void AddOwnedItem(
            BuqiRunEventRuntimeState state,
            string instanceId,
            string definitionId,
            int storageSlot = 0)
        {
            state.Economy.Items.Add(instanceId, new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = BuqiRunItemQuality.Common,
            });
            state.Economy.Run.StorageInstanceIds[storageSlot] = instanceId;
        }

        private static bool ContainsDefinition(BuqiRunEventRuntimeState state, string definitionId)
        {
            foreach (BuqiRunItemInstance item in state.Economy.Items.Values)
            {
                if (item.DefinitionId == definitionId)
                    return true;
            }

            return false;
        }

        private static BuqiRunEventDefinition CreateEvent(
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

        private static BuqiRunEventOptionDefinition CreateRandomRewardOption(string optionId, string buildTag)
        {
            BuqiRunEventOptionDefinition option = CreateOption(optionId);
            option.Actions.Add(new BuqiRunEventActionDefinition
            {
                ActionId = optionId + ".reward",
                Kind = BuqiRunEventActionKind.GrantRandomItem,
                BuildTag = buildTag,
            });
            return option;
        }

        private static BuqiRunEventOptionDefinition CreateOption(string optionId)
        {
            return new BuqiRunEventOptionDefinition { OptionId = optionId };
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
            private readonly Dictionary<string, BuqiRunEventDefinition> m_Definitions =
                new Dictionary<string, BuqiRunEventDefinition>();

            public TestEventCatalog(params BuqiRunEventDefinition[] definitions)
            {
                foreach (BuqiRunEventDefinition definition in definitions)
                {
                    m_Definitions.Add(definition.EventId, definition);
                }
            }

            public IReadOnlyList<BuqiRunEventDefinition> Definitions =>
                new List<BuqiRunEventDefinition>(m_Definitions.Values);

            public bool TryGet(string eventId, out BuqiRunEventDefinition definition)
            {
                return m_Definitions.TryGetValue(eventId, out definition);
            }
        }

        private sealed class TestItemCatalog : IBuqiRunEventItemCatalog
        {
            private readonly Dictionary<string, TestItemDefinition> m_Items =
                new Dictionary<string, TestItemDefinition>();

            public TestItemCatalog(params TestItemDefinition[] definitions)
            {
                foreach (TestItemDefinition definition in definitions)
                {
                    m_Items.Add(definition.Definition.DefinitionId, definition);
                }
            }

            public IReadOnlyList<string> DefinitionIds => new List<string>(m_Items.Keys);

            public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
            {
                if (m_Items.TryGetValue(definitionId, out TestItemDefinition value))
                {
                    definition = value.Definition;
                    return true;
                }

                definition = null;
                return false;
            }

            public bool HasBuildTag(string definitionId, string buildTag)
            {
                return m_Items.TryGetValue(definitionId, out TestItemDefinition value) &&
                       value.BuildTags.Contains(buildTag);
            }
        }

        private sealed class TestItemDefinition
        {
            public BuqiRunItemDefinition Definition = null;
            public List<string> BuildTags = new List<string>();
        }
    }
}
