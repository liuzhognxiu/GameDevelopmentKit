using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
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

            Assert.That(provider.ContentVersion, Is.EqualTo("buqi-step3-cv1"));
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
        public void ConfigValidator_AcceptsStep3MinimumCatalog()
        {
            BuqiConfigCatalog catalog = BuqiConfigTestData.CreateValidCatalog();

            List<string> errors = BuqiConfigValidator.Validate(catalog);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
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
            Assert.That(Contains(errors, "missing enabled item W8-015"), Is.True, string.Join("\n", errors));
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

        private static bool Contains(List<string> errors, string fragment)
        {
            foreach (string error in errors)
            {
                if (error.Contains(fragment))
                    return true;
            }
            return false;
        }

        private static class BuqiConfigTestData
        {
            public static BuqiConfigCatalog CreateValidCatalog()
            {
                var catalog = new BuqiConfigCatalog
                {
                    Global = new BuqiGlobalConfigRow
                    {
                        ContentVersion = "buqi-step3-cv1",
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
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-003-damage"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Haste, BattleTarget.LeftAdjacentItem, 2000, "W8-003-haste", 30)));
                catalog.Items.Add(Item("W8-005", BattleSize.M, 4, 70, "fast",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-005-adjacent-charge"),
                    ChargedEffect(6, 2, 3, true, "W8-005-damage")));
                catalog.Items.Add(Item("W8-006", BattleSize.L, 6, 100, "fast",
                    Effect(BattleTrigger.OnBattleStart, BattleEffect.Haste, BattleTarget.AllAdjacentItems, 1500, "W8-006-opening-haste", 50),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 16, "W8-006-damage"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Noise, BattleTarget.Self, 2, "W8-006-noise")));
                catalog.Items.Add(Item("W8-007", BattleSize.S, 2, 42, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 7, "W8-007-buffer")));
                catalog.Items.Add(Item("W8-008", BattleSize.S, 2, 55, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-008-charge"),
                    ConditionEffect(BattleConditionKind.BufferLost, 0, BattleEffect.Damage, BattleTarget.EnemyExecution, 8, "W8-008-buffer-counter")));
                catalog.Items.Add(Item("W8-012", BattleSize.L, 6, 90, "buffer",
                    Effect(BattleTrigger.OnUse, BattleEffect.Buffer, BattleTarget.Self, 12, "W8-012-buffer"),
                    ConditionEffect(BattleConditionKind.BufferLost, 0, BattleEffect.Damage, BattleTarget.EnemyExecution, 14, "W8-012-buffer-counter")));
                catalog.Items.Add(Item("W8-013", BattleSize.S, 2, 50, "chain",
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 4, "W8-013-damage"),
                    Effect(BattleTrigger.OnUse, BattleEffect.Charge, BattleTarget.RightAdjacentItem, 1, "W8-013-pass-charge")));
                catalog.Items.Add(Item("W8-014", BattleSize.S, 2, 60, "chain",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Charge, BattleTarget.Self, 1, "W8-014-adjacent-charge"),
                    ChargedEffect(3, 3, 2, true, "W8-014-damage")));
                catalog.Items.Add(Item("W8-015", BattleSize.M, 4, 65, "chain",
                    Effect(BattleTrigger.OnAdjacentUse, BattleEffect.Haste, BattleTarget.Self, 2000, "W8-015-adjacent-haste", 30),
                    Effect(BattleTrigger.OnUse, BattleEffect.Damage, BattleTarget.EnemyExecution, 7, "W8-015-damage")));

                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-01", DisplayName = "加急", Summary = "基础冷却 -15%，主动使用产生 1 失衡。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-03", DisplayName = "复写", Summary = "每场首次主动使用后以 50% 效果追加一次。" });
                catalog.Refinements.Add(new BuqiRefinementConfigRow { RefinementId = "A-04", DisplayName = "可靠", Summary = "不受敌方延迟，也不能获得友方加速。" });

                catalog.Echoes.Add(Echo("echo-fast-lesson", "fast", Instance("e1-deadline", "W8-006", BattleQuality.Normal, 0), Instance("e1-board", "W8-005", BattleQuality.Normal, 3), Instance("e1-urgent", "W8-003", BattleQuality.Normal, 5, "A-01")));
                catalog.Echoes.Add(Echo("echo-fast-early", "fast", Instance("e2-board", "W8-005", BattleQuality.Improved, 0), Instance("e2-urgent", "W8-003", BattleQuality.Normal, 2), Instance("e2-buffer", "W8-007", BattleQuality.Normal, 3)));
                catalog.Echoes.Add(Echo("echo-buffer-lesson", "buffer", Instance("e3-buffer", "W8-007", BattleQuality.Normal, 0), Instance("e3-risk", "W8-008", BattleQuality.Normal, 1, "A-04"), Instance("e3-center", "W8-012", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-buffer-early", "buffer", Instance("e4-center", "W8-012", BattleQuality.Normal, 0), Instance("e4-buffer", "W8-007", BattleQuality.Improved, 3)));
                catalog.Echoes.Add(Echo("echo-chain-lesson", "chain", Instance("e5-hand", "W8-013", BattleQuality.Normal, 0), Instance("e5-sign", "W8-014", BattleQuality.Normal, 1, "A-03"), Instance("e5-node", "W8-015", BattleQuality.Normal, 2)));
                catalog.Echoes.Add(Echo("echo-chain-early", "chain", Instance("e6-hand", "W8-013", BattleQuality.Improved, 0), Instance("e6-sign", "W8-014", BattleQuality.Normal, 1), Instance("e6-node", "W8-015", BattleQuality.Normal, 2)));
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
                    Tier = "lesson",
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
