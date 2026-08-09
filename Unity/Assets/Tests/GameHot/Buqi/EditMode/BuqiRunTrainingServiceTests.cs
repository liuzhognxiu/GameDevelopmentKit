using System.Collections.Generic;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Training;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunTrainingServiceTests
    {
        [Test]
        public void Execute_UpgradePaysConfiguredResourcesAndIsIdempotent()
        {
            var training = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.upgrade",
                Kind = BuqiRunTrainingKind.Upgrade,
                CoinCost = 5,
                CounterCostId = "essence",
                CounterCost = 2,
                RequiredBuildTag = "attack",
                QualitySteps = 1,
            };
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            BuqiRunEventRuntimeState source = CreateState();
            source.Economy.Run.Coins = 20;
            source.Counters.Add(new BuqiRunEventCounter { CounterId = "essence", Value = 3 });
            AddOwnedItem(source, "blade-owned", "blade.a");
            var request = new BuqiRunTrainingRequest
            {
                ResolutionId = "training-resolution-1",
                TrainingId = training.TrainingId,
                TargetInstanceId = "blade-owned",
            };
            var service = new BuqiRunTrainingService(new TestTrainingCatalog(training), items);

            BuqiRunTrainingResult result = service.Execute(source, request);
            BuqiRunTrainingResult replay = service.Execute(result.State, request);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.State.Economy.Run.Coins, Is.EqualTo(15));
            Assert.That(result.State.Counters[0].Value, Is.EqualTo(1));
            Assert.That(result.State.Economy.Items["blade-owned"].Quality, Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(source.Economy.Run.Coins, Is.EqualTo(20));
            Assert.That(source.Counters[0].Value, Is.EqualTo(3));
            Assert.That(source.Economy.Items["blade-owned"].Quality, Is.EqualTo(BuqiRunItemQuality.Common));

            Assert.That(replay.Success, Is.True, replay.FailureReason);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(replay.State.Economy.Run.Coins, Is.EqualTo(15));
            Assert.That(replay.State.AppliedResolutions, Has.Count.EqualTo(1));
        }

        [Test]
        public void Execute_DirectedStrengtheningAppliesRefinementAndBattleModifier()
        {
            var training = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.shield.focus",
                Kind = BuqiRunTrainingKind.DirectedStrengthening,
                CoinCost = 4,
                RequiredBuildTag = "shield",
                RefinementId = "training.tempered",
                ModifierId = "training.shield.lesson",
                ModifierKind = BuqiRunModifierKind.ShieldPercent,
                ModifierValue = 12,
                ModifierDurationBattles = 2,
            };
            TestItemCatalog items = new TestItemCatalog(Item("ward.a", "shield"));
            BuqiRunEventRuntimeState source = CreateState();
            source.Economy.Run.Coins = 10;
            AddOwnedItem(source, "ward-owned", "ward.a");

            BuqiRunTrainingResult result = new BuqiRunTrainingService(
                new TestTrainingCatalog(training),
                items).Execute(source, new BuqiRunTrainingRequest
                {
                    ResolutionId = "training-resolution-shield",
                    TrainingId = training.TrainingId,
                    TargetInstanceId = "ward-owned",
                });

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.State.Economy.Items["ward-owned"].RefinementId, Is.EqualTo("training.tempered"));
            Assert.That(result.State.TemporaryModifiers, Has.Count.EqualTo(1));
            Assert.That(result.State.TemporaryModifiers[0].Kind, Is.EqualTo(BuqiRunModifierKind.ShieldPercent));
            Assert.That(result.State.TemporaryModifiers[0].RemainingBattles, Is.EqualTo(2));
        }

        [Test]
        public void Execute_EconomyAndExperienceTrainingUseConfiguredRewards()
        {
            var economy = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.economy",
                Kind = BuqiRunTrainingKind.Economy,
                CoinCost = 3,
                CoinReward = 8,
                RewardCounterId = "commerce.lesson",
                RewardCounterAmount = 1,
            };
            var experience = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.experience",
                Kind = BuqiRunTrainingKind.Experience,
                CoinCost = 2,
                ExperienceReward = 6,
            };
            TestItemCatalog items = new TestItemCatalog(Item("blade.a", "attack"));
            var service = new BuqiRunTrainingService(new TestTrainingCatalog(economy, experience), items);
            BuqiRunEventRuntimeState source = CreateState();
            source.Economy.Run.Coins = 10;

            BuqiRunTrainingResult economyResult = service.Execute(source, new BuqiRunTrainingRequest
            {
                ResolutionId = "training-economy-1",
                TrainingId = economy.TrainingId,
            });
            BuqiRunTrainingResult experienceResult = service.Execute(economyResult.State, new BuqiRunTrainingRequest
            {
                ResolutionId = "training-experience-1",
                TrainingId = experience.TrainingId,
            });

            Assert.That(economyResult.Success, Is.True, economyResult.FailureReason);
            Assert.That(economyResult.State.Economy.Run.Coins, Is.EqualTo(15));
            Assert.That(economyResult.State.Counters[0].CounterId, Is.EqualTo("commerce.lesson"));
            Assert.That(experienceResult.Success, Is.True, experienceResult.FailureReason);
            Assert.That(experienceResult.State.Economy.Run.Coins, Is.EqualTo(13));
            Assert.That(experienceResult.State.Experience, Is.EqualTo(6));
        }

        [Test]
        public void Execute_InvalidTargetRollsBackAllTrainingCosts()
        {
            var training = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.attack.focus",
                Kind = BuqiRunTrainingKind.DirectedStrengthening,
                CoinCost = 7,
                CounterCostId = "essence",
                CounterCost = 1,
                RequiredBuildTag = "attack",
                RefinementId = "training.focused",
            };
            TestItemCatalog items = new TestItemCatalog(Item("ward.a", "shield"));
            BuqiRunEventRuntimeState source = CreateState();
            source.Economy.Run.Coins = 10;
            source.Counters.Add(new BuqiRunEventCounter { CounterId = "essence", Value = 2 });
            AddOwnedItem(source, "ward-owned", "ward.a");

            BuqiRunTrainingResult result = new BuqiRunTrainingService(
                new TestTrainingCatalog(training),
                items).Execute(source, new BuqiRunTrainingRequest
                {
                    ResolutionId = "training-invalid-target",
                    TrainingId = training.TrainingId,
                    TargetInstanceId = "ward-owned",
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.State.Economy.Run.Coins, Is.EqualTo(10));
            Assert.That(result.State.Counters[0].Value, Is.EqualTo(2));
            Assert.That(result.State.Economy.Items["ward-owned"].RefinementId, Is.Empty);
            Assert.That(source.Economy.Run.Coins, Is.EqualTo(10));
        }

        [Test]
        public void Execute_DirectedStrengtheningRejectsExistingRefinementAndRollsBack()
        {
            var training = new BuqiRunTrainingDefinition
            {
                TrainingId = "training.no-overwrite",
                Kind = BuqiRunTrainingKind.DirectedStrengthening,
                CoinCost = 4,
                RequiredBuildTag = "shield",
                RefinementId = "training.new",
            };
            TestItemCatalog items = new TestItemCatalog(Item("ward.a", "shield"));
            BuqiRunEventRuntimeState source = CreateState();
            source.Economy.Run.Coins = 10;
            AddOwnedItem(source, "ward-owned", "ward.a");
            source.Economy.Items["ward-owned"].RefinementId = "existing.refinement";

            BuqiRunTrainingResult result = new BuqiRunTrainingService(
                new TestTrainingCatalog(training),
                items).Execute(source, new BuqiRunTrainingRequest
                {
                    ResolutionId = "training-no-overwrite",
                    TrainingId = training.TrainingId,
                    TargetInstanceId = "ward-owned",
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.State.Economy.Run.Coins, Is.EqualTo(10));
            Assert.That(result.State.Economy.Items["ward-owned"].RefinementId, Is.EqualTo("existing.refinement"));
            Assert.That(result.State.AppliedResolutions, Is.Empty);
        }

        private static BuqiRunEventRuntimeState CreateState()
        {
            return BuqiRunEventRuntimeState.CreateInitial(54321L);
        }

        private static void AddOwnedItem(BuqiRunEventRuntimeState state, string instanceId, string definitionId)
        {
            state.Economy.Items.Add(instanceId, new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = BuqiRunItemQuality.Common,
            });
            state.Economy.Run.StorageInstanceIds[0] = instanceId;
        }

        private static TestItemDefinition Item(string definitionId, params string[] buildTags)
        {
            return new TestItemDefinition
            {
                Definition = new BuqiRunItemDefinition { DefinitionId = definitionId, Size = 1 },
                BuildTags = new List<string>(buildTags),
            };
        }

        private sealed class TestTrainingCatalog : IBuqiRunTrainingCatalog
        {
            private readonly Dictionary<string, BuqiRunTrainingDefinition> m_Definitions =
                new Dictionary<string, BuqiRunTrainingDefinition>();

            public TestTrainingCatalog(params BuqiRunTrainingDefinition[] definitions)
            {
                foreach (BuqiRunTrainingDefinition definition in definitions)
                {
                    m_Definitions.Add(definition.TrainingId, definition);
                }
            }

            public bool TryGet(string trainingId, out BuqiRunTrainingDefinition definition)
            {
                return m_Definitions.TryGetValue(trainingId, out definition);
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
