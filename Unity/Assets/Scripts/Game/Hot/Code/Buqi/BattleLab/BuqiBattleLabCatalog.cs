using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.BattleLab
{
    /// <summary>
    /// 将可变内容配置投影为战斗实验室使用的稳定只读目录。
    /// </summary>
    public sealed class BuqiBattleLabCatalog
    {
        private BuqiBattleLabCatalog(
            int boardSlotCount,
            IReadOnlyList<BuqiBattleLabHeroDefinition> heroes,
            IReadOnlyList<BuqiBattleLabItemDefinition> items,
            IReadOnlyList<BuqiBattleLabPresetOpponent> presetOpponents,
            BuqiDefinitionProvider definitionProvider)
        {
            BoardSlotCount = boardSlotCount;

            var heroCopy = new BuqiBattleLabHeroDefinition[heroes.Count];
            for (int index = 0; index < heroCopy.Length; index++)
                heroCopy[index] = heroes[index];
            Heroes = Array.AsReadOnly(heroCopy);

            var itemCopy = new BuqiBattleLabItemDefinition[items.Count];
            for (int index = 0; index < itemCopy.Length; index++)
                itemCopy[index] = items[index];
            Items = Array.AsReadOnly(itemCopy);

            var opponentCopy = new BuqiBattleLabPresetOpponent[presetOpponents.Count];
            for (int index = 0; index < opponentCopy.Length; index++)
                opponentCopy[index] = presetOpponents[index];
            PresetOpponents = Array.AsReadOnly(opponentCopy);
            DefinitionProvider = definitionProvider;
        }

        public int BoardSlotCount { get; }
        public IReadOnlyList<BuqiBattleLabHeroDefinition> Heroes { get; }
        public IReadOnlyList<BuqiBattleLabItemDefinition> Items { get; }
        public IReadOnlyList<BuqiBattleLabPresetOpponent> PresetOpponents { get; }
        public BuqiDefinitionProvider DefinitionProvider { get; }

        public static bool TryCreate(
            BuqiConfigCatalog source,
            out BuqiBattleLabCatalog catalog,
            out string error)
        {
            catalog = null;
            error = string.Empty;
            if (source?.Global == null || source.Items == null || source.Echoes == null)
            {
                error = "不器战斗实验室配置不可用";
                return false;
            }
            if (source.Global.BoardSlotCount < 8 || source.Global.BoardSlotCount > 10)
            {
                error = "战斗实验室棋盘只支持 8 至 10 格";
                return false;
            }

            var heroes = new List<BuqiBattleLabHeroDefinition>
            {
                new BuqiBattleLabHeroDefinition("balanced", "归衡者", "均衡", 100, 0, 0),
                new BuqiBattleLabHeroDefinition("guarded", "铁衣客", "护盾", 85, 20, 0),
                new BuqiBattleLabHeroDefinition("survivor", "长生客", "生存", 115, 0, 4),
            };

            var itemRows = new List<BuqiItemConfigRow>(source.Items);
            itemRows.Sort((left, right) => StringComparer.Ordinal.Compare(
                left == null ? string.Empty : left.DefinitionId,
                right == null ? string.Empty : right.DefinitionId));
            var items = new List<BuqiBattleLabItemDefinition>();
            foreach (BuqiItemConfigRow row in itemRows)
            {
                if (row == null)
                    continue;

                int size = (int)row.Size;
                string itemError = GetItemError(row, size);
                bool enabled = string.IsNullOrEmpty(itemError);
                items.Add(new BuqiBattleLabItemDefinition(
                    row.DefinitionId,
                    row.DisplayName,
                    row.EffectDescription,
                    size,
                    BuqiQuality.Normal,
                    row.BaseCooldownTicks,
                    row.ArchetypeId,
                    row.Role,
                    row.PositionHint,
                    row.Tags,
                    enabled,
                    itemError));
            }

            BuqiDefinitionProvider definitionProvider = CreateDefinitionProvider(source, itemRows);
            var echoRows = new List<BuqiEchoConfigRow>(source.Echoes);
            echoRows.Sort((left, right) => StringComparer.Ordinal.Compare(
                left == null ? string.Empty : left.EchoId,
                right == null ? string.Empty : right.EchoId));
            var presetOpponents = new List<BuqiBattleLabPresetOpponent>();
            foreach (BuqiEchoConfigRow row in echoRows)
            {
                if (row == null)
                    continue;

                BuildSnapshot snapshot = CopySnapshot(row.Snapshot, source.Global.ContentVersion);
                BuqiBoardValidator.Validate(snapshot, definitionProvider, out List<string> validationErrors);
                presetOpponents.Add(new BuqiBattleLabPresetOpponent(
                    row.EchoId,
                    string.IsNullOrEmpty(row.DisplayName) ? row.EchoId : row.DisplayName,
                    row.Build,
                    snapshot,
                    validationErrors));
            }

            catalog = new BuqiBattleLabCatalog(
                source.Global.BoardSlotCount,
                heroes,
                items,
                presetOpponents,
                definitionProvider);
            return true;
        }

        private static BuqiDefinitionProvider CreateDefinitionProvider(
            BuqiConfigCatalog source,
            IReadOnlyList<BuqiItemConfigRow> itemRows)
        {
            var providerSource = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = source.Global.ContentVersion,
                },
            };
            foreach (BuqiItemConfigRow row in itemRows)
            {
                if (row != null && string.IsNullOrEmpty(GetItemError(row, (int)row.Size)))
                    providerSource.Items.Add(CopyProviderItem(row));
            }
            return new BuqiDefinitionProvider(providerSource);
        }

        private static string GetItemError(BuqiItemConfigRow row, int size)
        {
            if (size < 1 || size > 3)
                return "道具尺寸必须为 1 至 3 格";
            if (row.Effects == null)
                return "道具效果列表不可为空";
            foreach (BuqiEffectConfigRow effect in row.Effects)
            {
                if (effect == null)
                    return "道具效果列表不能包含空项";
            }
            return string.Empty;
        }

        private static BuqiItemConfigRow CopyProviderItem(BuqiItemConfigRow source)
        {
            var item = new BuqiItemConfigRow
            {
                DefinitionId = source.DefinitionId,
                Size = source.Size,
                BaseCooldownTicks = source.BaseCooldownTicks,
            };
            if (source.Effects == null)
                return item;

            foreach (BuqiEffectConfigRow effect in source.Effects)
            {
                if (effect != null)
                    item.Effects.Add(CopyProviderEffect(effect));
            }
            return item;
        }

        private static BuqiEffectConfigRow CopyProviderEffect(BuqiEffectConfigRow source)
        {
            return new BuqiEffectConfigRow
            {
                Trigger = source.Trigger,
                Effect = source.Effect,
                Target = source.Target,
                Amount = source.Amount,
                DurationTicks = source.DurationTicks,
                ReasonCode = source.ReasonCode,
                ConditionKind = source.ConditionKind,
                ConditionThreshold = source.ConditionThreshold,
                UseCountThreshold = source.UseCountThreshold,
                ResetCountOnReached = source.ResetCountOnReached,
                CriticalChanceBps = source.CriticalChanceBps,
                RepeatCount = source.RepeatCount,
                RageThreshold = source.RageThreshold,
                RageDurationTicks = source.RageDurationTicks,
                RageCooldownReductionBps = source.RageCooldownReductionBps,
                FlightDamageBonusBps = source.FlightDamageBonusBps,
                FlightEndDamage = source.FlightEndDamage,
            };
        }

        private static BuildSnapshot CopySnapshot(
            BuqiBuildSnapshotConfigRow source,
            string contentVersion)
        {
            if (source == null)
                return null;

            var snapshot = new BuildSnapshot
            {
                SnapshotId = source.SnapshotId,
                ContentVersion = contentVersion,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };
            if (source.Items == null)
                return snapshot;

            foreach (BuqiItemInstanceConfigRow row in source.Items)
            {
                if (row == null)
                {
                    snapshot.Items.Add(null);
                    continue;
                }

                snapshot.Items.Add(new ItemInstance
                {
                    InstanceId = row.InstanceId,
                    DefinitionId = row.DefinitionId,
                    Quality = (int)row.Quality,
                    AnchorSlot = row.AnchorSlot,
                    AnnotationId = row.RefinementId,
                });
            }
            return snapshot;
        }
    }
}
