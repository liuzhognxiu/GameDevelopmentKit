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

            Assert.That(provider.ContentVersion, Is.EqualTo("buqi-effects-cv1"));
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
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();

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
            Assert.That(generated.Items.Count, Is.EqualTo(24));
            Assert.That(generated.Refinements.Count, Is.EqualTo(6));
            Assert.That(generated.Echoes.Count, Is.EqualTo(16));

            BuqiConfigCatalog fixture = BuqiConfigTestData.CreateValidCatalog();
            AssertGlobalEquivalent(generated.Global, fixture.Global);
            foreach (BuqiItemConfigRow expected in fixture.Items)
            {
                BuqiItemConfigRow actual = FindItem(generated.Items, expected.DefinitionId);
                Assert.That(actual, Is.Not.Null, "generated item missing: " + expected.DefinitionId);
                AssertItemEquivalent(actual, expected);
            }
            foreach (BuqiRefinementConfigRow expected in fixture.Refinements)
            {
                BuqiRefinementConfigRow actual = FindRefinement(generated.Refinements, expected.RefinementId);
                Assert.That(actual, Is.Not.Null, "generated refinement missing: " + expected.RefinementId);
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), expected.RefinementId);
                Assert.That(actual.Summary, Is.EqualTo(expected.Summary), expected.RefinementId);
            }
            foreach (BuqiEchoConfigRow expected in fixture.Echoes)
            {
                BuqiEchoConfigRow actual = FindEcho(generated.Echoes, expected.EchoId);
                Assert.That(actual, Is.Not.Null, "generated echo missing: " + expected.EchoId);
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

            Assert.That(Contains(errors, "enabled item W8-001"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "missing enabled item W8-030"), Is.True, string.Join("\n", errors));
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

            Assert.That(Contains(errors, "Buffer requires Self target"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "OnUseCountReached requires use count threshold"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "OnFirstConditionMet requires condition kind"), Is.True, string.Join("\n", errors));
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

            Assert.That(Contains(errors, "unknown definitionId W8-999"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "unknown refinementId A-99"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "overlap at slot"), Is.True, string.Join("\n", errors));
        }

        [Test]
        public void ConfigValidator_RejectsEchoesWithMissingOrMismatchedBuildMetadata()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();
            catalog.Echoes[0].Build = string.Empty;
            catalog.Echoes[1].Build = "buffer";

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "build is empty"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "build buffer does not match snapshot archetype fast"),
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
            catalog.Global.NormalDurationTicks++;
            catalog.Global.OvertimeStartTicks++;
            catalog.Global.HardCapTicks++;

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(Contains(errors, "initial execution must match battle simulator"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "buffer cap must match battle simulator"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "noise threshold must match battle simulator"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "noise incident damage must match battle simulator"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "normal duration must match battle simulator"), Is.True, string.Join("\n", errors));
            Assert.That(Contains(errors, "hard cap must match battle simulator"), Is.True, string.Join("\n", errors));
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
            Assert.That(actual.NormalDurationTicks, Is.EqualTo(expected.NormalDurationTicks));
            Assert.That(actual.HardCapTicks, Is.EqualTo(expected.HardCapTicks));
            Assert.That(actual.OvertimeStartTicks, Is.EqualTo(expected.OvertimeStartTicks));
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
            Assert.That(actual.ChargeReadLimit, Is.EqualTo(expected.ChargeReadLimit), where);
            Assert.That(actual.AmountPerCharge, Is.EqualTo(expected.AmountPerCharge), where);
            Assert.That(actual.ChargeConsume, Is.EqualTo(expected.ChargeConsume), where);
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

            public static GeneratedBuqiTables LoadFromProject()
            {
                var tables = new GeneratedBuqiTables
                {
                    DTBuqiGlobal = new DTBuqiGlobal(() => LoadBytes("dtbuqiglobal")),
                    DTBuqiItem = new DTBuqiItem(() => LoadBytes("dtbuqiitem")),
                    DTBuqiRefinement = new DTBuqiRefinement(() => LoadBytes("dtbuqirefinement")),
                    DTBuqiEcho = new DTBuqiEcho(() => LoadBytes("dtbuqiecho")),
                };
                tables.DTBuqiGlobal.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiItem.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiRefinement.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEcho.LoadAsync().GetAwaiter().GetResult();
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
                        ContentVersion = "buqi-effects-cv1",
                        InitialExecution = 100,
                        BufferCap = 60,
                        NoiseThreshold = 10,
                        NoiseIncidentDamage = 8,
                        BoardSlotCount = 8,
                        NormalDurationTicks = 450,
                        HardCapTicks = 600,
                        OvertimeStartTicks = 450,
                        MaxTickEvents = 64,
                        MaxItemEventsPerTick = 4,
                    },
                };

                catalog.Items.Add(Item("W8-003", BattleSize.S, 2, 60, "fast",
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-003-attack"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.LeftAdjacentItem, 2000, "W8-003-haste", 30)));
                catalog.Items.Add(Item("W8-005", BattleSize.M, 4, 70, "fast",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-005-adjacent-charge"),
                    ChargedEffect(6, 2, 3, true, "W8-005-attack")));
                catalog.Items.Add(Item("W8-006", BattleSize.L, 6, 100, "fast",
                    Effect(BattleTrigger.OnBattleStart, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 1500, "W8-006-opening-haste", 50),
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
                    ChargedEffect(3, 3, 2, true, "W8-014-attack")));
                catalog.Items.Add(Item("W8-015", BattleSize.M, 4, 65, "chain",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Haste, BattleTarget.Self, 2000, "W8-015-adjacent-haste", 30),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 7, "W8-015-attack")));
                catalog.Items.Add(Item("W8-016", BattleSize.S, 2, 45, "heal",
                    Effect(BattleTrigger.OnUse, BattleEffect.Heal, BattleTarget.Self, 8, "W8-016-heal")));
                catalog.Items.Add(Item("W8-017", BattleSize.M, 4, 70, "heal",
                    Effect(BattleTrigger.OnUse, BattleEffect.Regen, BattleTarget.Self, 4, "W8-017-regen", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 5, "W8-017-shield")));
                catalog.Items.Add(Item("W8-018", BattleSize.L, 6, 95, "heal",
                    Effect(BattleTrigger.OnBattleStart, BattleEffect.Regen, BattleTarget.Self, 3, "W8-018-opening-regen", 60),
                    Effect(BattleTrigger.OnUse, BattleEffect.Heal, BattleTarget.Self, 15, "W8-018-heal"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 1200, "W8-018-haste")));
                catalog.Items.Add(Item("W8-019", BattleSize.S, 2, 40, "poison",
                    Effect(BattleTrigger.OnUse, BattleEffect.Poison, BattleTarget.EnemyExecution, 3, "W8-019-poison", 40)));
                catalog.Items.Add(Item("W8-020", BattleSize.M, 4, 65, "poison",
                    Effect(BattleTrigger.OnUse, BattleEffect.Poison, BattleTarget.EnemyExecution, 5, "W8-020-poison", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Delay, BattleTarget.ShortestCooldownEnemyItem, 1000, "W8-020-slow")));
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
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 1000, "W8-024-haste")));
                catalog.Items.Add(Item("W8-025", BattleSize.S, 2, 50, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.ShortestCooldownEnemyItem, 8, "W8-025-freeze", 8)));
                catalog.Items.Add(Item("W8-026", BattleSize.M, 4, 75, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.LongestCooldownEnemyItem, 12, "W8-026-freeze", 12),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-026-attack")));
                catalog.Items.Add(Item("W8-027", BattleSize.L, 6, 110, "freeze",
                    Effect(BattleTrigger.OnUse, BattleEffect.Freeze, BattleTarget.ShortestCooldownEnemyItem, 16, "W8-027-freeze", 16),
                    Effect(BattleTrigger.OnUse, BattleEffect.Delay, BattleTarget.ShortestCooldownEnemyItem, 1000, "W8-027-slow", 40)));
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

                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-01", DisplayName = "加急", Summary = "cooldown -15%; each active use creates 1 overload." });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-02", DisplayName = "激化", Summary = "cooldown +20%; non-opening effects +30%." });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-03", DisplayName = "复写", Summary = "first active use repeats at 50% effect once per battle." });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-04", DisplayName = "可靠", Summary = "immune to enemy slow and ignores friendly haste." });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-05", DisplayName = "稳流", Summary = "attack/shield -15%; overload gained by this item -1." });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-06", DisplayName = "超载", Summary = "battle start gains 3 overload; attack/shield +35%." });

                catalog.Echoes.Add(Echo("echo-fast-lesson", "fast", Instance("e1-deadline", "W8-006", BattleQuality.Normal, 0), Instance("e1-board", "W8-005", BattleQuality.Normal, 3), Instance("e1-urgent", "W8-003", BattleQuality.Normal, 5, "A-01")));
                catalog.Echoes.Add(Echo("echo-fast-early", "fast", Instance("e2-board", "W8-005", BattleQuality.Improved, 0), Instance("e2-urgent", "W8-003", BattleQuality.Normal, 2), Instance("e2-buffer", "W8-007", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-buffer-lesson", "buffer", Instance("e3-buffer", "W8-007", BattleQuality.Normal, 0), Instance("e3-risk", "W8-008", BattleQuality.Normal, 1, "A-04"), Instance("e3-center", "W8-012", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-buffer-early", "buffer", Instance("e4-center", "W8-012", BattleQuality.Normal, 0), Instance("e4-buffer", "W8-007", BattleQuality.Improved, 3)));
                catalog.Echoes.Add(Echo("echo-chain-lesson", "chain", Instance("e5-hand", "W8-013", BattleQuality.Normal, 0), Instance("e5-sign", "W8-014", BattleQuality.Normal, 1, "A-03"), Instance("e5-node", "W8-015", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-chain-early", "chain", Instance("e6-hand", "W8-013", BattleQuality.Improved, 0), Instance("e6-sign", "W8-014", BattleQuality.Normal, 1), Instance("e6-node", "W8-015", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-heal-lesson", "heal", Instance("e7-pack", "W8-016", BattleQuality.Normal, 0), Instance("e7-spring", "W8-017", BattleQuality.Normal, 1), Instance("e7-flag", "W8-018", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-heal-early", "heal", Instance("e8-pack", "W8-016", BattleQuality.Improved, 0), Instance("e8-spring", "W8-017", BattleQuality.Normal, 1), Instance("e8-shield", "W8-007", BattleQuality.Normal, 3)));
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

            public static BuqiEffectConfigRow ChargedEffect(
                int amount,
                int amountPerCharge,
                int chargeReadLimit,
                bool consume,
                string reasonCode)
            {
                BuqiEffectConfigRow effect = Effect(
                    BattleTrigger.OnUse,
                    BattleEffect.Damage,
                    BattleTarget.EnemyExecution,
                    amount,
                    reasonCode);
                effect.AmountPerCharge = amountPerCharge;
                effect.ChargeReadLimit = chargeReadLimit;
                effect.ChargeConsume = consume;
                return effect;
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
