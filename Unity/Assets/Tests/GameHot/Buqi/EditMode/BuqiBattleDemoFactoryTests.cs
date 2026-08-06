using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBattleDemoFactoryTests
    {
        [Test]
        public void Factory_IsAvailableInGameHotAssembly()
        {
            Type factoryType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.Demo.BuqiBattleDemoFactory");

            Assert.That(factoryType, Is.Not.Null);
        }

        [Test]
        public void Factory_CreatesSameReplayFromSameCatalog()
        {
            BuqiConfigCatalog catalog = CreateCatalog();

            Assert.That(TryCreate(catalog, out BattleReplayData first, out string firstError), Is.True, firstError);
            Assert.That(TryCreate(catalog, out BattleReplayData second, out string secondError), Is.True, secondError);
            Assert.That(first.Result.BattleLogHash, Is.EqualTo(second.Result.BattleLogHash));
            Assert.That(first.Title, Is.EqualTo(second.Title));
            Assert.That(first.LeftBuild.Items.Count, Is.GreaterThan(0));
            Assert.That(first.RightBuild.Items.Count, Is.GreaterThan(0));
            Assert.That(new BattleReplayController(first).Frame.Error, Is.Empty);
        }

        [Test]
        public void Factory_RejectsCatalogWithoutTwoEchoes()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            catalog.Echoes.RemoveAt(1);

            Assert.That(TryCreate(catalog, out BattleReplayData data, out string error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error, Does.Contain("两个"));
        }

        private static bool TryCreate(
            BuqiConfigCatalog catalog,
            out BattleReplayData data,
            out string error)
        {
            Type factoryType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.Demo.BuqiBattleDemoFactory");
            MethodInfo method = factoryType.GetMethod("TryCreate", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing BuqiBattleDemoFactory.TryCreate");
            object[] arguments = { catalog, null, null };
            bool result = (bool)method.Invoke(null, arguments);
            data = (BattleReplayData)arguments[1];
            error = (string)arguments[2];
            return result;
        }

        private static BuqiConfigCatalog CreateCatalog()
        {
            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            BattleRequest request = BuqiTestSuite.CreateVectors()[0].Request;
            var catalog = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = provider.ContentVersion,
                    InitialExecution = request.Left.InitialExecution,
                    BufferCap = BuqiBattleSimulator.BufferCap,
                    NoiseThreshold = BuqiBattleSimulator.NoiseThreshold,
                    NoiseIncidentDamage = BuqiBattleSimulator.NoiseAccidentDamage,
                    BoardSlotCount = 8,
                    NormalDurationTicks = BuqiBattleSimulator.NormalTickCount,
                    HardCapTicks = BuqiBattleSimulator.HardCapTick + 1,
                    OvertimeStartTicks = BuqiBattleSimulator.NormalTickCount,
                    MaxTickEvents = BuqiBattleSimulator.MaxEventsPerTick,
                    MaxItemEventsPerTick = BuqiBattleSimulator.MaxEventsPerItemPerTick,
                },
            };

            AddDefinitions(catalog, provider, request.Left);
            AddDefinitions(catalog, provider, request.Right);
            catalog.Echoes.Add(CreateEcho("echo-a", "对手快照甲", request.Left));
            catalog.Echoes.Add(CreateEcho("echo-b", "对手快照乙", request.Right));
            return catalog;
        }

        private static void AddDefinitions(
            BuqiConfigCatalog catalog,
            IItemDefinitionProvider provider,
            BuildSnapshot build)
        {
            foreach (ItemInstance instance in build.Items)
            {
                if (catalog.Items.Exists(row => row.DefinitionId == instance.DefinitionId))
                    continue;
                provider.TryGet(instance.DefinitionId, out BuqiItemDefinition definition);
                var row = new BuqiItemConfigRow
                {
                    DefinitionId = definition.DefinitionId,
                    DisplayName = definition.DefinitionId,
                    Size = (Game.Hot.Buqi.Battle.BuqiSize)definition.Size,
                    BaseCooldownTicks = definition.BaseCooldownTicks,
                    ArchetypeId = build.ArchetypeId,
                };
                foreach (BuqiEffectSpec effect in definition.Effects)
                {
                    row.Effects.Add(new BuqiEffectConfigRow
                    {
                        Trigger = effect.Trigger,
                        Effect = effect.Effect,
                        Target = effect.Target,
                        Amount = effect.Amount,
                        DurationTicks = effect.DurationTicks,
                        ReasonCode = effect.ReasonCode,
                        ConditionKind = effect.ConditionKind,
                        ConditionThreshold = effect.ConditionThreshold,
                        UseCountThreshold = effect.UseCountThreshold,
                        ChargeReadLimit = effect.ChargeReadLimit,
                        AmountPerCharge = effect.AmountPerCharge,
                        ChargeConsume = effect.ChargeConsume,
                        ResetCountOnReached = effect.ResetCountOnReached,
                    });
                }
                catalog.Items.Add(row);
            }
        }

        private static BuqiEchoConfigRow CreateEcho(string id, string name, BuildSnapshot build)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = build.SnapshotId,
                ArchetypeId = build.ArchetypeId,
                InitialExecution = build.InitialExecution,
                InitialBuffer = build.InitialBuffer,
                InitialNoiseDebt = build.InitialNoiseDebt,
            };
            foreach (ItemInstance item in build.Items)
            {
                snapshot.Items.Add(new BuqiItemInstanceConfigRow
                {
                    InstanceId = item.InstanceId,
                    DefinitionId = item.DefinitionId,
                    Quality = (Game.Hot.Buqi.Battle.BuqiQuality)item.Quality,
                    AnchorSlot = item.AnchorSlot,
                    RefinementId = item.AnnotationId,
                });
            }
            return new BuqiEchoConfigRow
            {
                EchoId = id,
                DisplayName = name,
                Tier = "1",
                Build = build.ArchetypeId,
                Snapshot = snapshot,
            };
        }
    }
}
