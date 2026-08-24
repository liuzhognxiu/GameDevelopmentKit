using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Luban;
using NUnit.Framework;
using BattleConditionKind = Game.Hot.Buqi.Battle.BuqiConditionKind;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;

namespace Game.Hot.Buqi.Tests
{
    /// <summary>Step 3 Luban 最小配置链路的适配与校验测试。</summary>
    public sealed class BuqiConfigTests
    {
        [Test]
        public void DefinitionProvider_CopiesRowsIntoBattleDefinitions()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();

            var provider = new BuqiDefinitionProvider(catalog);

            Assert.That(provider.ContentVersion, Is.EqualTo("buqi-content-cv3"));
            Assert.That(provider.TryGet("W8-003", out BuqiItemDefinition urgent), Is.True);
            Assert.That(urgent.DefinitionId, Is.EqualTo("W8-003"));
            Assert.That(urgent.Size, Is.EqualTo((int)BattleSize.S));
            Assert.That(urgent.BaseCooldownTicks, Is.EqualTo(60));
            Assert.That(urgent.Effects.Count, Is.EqualTo(2));
            Assert.That(urgent.Effects[1].Effect, Is.EqualTo(BattleEffect.Haste));
            Assert.That(urgent.Effects[1].Target, Is.EqualTo(BattleTarget.LeftAdjacentItem));

            catalog.Items[0].Effects[0].Amount = 99;

            Assert.That(provider.TryGet("W8-003", out BuqiItemDefinition copied), Is.True);
            Assert.That(copied.Effects[0].Amount, Is.EqualTo(4));
        }

        [Test]
        public void ConfigValidator_AcceptsExpandedCatalog()
        {
            GeneratedBuqiTables tables = GeneratedBuqiTables.LoadFromProject();
            Assert.That(BuqiGeneratedConfigAdapter.TryReadFromTables(
                tables, out BuqiConfigCatalog catalog, out List<string> adapterErrors),
                Is.True,
                string.Join("\n", adapterErrors));

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void GeneratedExpandedBytes_AdaptValidateAndMatchFixtureEffects()
        {
            GeneratedBuqiTables tables = GeneratedBuqiTables.LoadFromProject();

            bool adapted = BuqiGeneratedConfigAdapter.TryReadFromTables(
                tables, out BuqiConfigCatalog generated, out List<string> adapterErrors);

            Assert.That(adapted, Is.True, string.Join("\n", adapterErrors));
            List<string> validationErrors = BuqiConfigValidator.Validate(generated);
            Assert.That(validationErrors, Is.Empty, string.Join("\n", validationErrors));
            Assert.That(generated.Items.Count, Is.EqualTo(300));
            Assert.That(generated.Refinements.Count, Is.EqualTo(6));
            Assert.That(generated.Echoes.Count, Is.EqualTo(16));
            Assert.That(generated.Merchants.Count, Is.EqualTo(8));
            Assert.That(generated.Trainers.Count, Is.EqualTo(4));
            Assert.That(generated.TrainingProjects.Count, Is.EqualTo(12));
            Assert.That(generated.Events.Count, Is.EqualTo(24));
            Assert.That(generated.EventOptions.Count, Is.EqualTo(72));

            BuqiConfigCatalog fixture = BuqiConfigTestData.CreateValidCatalog();
            AssertGlobalEquivalent(generated.Global, fixture.Global);
            foreach (BuqiItemConfigRow expected in fixture.Items)
            {
                BuqiItemConfigRow actual = FindItem(generated.Items, expected.DefinitionId);
                Assert.That(actual, Is.Not.Null, "缺少生成的装备：" + expected.DefinitionId);
                AssertItemEquivalent(actual, expected);
            }
            foreach (BuqiRefinementConfigRow expected in fixture.Refinements)
            {
                BuqiRefinementConfigRow actual = FindRefinement(generated.Refinements, expected.RefinementId);
                Assert.That(actual, Is.Not.Null, "缺少生成的淬炼：" + expected.RefinementId);
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), expected.RefinementId);
                Assert.That(actual.Summary, Is.EqualTo(expected.Summary), expected.RefinementId);
            }
            foreach (BuqiEchoConfigRow expected in fixture.Echoes)
            {
                BuqiEchoConfigRow actual = FindEcho(generated.Echoes, expected.EchoId);
                Assert.That(actual, Is.Not.Null, "缺少生成的道影：" + expected.EchoId);
                AssertEchoEquivalent(actual, expected);
            }
        }

        [Test]
        public void ConfigValidator_RejectsWrongCountsAndOutOfScopeItems()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            catalog.Items.RemoveAt(catalog.Items.Count - 1);
            catalog.Items.Add(BuqiConfigTestData.Item("W8-001", BattleSize.S, 2, 38, "fast",
                BuqiConfigTestData.Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 5, "W8-001-damage")));

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "已启用装备 W8-001"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "缺少已启用装备 W8-030"), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void ConfigValidator_RejectsInvalidEffectTargetAndTriggerFields()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            catalog.Items[0].Effects[0].Effect = BattleEffect.Buffer;
            catalog.Items[0].Effects[0].Target = BattleTarget.EnemyExecution;
            catalog.Items[1].Effects[0].Trigger = BattleTrigger.OnUseCountReached;
            catalog.Items[1].Effects[0].UseCountThreshold = 0;
            catalog.Items[2].Effects[0].Trigger = BattleTrigger.OnFirstConditionMet;
            catalog.Items[2].Effects[0].ConditionKind = BattleConditionKind.None;

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "Buffer 需要 Self 目标"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "OnUseCountReached 需要使用次数阈值"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "OnFirstConditionMet 需要条件类型"), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void ConfigValidator_RejectsEchoesWithUnknownReferencesAndInvalidBoard()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            BuqiEchoConfigRow echo = catalog.Echoes[0];
            echo.Snapshot.Items[0].DefinitionId = "W8-999";
            echo.Snapshot.Items[1].RefinementId = "A-99";
            echo.Snapshot.Items[1].AnchorSlot = 0;

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "未知的 definitionId W8-999"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "未知的 refinementId A-99"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "在棋位 0 与装备[0] 重叠"), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void ConfigValidator_RejectsEchoesWithMissingOrMismatchedBuildMetadata()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            catalog.Echoes[0].Build = string.Empty;
            catalog.Echoes[1].Build = "buffer";

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "构筑方向为空"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "构筑方向 buffer 与快照 archetype fast 不匹配"),
                Is.True, string.Join("\n", errors));
        }

        [Test]
        public void ConfigValidator_RejectsGlobalRulesThatDriftFromBattleSimulator()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            catalog.Global.InitialExecution++;
            catalog.Global.BufferCap++;
            catalog.Global.NoiseThreshold++;
            catalog.Global.NoiseIncidentDamage++;
            catalog.Global.StormStartTicks = -1;
            catalog.Global.StormBaseDamage = 0;
            catalog.Global.StormRampDamage = 0;

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "全局初始道基必须与战斗模拟器一致"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "全局护体上限必须与战斗模拟器一致"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "全局失衡阈值必须与战斗模拟器一致"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "全局失衡事故伤害必须与战斗模拟器一致"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "StormStartTicks"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "StormBaseDamage"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "StormRampDamage"), Is.True, string.Join("\n", errors));
        }

        private static bool Contains(List<string> errors, string fragment)
        {
            foreach (string error in errors)
            {
                if (error.Contains(fragment))
                    return true;
            }
            return false;
        }

        private static BuqiItemConfigRow FindItem(List<BuqiItemConfigRow> items, string definitionId)
        {
            foreach (BuqiItemConfigRow item in items)
            {
                if (item.DefinitionId == definitionId)
                    return item;
            }
            return null;
        }

        private static BuqiRefinementConfigRow FindRefinement(
            List<BuqiRefinementConfigRow> refinements,
            string refinementId)
        {
            foreach (BuqiRefinementConfigRow refinement in refinements)
            {
                if (refinement.RefinementId == refinementId)
                    return refinement;
            }
            return null;
        }

        private static BuqiEchoConfigRow FindEcho(List<BuqiEchoConfigRow> echoes, string echoId)
        {
            foreach (BuqiEchoConfigRow echo in echoes)
            {
                if (echo.EchoId == echoId)
                    return echo;
            }
            return null;
        }

        private static void AssertGlobalEquivalent(BuqiGlobalConfigRow actual, BuqiGlobalConfigRow expected)
        {
            Assert.That(actual.ContentVersion, Is.EqualTo(expected.ContentVersion));
            Assert.That(actual.InitialExecution, Is.EqualTo(expected.InitialExecution));
            Assert.That(actual.BufferCap, Is.EqualTo(expected.BufferCap));
            Assert.That(actual.NoiseThreshold, Is.EqualTo(expected.NoiseThreshold));
            Assert.That(actual.NoiseIncidentDamage, Is.EqualTo(expected.NoiseIncidentDamage));
            Assert.That(actual.BoardSlotCount, Is.EqualTo(expected.BoardSlotCount));
            Assert.That(actual.StormStartTicks, Is.EqualTo(expected.StormStartTicks));
            Assert.That(actual.StormBaseDamage, Is.EqualTo(expected.StormBaseDamage));
            Assert.That(actual.StormRampDamage, Is.EqualTo(expected.StormRampDamage));
            Assert.That(actual.MaxTickEvents, Is.EqualTo(expected.MaxTickEvents));
            Assert.That(actual.MaxItemEventsPerTick, Is.EqualTo(expected.MaxItemEventsPerTick));
        }

        private static void AssertItemEquivalent(BuqiItemConfigRow actual, BuqiItemConfigRow expected)
        {
            string where = expected.DefinitionId;
            Assert.That(actual.DisplayName, Is.Not.Empty, where);
            Assert.That(actual.Size, Is.EqualTo(expected.Size), where);
            Assert.That(actual.BasePrice, Is.EqualTo(expected.BasePrice), where);
            Assert.That(actual.BaseCooldownTicks, Is.EqualTo(expected.BaseCooldownTicks), where);
            Assert.That(actual.ArchetypeId, Is.EqualTo(expected.ArchetypeId), where);
            Assert.That(actual.Tags, Is.Not.Null, where);
            Assert.That(actual.Effects.Count, Is.EqualTo(expected.Effects.Count), where);
            for (int index = 0; index < expected.Effects.Count; index++)
                AssertEffectEquivalent(actual.Effects[index], expected.Effects[index], where + ".effect[" + index + "]");
        }

        private static void AssertEffectEquivalent(
            BuqiEffectConfigRow actual,
            BuqiEffectConfigRow expected,
            string where)
        {
            Assert.That(actual.Trigger, Is.EqualTo(expected.Trigger), where);
            Assert.That(actual.Effect, Is.EqualTo(expected.Effect), where);
            Assert.That(actual.Target, Is.EqualTo(expected.Target), where);
            Assert.That(actual.Amount, Is.EqualTo(expected.Amount), where);
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks), where);
            Assert.That(actual.ReasonCode, Is.EqualTo(expected.ReasonCode), where);
            Assert.That(actual.ConditionKind, Is.EqualTo(expected.ConditionKind), where);
            Assert.That(actual.ConditionThreshold, Is.EqualTo(expected.ConditionThreshold), where);
            Assert.That(actual.UseCountThreshold, Is.EqualTo(expected.UseCountThreshold), where);
            Assert.That(actual.CriticalChanceBps, Is.EqualTo(expected.CriticalChanceBps), where);
            Assert.That(actual.RepeatCount, Is.EqualTo(expected.RepeatCount), where);
            Assert.That(actual.RageThreshold, Is.EqualTo(expected.RageThreshold), where);
            Assert.That(actual.RageDurationTicks, Is.EqualTo(expected.RageDurationTicks), where);
            Assert.That(actual.RageCooldownReductionBps, Is.EqualTo(expected.RageCooldownReductionBps), where);
            Assert.That(actual.FlightDamageBonusBps, Is.EqualTo(expected.FlightDamageBonusBps), where);
            Assert.That(actual.FlightEndDamage, Is.EqualTo(expected.FlightEndDamage), where);
            Assert.That(actual.ResetCountOnReached, Is.EqualTo(expected.ResetCountOnReached), where);
        }

        private static void AssertEchoEquivalent(BuqiEchoConfigRow actual, BuqiEchoConfigRow expected)
        {
            string where = expected.EchoId;
            Assert.That(actual.Tier, Is.EqualTo(expected.Tier), where);
            Assert.That(actual.Build, Is.EqualTo(expected.Build), where);
            Assert.That(actual.Snapshot.SnapshotId, Is.EqualTo(expected.Snapshot.SnapshotId), where);
            Assert.That(actual.Snapshot.ArchetypeId, Is.EqualTo(expected.Snapshot.ArchetypeId), where);
            Assert.That(actual.Snapshot.InitialExecution, Is.EqualTo(expected.Snapshot.InitialExecution), where);
            Assert.That(actual.Snapshot.InitialBuffer, Is.EqualTo(expected.Snapshot.InitialBuffer), where);
            Assert.That(actual.Snapshot.InitialNoiseDebt, Is.EqualTo(expected.Snapshot.InitialNoiseDebt), where);
            Assert.That(actual.Snapshot.Items.Count, Is.EqualTo(expected.Snapshot.Items.Count), where);
            for (int index = 0; index < expected.Snapshot.Items.Count; index++)
            {
                BuqiItemInstanceConfigRow actualItem = actual.Snapshot.Items[index];
                BuqiItemInstanceConfigRow expectedItem = expected.Snapshot.Items[index];
                string itemWhere = where + ".item[" + index + "]";
                Assert.That(actualItem.InstanceId, Is.Not.Empty, itemWhere);
                Assert.That(actualItem.DefinitionId, Is.EqualTo(expectedItem.DefinitionId), itemWhere);
                Assert.That(actualItem.Quality, Is.EqualTo(expectedItem.Quality), itemWhere);
                Assert.That(actualItem.AnchorSlot, Is.EqualTo(expectedItem.AnchorSlot), itemWhere);
                Assert.That(actualItem.RefinementId, Is.EqualTo(expectedItem.RefinementId), itemWhere);
            }
        }

        private sealed class GeneratedBuqiTables
        {
            public DTBuqiGlobal DTBuqiGlobal { get; private set; }
            public DTBuqiItem DTBuqiItem { get; private set; }
            public DTBuqiRefinement DTBuqiRefinement { get; private set; }
            public DTBuqiEcho DTBuqiEcho { get; private set; }
            public DTBuqiMerchant DTBuqiMerchant { get; private set; }
            public DTBuqiTrainer DTBuqiTrainer { get; private set; }
            public DTBuqiTrainingProject DTBuqiTrainingProject { get; private set; }
            public DTBuqiEvent DTBuqiEvent { get; private set; }
            public DTBuqiEventOption DTBuqiEventOption { get; private set; }

            public static GeneratedBuqiTables LoadFromProject()
            {
                var tables = new GeneratedBuqiTables
                {
                    DTBuqiGlobal = new DTBuqiGlobal(() => LoadBytes("dtbuqiglobal")),
                    DTBuqiItem = new DTBuqiItem(() => LoadBytes("dtbuqiitem")),
                    DTBuqiRefinement = new DTBuqiRefinement(() => LoadBytes("dtbuqirefinement")),
                    DTBuqiEcho = new DTBuqiEcho(() => LoadBytes("dtbuqiecho")),
                    DTBuqiMerchant = new DTBuqiMerchant(() => LoadBytes("dtbuqimerchant")),
                    DTBuqiTrainer = new DTBuqiTrainer(() => LoadBytes("dtbuqitrainer")),
                    DTBuqiTrainingProject = new DTBuqiTrainingProject(() => LoadBytes("dtbuqitrainingproject")),
                    DTBuqiEvent = new DTBuqiEvent(() => LoadBytes("dtbuqievent")),
                    DTBuqiEventOption = new DTBuqiEventOption(() => LoadBytes("dtbuqieventoption")),
                };
                tables.DTBuqiGlobal.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiItem.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiRefinement.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEcho.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiMerchant.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiTrainer.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiTrainingProject.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEvent.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEventOption.LoadAsync().GetAwaiter().GetResult();
                return tables;
            }

            private static UniTask<ByteBuf> LoadBytes(string tableName)
            {
                string path = Path.Combine(
                    Directory.GetCurrentDirectory(), "Assets", "Res", "Hot", "Luban", tableName + ".bytes");
                Assert.That(File.Exists(path), Is.True, "generated table bytes missing: " + path);
                return UniTask.FromResult(new ByteBuf(File.ReadAllBytes(path)));
            }
        }

        private static class BuqiConfigTestData
        {
            public static BuqiConfigCatalog CreateValidCatalog()
            {
                var catalog = new BuqiConfigCatalog
                {
                    Global = new BuqiGlobalConfigRow
                    {
                        ContentVersion = "buqi-content-cv3",
                        InitialExecution = 100,
                        BufferCap = 60,
                        NoiseThreshold = 10,
                        NoiseIncidentDamage = 8,
                        BoardSlotCount = BuqiBoardValidator.BoardSlotCount,
                        StormStartTicks = 300,
                        StormBaseDamage = 1,
                        StormRampDamage = 1,
                        MaxTickEvents = 64,
                        MaxItemEventsPerTick = 4,
                    },
                };

                catalog.Items.Add(Item("W8-003", BattleSize.S, 2, 60, "fast",
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-003-attack"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.LeftAdjacentItem, 20000, "W8-003-haste", 30)));
                catalog.Items.Add(Item("W8-005", BattleSize.M, 4, 70, "fast",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-005-adjacent-charge"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 6, "W8-005-attack")));
                catalog.Items.Add(Item("W8-006", BattleSize.L, 6, 100, "fast",
                    Effect(BattleTrigger.OnBattleStart, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 20000, "W8-006-opening-haste", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 16, "W8-006-attack"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, 2, "W8-006-overload")));
                catalog.Items.Add(Item("W8-007", BattleSize.S, 2, 42, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 7, "W8-007-shield")));
                catalog.Items.Add(Item("W8-008", BattleSize.S, 2, 55, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-008-charge"),
                    ConditionEffect(BattleConditionKind.BufferLost, 0, BattleEffect.Damage, BattleTarget.EnemyExecution, 8, "W8-008-shield-break")));
                catalog.Items.Add(Item("W8-012", BattleSize.L, 6, 90, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 12, "W8-012-shield"),
                    ConditionEffect(BattleConditionKind.BufferLost, 0, BattleEffect.Damage, BattleTarget.EnemyExecution, 14, "W8-012-shield-break")));
                catalog.Items.Add(Item("W8-013", BattleSize.S, 2, 50, "chain",
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-013-attack"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.RightAdjacentItem, 1, "W8-013-pass-charge"),
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.RightAdjacentItem, 1, "W8-013-adjacent-pass")));
                catalog.Items.Add(Item("W8-014", BattleSize.S, 2, 60, "chain",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-014-adjacent-charge"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 3, "W8-014-attack")));
                catalog.Items.Add(Item("W8-015", BattleSize.M, 4, 65, "chain",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Haste, BattleTarget.Self, 20000, "W8-015-adjacent-haste", 30),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 7, "W8-015-attack")));
                catalog.Items.Add(Item("W8-016", BattleSize.S, 2, 45, "heal",
                    Effect(BattleTrigger.OnUse, BattleEffect.Heal, BattleTarget.Self, 8, "W8-016-heal")));
                catalog.Items.Add(Item("W8-017", BattleSize.M, 4, 70, "heal",
                    Effect(BattleTrigger.OnUse, BattleEffect.Regen, BattleTarget.Self, 4, "W8-017-regen", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 5, "W8-017-shield")));
                catalog.Items.Add(Item("W8-018", BattleSize.L, 6, 95, "heal",
                    Effect(BattleTrigger.OnBattleStart, BattleEffect.Regen, BattleTarget.Self, 3, "W8-018-opening-regen", 60),
                    Effect(BattleTrigger.OnUse, BattleEffect.Heal, BattleTarget.Self, 15, "W8-018-heal"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 20000, "W8-018-haste")));
                catalog.Items.Add(Item("W8-019", BattleSize.S, 2, 40, "poison",
                    Effect(BattleTrigger.OnUse, BattleEffect.Poison, BattleTarget.EnemyExecution, 3, "W8-019-poison", 40)));
                catalog.Items.Add(Item("W8-020", BattleSize.M, 4, 65, "poison",
                    Effect(BattleTrigger.OnUse, BattleEffect.Poison, BattleTarget.EnemyExecution, 5, "W8-020-poison", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Delay, BattleTarget.ShortestCooldownEnemyItem, 5000, "W8-020-slow")));
                catalog.Items.Add(Item("W8-021", BattleSize.L, 6, 90, "poison",
                    Effect(BattleTrigger.OnUse, BattleEffect.Poison, BattleTarget.EnemyExecution, 7, "W8-021-poison", 60),
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, 1, "W8-021-overload")));
                catalog.Items.Add(Item("W8-022", BattleSize.S, 2, 38, "burn",
                    Effect(BattleTrigger.OnUse, BattleEffect.Burn, BattleTarget.EnemyExecution, 3, "W8-022-burn")));
                catalog.Items.Add(Item("W8-023", BattleSize.M, 4, 60, "burn",
                    Effect(BattleTrigger.OnUse, BattleEffect.Burn, BattleTarget.EnemyExecution, 5, "W8-023-burn", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-023-attack")));
                catalog.Items.Add(Item("W8-024", BattleSize.L, 6, 95, "burn",
                    Effect(BattleTrigger.OnUse, BattleEffect.Burn, BattleTarget.EnemyExecution, 8, "W8-024-burn", 60),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 20000, "W8-024-haste")));
                catalog.Items.Add(Item("W8-025", BattleSize.S, 2, 50, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.ShortestCooldownEnemyItem, 8, "W8-025-freeze", 8)));
                catalog.Items.Add(Item("W8-026", BattleSize.M, 4, 75, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.LongestCooldownEnemyItem, 12, "W8-026-freeze", 12),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-026-attack")));
                catalog.Items.Add(Item("W8-027", BattleSize.L, 6, 110, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.ShortestCooldownEnemyItem, 16, "W8-027-freeze", 16),
                    Effect(BattleTrigger.OnUse, BattleEffect.Delay, BattleTarget.ShortestCooldownEnemyItem, 5000, "W8-027-slow", 40)));
                catalog.Items.Add(Item("W8-028", BattleSize.S, 2, 36, "overload",
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.Self, 2, "W8-028-charge"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, 2, "W8-028-overload")));
                catalog.Items.Add(Item("W8-029", BattleSize.S, 2, 55, "overload",
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, -4, "W8-029-vent"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 4, "W8-029-shield")));
                catalog.Items.Add(Item("W8-030", BattleSize.M, 4, 80, "overload",
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 9, "W8-030-shield"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.RightAdjacentItem, 2, "W8-030-pass-charge"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, -3, "W8-030-vent")));

                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-01", DisplayName = "快速改造", Summary = "冷却缩短 15%；每次主动使用增加 1 点过载。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-02", DisplayName = "强效改造", Summary = "冷却延长 20%；非开场效果提高 30%。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-03", DisplayName = "首次重复触发", Summary = "每场战斗首次主动使用时，以 50% 效果额外结算一次。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-04", DisplayName = "稳定改造", Summary = "免疫敌方减速，但不受友方加速。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-05", DisplayName = "低负载改造", Summary = "伤害与护盾降低 15%；本装备造成的过载减少 1 点。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-06", DisplayName = "高风险强化", Summary = "开场增加 3 点过载；伤害与护盾提高 35%。" });

                catalog.Echoes.Add(Echo("echo-fast-lesson", "fast", Instance("efl-deadline", "W8-006", BattleQuality.Fixed, 0, "A-06"), Instance("efl-board-a", "W8-005", BattleQuality.Fixed, 3, "A-06"), Instance("efl-board-b", "W8-005", BattleQuality.Improved, 5, "A-02"), Instance("efl-urgent", "W8-003", BattleQuality.Fixed, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-fast-early", "fast", Instance("efe-deadline", "W8-006", BattleQuality.Fixed, 0, "A-06"), Instance("efe-board-a", "W8-005", BattleQuality.Fixed, 3, "A-06"), Instance("efe-board-b", "W8-005", BattleQuality.Improved, 5, "A-02"), Instance("efe-urgent", "W8-003", BattleQuality.Fixed, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-buffer-lesson", "buffer", Instance("ebl-core-a", "W8-012", BattleQuality.Fixed, 0, "A-06"), Instance("ebl-core-b", "W8-012", BattleQuality.Fixed, 3, "A-06"), Instance("ebl-list", "W8-008", BattleQuality.Fixed, 6, "A-04"), Instance("ebl-shield", "W8-007", BattleQuality.Fixed, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-buffer-early", "buffer", Instance("ebe-core-a", "W8-012", BattleQuality.Fixed, 0, "A-06"), Instance("ebe-core-b", "W8-012", BattleQuality.Fixed, 3, "A-06"), Instance("ebe-list", "W8-008", BattleQuality.Fixed, 6, "A-04"), Instance("ebe-shield", "W8-007", BattleQuality.Fixed, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-chain-lesson", "chain", Instance("e5-hand", "W8-013", BattleQuality.Normal, 0), Instance("e5-sign", "W8-014", BattleQuality.Normal, 1, "A-03"), Instance("e5-node", "W8-015", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-chain-early", "chain", Instance("e6-hand", "W8-013", BattleQuality.Improved, 0), Instance("e6-sign", "W8-014", BattleQuality.Normal, 1), Instance("e6-node", "W8-015", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-heal-lesson", "heal", Instance("ehl-flag", "W8-018", BattleQuality.Normal, 0), Instance("ehl-spring-a", "W8-017", BattleQuality.Normal, 3), Instance("ehl-spring-b", "W8-017", BattleQuality.Normal, 5), Instance("ehl-pack", "W8-016", BattleQuality.Normal, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-heal-early", "heal", Instance("ehe-flag", "W8-018", BattleQuality.Normal, 0), Instance("ehe-spring-a", "W8-017", BattleQuality.Normal, 3), Instance("ehe-spring-b", "W8-017", BattleQuality.Normal, 5), Instance("ehe-pack", "W8-016", BattleQuality.Normal, 7, "A-01")));
                catalog.Echoes.Add(Echo("echo-poison-lesson", "poison", Instance("e9-needle", "W8-019", BattleQuality.Normal, 0), Instance("e9-bottle", "W8-020", BattleQuality.Normal, 1), Instance("e9-fog", "W8-021", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-poison-early", "poison", Instance("e10-needle", "W8-019", BattleQuality.Improved, 0), Instance("e10-bottle", "W8-020", BattleQuality.Normal, 1), Instance("e10-mirror", "W8-025", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-burn-lesson", "burn", Instance("e11-spark", "W8-022", BattleQuality.Normal, 0), Instance("e11-furnace", "W8-023", BattleQuality.Normal, 1), Instance("e11-array", "W8-024", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-burn-early", "burn", Instance("e12-spark", "W8-022", BattleQuality.Improved, 0), Instance("e12-furnace", "W8-023", BattleQuality.Normal, 1), Instance("e12-urgent", "W8-003", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-freeze-lesson", "freeze", Instance("e13-mirror", "W8-025", BattleQuality.Normal, 0), Instance("e13-lock", "W8-026", BattleQuality.Normal, 1), Instance("e13-tower", "W8-027", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-freeze-early", "freeze", Instance("e14-mirror", "W8-025", BattleQuality.Improved, 0), Instance("e14-lock", "W8-026", BattleQuality.Normal, 1), Instance("e14-shield", "W8-007", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-overload-lesson", "overload", Instance("e15-battery", "W8-028", BattleQuality.Normal, 0), Instance("e15-core", "W8-030", BattleQuality.Normal, 1), Instance("e15-vent", "W8-029", BattleQuality.Normal, 3), Instance("e15-board", "W8-005", BattleQuality.Normal, 4)));
                catalog.Echoes.Add(Echo("echo-overload-early", "overload", Instance("e16-battery", "W8-028", BattleQuality.Improved, 0), Instance("e16-vent", "W8-029", BattleQuality.Normal, 1), Instance("e16-core", "W8-030", BattleQuality.Normal, 2)));
                return catalog;
            }

            public static BuqiItemConfigRow Item(
                string id,
                BattleSize size,
                int price,
                int cooldownTicks,
                string archetypeId,
                params BuqiEffectConfigRow[] effects)
            {
                var item = new BuqiItemConfigRow
                {
                    DefinitionId = id,
                    DisplayName = id,
                    Size = size,
                    BasePrice = price,
                    BaseCooldownTicks = cooldownTicks,
                    ArchetypeId = archetypeId,
                };
                item.Effects.AddRange(effects);
                return item;
            }

            public static BuqiEffectConfigRow Effect(
                BattleTrigger trigger,
                BattleEffect effect,
                BattleTarget target,
                int amount,
                string reasonCode,
                int durationTicks = 30)
            {
                return new BuqiEffectConfigRow
                {
                    Trigger = trigger,
                    Effect = effect,
                    Target = target,
                    Amount = amount,
                    DurationTicks = durationTicks,
                    ReasonCode = reasonCode,
                    ResetCountOnReached = true,
                };
            }

            public static BuqiEffectConfigRow ConditionEffect(
                BattleConditionKind condition,
                int threshold,
                BattleEffect effect,
                BattleTarget target,
                int amount,
                string reasonCode)
            {
                BuqiEffectConfigRow spec = Effect(
                    BattleTrigger.OnFirstConditionMet,
                    effect,
                    target,
                    amount,
                    reasonCode);
                spec.ConditionKind = condition;
                spec.ConditionThreshold = threshold;
                return spec;
            }

            private static BuqiEchoConfigRow Echo(
                string id,
                string archetypeId,
                params BuqiItemInstanceConfigRow[] items)
            {
                var echo = new BuqiEchoConfigRow
                {
                    EchoId = id,
                    DisplayName = id,
                    Tier = id.EndsWith("-early") ? "early" : "lesson",
                    Build = archetypeId,
                    Snapshot = new BuqiBuildSnapshotConfigRow
                    {
                        SnapshotId = id,
                        ArchetypeId = archetypeId,
                        InitialExecution = 100,
                    },
                };
                echo.Snapshot.Items.AddRange(items);
                return echo;
            }

            private static BuqiItemInstanceConfigRow Instance(
                string instanceId,
                string definitionId,
                BattleQuality quality,
                int anchorSlot,
                string refinementId = "")
            {
                return new BuqiItemInstanceConfigRow
                {
                    InstanceId = instanceId,
                    DefinitionId = definitionId,
                    Quality = quality,
                    AnchorSlot = anchorSlot,
                    RefinementId = refinementId,
                };
            }
        }
    }
}
