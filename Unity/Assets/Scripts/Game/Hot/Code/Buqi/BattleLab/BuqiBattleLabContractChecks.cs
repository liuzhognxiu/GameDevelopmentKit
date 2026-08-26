using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.BattleLab
{
    /// <summary>
    /// 不器战斗实验室的跨端行为契约。Unity EditMode 与 .NET 无头端共用同一组检查。
    /// </summary>
    public static class BuqiBattleLabContractChecks
    {
        public static List<string> RunAll()
        {
            var failures = new List<string>();
            RunCheck("目录投影", CheckCatalogProjection, failures);
            RunCheck("棋盘尺寸", CheckBoardSlotRange, failures);
            RunCheck("原子棋盘", CheckAtomicBoards, failures);
            RunCheck("无效内容", CheckInvalidContentProjection, failures);
            RunCheck("畸形效果", CheckMalformedEffectProjection, failures);
            RunCheck("只读模型", CheckModelDefensiveCopies, failures);
            return failures;
        }

        private static void CheckCatalogProjection(List<string> failures)
        {
            BuqiConfigCatalog source = CreateSource(8);
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("目录投影：创建失败：{0}", error));
                return;
            }

            Expect(catalog.BoardSlotCount == 8, "目录投影：棋盘格数不是 8", failures);
            Expect(
                catalog.Heroes.Select(hero => hero.HeroId).SequenceEqual(
                    new[] { "balanced", "guarded", "survivor" }, StringComparer.Ordinal),
                "目录投影：英雄顺序不稳定",
                failures);
            ExpectHero(catalog.Heroes[0], "归衡者", 100, 0, 0, "归衡者", failures);
            ExpectHero(catalog.Heroes[1], "铁衣客", 85, 20, 0, "铁衣客", failures);
            ExpectHero(catalog.Heroes[2], "长生客", 115, 0, 4, "长生客", failures);

            string[] itemIds = catalog.Items.Select(item => item.DefinitionId).ToArray();
            string[] sortedItemIds = itemIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Expect(
                itemIds.SequenceEqual(sortedItemIds, StringComparer.Ordinal),
                "目录投影：道具未按 DefinitionId 序号排序",
                failures);
            Expect(
                catalog.Items.All(item => item.Quality == BuqiQuality.Normal),
                "目录投影：卡库道具不是普通品质",
                failures);

            BuqiBattleLabPresetOpponent opponent = catalog.PresetOpponents.Single();
            Expect(
                !ReferenceEquals(opponent.Snapshot, source.Echoes[0].Snapshot),
                "目录投影：预设快照仍引用配置对象",
                failures);
            source.Echoes[0].Snapshot.Items[0].InstanceId = "mutated-source";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "echo-item",
                "目录投影：修改配置快照污染了预设快照",
                failures);
        }

        private static void CheckBoardSlotRange(List<string> failures)
        {
            Expect(
                BuqiBattleLabCatalog.TryCreate(CreateSource(10), out _, out string tenSlotError),
                BuqiText.Format("棋盘尺寸：10 格创建失败：{0}", tenSlotError),
                failures);

            bool accepted = BuqiBattleLabCatalog.TryCreate(
                CreateSource(7), out _, out string sevenSlotError);
            Expect(!accepted, "棋盘尺寸：7 格被错误接受", failures);
            Expect(
                sevenSlotError == "战斗实验室棋盘只支持 8 至 10 格",
                BuqiText.Format("棋盘尺寸：7 格错误不精确：{0}", sevenSlotError),
                failures);
        }

        private static void CheckAtomicBoards(List<string> failures)
        {
            Expect(
                !typeof(BuqiBattleLabBoard).GetProperty(
                    nameof(BuqiBattleLabBoard.View)).CanWrite,
                "原子棋盘：公开视图属性不应提供 setter",
                failures);

            foreach (int slotCount in new[] { 8, 10 })
            {
                var board = new BuqiBattleLabBoard(slotCount);
                var small = new BuqiBattleLabPlacement(
                    "p-1", "small", "小型", 1,
                    BuqiQuality.Normal, 0, string.Empty);
                var medium = new BuqiBattleLabPlacement(
                    "p-2", "medium", "中型", 2,
                    BuqiQuality.Normal, slotCount - 2, string.Empty);

                Expect(
                    board.View.SlotCount == slotCount,
                    BuqiText.Format("原子棋盘：{0} 格视图尺寸错误", slotCount),
                    failures);
                Expect(
                    board.TryAdd(small, out string reason),
                    BuqiText.Format("原子棋盘：{0} 格添加小型道具失败：{1}", slotCount, reason),
                    failures);
                Expect(
                    board.TryAdd(medium, out reason),
                    BuqiText.Format("原子棋盘：{0} 格添加中型道具失败：{1}", slotCount, reason),
                    failures);

                BuqiBattleLabBoardView beforeLargePreview = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> beforeLargeSequence =
                    board.CopyPlacements();
                BuqiBattleLabPlacementPreview largePreview = board.Preview(
                    "large", 3, slotCount - 2, string.Empty);
                Expect(
                    !largePreview.Accepted && largePreview.Reason == "需要连续 3 格",
                    BuqiText.Format(
                        "原子棋盘：{0} 格大型道具越界错误不精确：{1}",
                        slotCount,
                        largePreview.Reason),
                    failures);
                Expect(
                    largePreview.CoveredSlots.SequenceEqual(
                        new[] { slotCount - 2, slotCount - 1, slotCount }),
                    BuqiText.Format("原子棋盘：{0} 格大型道具预览范围错误", slotCount),
                    failures);
                Expect(
                    ReferenceEquals(beforeLargePreview, board.View) &&
                    SamePlacementSequence(beforeLargeSequence, board.CopyPlacements()),
                    BuqiText.Format("原子棋盘：{0} 格失败预览改写了棋盘", slotCount),
                    failures);

                var overlap = new BuqiBattleLabPlacement(
                    "p-overlap", "overlap", "重叠", 1,
                    BuqiQuality.Normal, 0, string.Empty);
                BuqiBattleLabBoardView beforeOverlap = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> beforeOverlapSequence =
                    board.CopyPlacements();
                Expect(
                    !board.TryAdd(overlap, out reason) && reason == "与小型重叠",
                    BuqiText.Format("原子棋盘：{0} 格重叠错误不精确：{1}", slotCount, reason),
                    failures);
                Expect(
                    ReferenceEquals(beforeOverlap, board.View) &&
                    SamePlacementSequence(beforeOverlapSequence, board.CopyPlacements()),
                    BuqiText.Format("原子棋盘：{0} 格失败重叠改写了棋盘", slotCount),
                    failures);

                var duplicate = new BuqiBattleLabPlacement(
                    "p-1", "duplicate", "重复", 1,
                    BuqiQuality.Normal, 3, string.Empty);
                Expect(
                    !board.TryAdd(duplicate, out reason) && reason == "同一实例不能重复放置",
                    BuqiText.Format("原子棋盘：{0} 格重复实例错误不精确：{1}", slotCount, reason),
                    failures);
                Expect(
                    !board.TryMove("missing", 1, out reason) && reason == "来源位置没有道具",
                    BuqiText.Format("原子棋盘：{0} 格未知来源错误不精确：{1}", slotCount, reason),
                    failures);
                Expect(
                    board.TryMove("p-2", 1, out reason),
                    BuqiText.Format("原子棋盘：{0} 格移动中型道具失败：{1}", slotCount, reason),
                    failures);
                Expect(
                    board.TryRemove("p-1", out reason),
                    BuqiText.Format("原子棋盘：{0} 格移除小型道具失败：{1}", slotCount, reason),
                    failures);
                Expect(
                    board.Clear() && board.View.Placements.Count == 0,
                    BuqiText.Format("原子棋盘：{0} 格清空失败", slotCount),
                    failures);
            }
        }

        private static bool SamePlacementSequence(
            IReadOnlyList<BuqiBattleLabPlacement> left,
            IReadOnlyList<BuqiBattleLabPlacement> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int index = 0; index < left.Count; index++)
            {
                if (!ReferenceEquals(left[index], right[index]))
                    return false;
            }
            return true;
        }

        private static void CheckInvalidContentProjection(List<string> failures)
        {
            bool accepted = BuqiBattleLabCatalog.TryCreate(null, out _, out string unavailableError);
            Expect(!accepted, "无效内容：空配置被错误接受", failures);
            Expect(
                unavailableError == "不器战斗实验室配置不可用",
                BuqiText.Format("无效内容：空配置错误不精确：{0}", unavailableError),
                failures);

            BuqiConfigCatalog source = CreateSource(8);
            source.Items[0].Size = (BuqiSize)4;
            source.Echoes[0].Snapshot.Items.Clear();
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("无效内容：目录不应丢弃无效行：{0}", error));
                return;
            }

            BuqiBattleLabItemDefinition invalidItem = catalog.Items.Single(
                item => item.DefinitionId == "z-last");
            Expect(!invalidItem.Enabled, "无效内容：非法尺寸道具仍被启用", failures);
            Expect(
                invalidItem.Error == "道具尺寸必须为 1 至 3 格",
                BuqiText.Format("无效内容：非法尺寸错误不精确：{0}", invalidItem.Error),
                failures);
            Expect(
                catalog.PresetOpponents.Single().ValidationErrors.Count > 0,
                "无效内容：预设快照校验错误未被保留",
                failures);
        }

        private static void CheckModelDefensiveCopies(List<string> failures)
        {
            var tags = new List<string> { "before" };
            var itemDefinition = new BuqiBattleLabItemDefinition(
                "copy-item", "复制契约", "复制契约", 1, BuqiQuality.Normal, 30,
                "copy", "copy", "copy", tags, true, string.Empty);
            tags[0] = "after";
            Expect(
                itemDefinition.Tags[0] == "before",
                "只读模型：道具标签仍引用构造参数",
                failures);

            var snapshot = new BuildSnapshot
            {
                SnapshotId = "copy-snapshot",
                ContentVersion = "copy-v1",
                InitialExecution = 100,
                Items = new List<ItemInstance>
                {
                    new ItemInstance
                    {
                        InstanceId = "copy-instance",
                        DefinitionId = "copy-item",
                        Quality = (int)BuqiQuality.Normal,
                    },
                },
            };
            var validationErrors = new List<string> { "before" };
            var opponent = new BuqiBattleLabPresetOpponent(
                "copy-opponent", "复制对手", "copy", snapshot, validationErrors);
            snapshot.Items[0].InstanceId = "after";
            validationErrors[0] = "after";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "copy-instance" &&
                opponent.ValidationErrors[0] == "before",
                "只读模型：预设对手仍引用构造参数",
                failures);
            BuildSnapshot exposedSnapshot = opponent.Snapshot;
            exposedSnapshot.Items[0].InstanceId = "mutated-view";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "copy-instance",
                "只读模型：调用方可改写预设对手快照",
                failures);

            var placements = new List<BuqiBattleLabPlacement>
            {
                new BuqiBattleLabPlacement(
                    "copy-placement", "copy-item", "复制契约", 1,
                    BuqiQuality.Normal, 0, string.Empty),
            };
            var occupiedInstanceIds = new List<string> { "copy-placement" };
            var board = new BuqiBattleLabBoardView(8, placements, occupiedInstanceIds);
            placements.Clear();
            occupiedInstanceIds[0] = "after";
            Expect(
                board.Placements.Count == 1 &&
                board.OccupiedInstanceIds[0] == "copy-placement",
                "只读模型：棋盘视图仍引用构造参数",
                failures);

            var coveredSlots = new List<int> { 2, 3 };
            var preview = new BuqiBattleLabPlacementPreview(
                BuqiBattleLabSide.Player, 2, 2, coveredSlots, true, string.Empty);
            coveredSlots[0] = 7;
            Expect(
                preview.CoveredSlots[0] == 2,
                "只读模型：落点预览仍引用构造参数",
                failures);
        }

        private static void CheckMalformedEffectProjection(List<string> failures)
        {
            BuqiConfigCatalog source = CreateSource(8);
            BuqiItemConfigRow nullEffects = Item(
                "effects-null", "空效果列表", BuqiSize.S, 30);
            nullEffects.Effects = null;
            source.Items.Add(nullEffects);

            BuqiItemConfigRow nullEffectEntry = Item(
                "effect-entry-null", "空效果项", BuqiSize.S, 30);
            nullEffectEntry.Effects.Add(null);
            source.Items.Add(nullEffectEntry);

            source.Echoes[0].EchoId = "echo-effects-null";
            source.Echoes[0].Snapshot.Items[0].DefinitionId = "effects-null";
            source.Echoes.Add(new BuqiEchoConfigRow
            {
                EchoId = "echo-effect-entry-null",
                DisplayName = "空效果项道影",
                Build = "balanced",
                Snapshot = new BuqiBuildSnapshotConfigRow
                {
                    SnapshotId = "echo-effect-entry-null-snapshot",
                    ArchetypeId = "balanced",
                    InitialExecution = 100,
                    Items = new List<BuqiItemInstanceConfigRow>
                    {
                        new BuqiItemInstanceConfigRow
                        {
                            InstanceId = "echo-effect-entry-null-item",
                            DefinitionId = "effect-entry-null",
                            Quality = BuqiQuality.Normal,
                            AnchorSlot = 0,
                        },
                    },
                },
            });

            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("畸形效果：目录不应丢弃畸形行：{0}", error));
                return;
            }

            BuqiBattleLabItemDefinition nullEffectsItem = catalog.Items.Single(
                item => item.DefinitionId == "effects-null");
            Expect(!nullEffectsItem.Enabled, "畸形效果：空效果列表道具仍被启用", failures);
            Expect(
                nullEffectsItem.Error == "道具效果列表不可为空",
                BuqiText.Format("畸形效果：空效果列表错误不精确：{0}", nullEffectsItem.Error),
                failures);

            BuqiBattleLabItemDefinition nullEffectEntryItem = catalog.Items.Single(
                item => item.DefinitionId == "effect-entry-null");
            Expect(!nullEffectEntryItem.Enabled, "畸形效果：含空效果项道具仍被启用", failures);
            Expect(
                nullEffectEntryItem.Error == "道具效果列表不能包含空项",
                BuqiText.Format("畸形效果：空效果项错误不精确：{0}", nullEffectEntryItem.Error),
                failures);

            Expect(
                source.Items.Single(item => item.DefinitionId == "effects-null").Effects == null &&
                source.Items.Single(item => item.DefinitionId == "effect-entry-null").Effects[0] == null,
                "畸形效果：目录投影改写了源配置",
                failures);

            BuqiBattleLabPresetOpponent nullEffectsOpponent = catalog.PresetOpponents.Single(
                opponent => opponent.EchoId == "echo-effects-null");
            Expect(
                nullEffectsOpponent.ValidationErrors.Any(
                    validationError => validationError.Contains("effects-null")),
                "畸形效果：空效果列表定义仍可用于预设模拟",
                failures);

            BuqiBattleLabPresetOpponent nullEffectEntryOpponent = catalog.PresetOpponents.Single(
                opponent => opponent.EchoId == "echo-effect-entry-null");
            Expect(
                nullEffectEntryOpponent.ValidationErrors.Any(
                    validationError => validationError.Contains("effect-entry-null")),
                "畸形效果：含空效果项定义仍可用于预设模拟",
                failures);
        }

        private static BuqiConfigCatalog CreateSource(int boardSlotCount)
        {
            var source = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "battle-lab-contract-v1",
                    BoardSlotCount = boardSlotCount,
                },
            };

            // 故意逆序插入，契约要求投影按 DefinitionId 序号排序。
            source.Items.Add(Item("z-last", "后置法门", BuqiSize.S, 30));
            source.Items.Add(Item("m-middle", "中置法门", BuqiSize.M, 40));
            source.Items.Add(Item("a-first", "前置法门", BuqiSize.L, 50));
            source.Echoes.Add(new BuqiEchoConfigRow
            {
                EchoId = "echo-balanced",
                DisplayName = "归衡道影",
                Build = "balanced",
                Snapshot = new BuqiBuildSnapshotConfigRow
                {
                    SnapshotId = "echo-snapshot",
                    ArchetypeId = "balanced",
                    InitialExecution = 100,
                    InitialBuffer = 0,
                    InitialNoiseDebt = 0,
                    Items = new List<BuqiItemInstanceConfigRow>
                    {
                        new BuqiItemInstanceConfigRow
                        {
                            InstanceId = "echo-item",
                            DefinitionId = "a-first",
                            Quality = BuqiQuality.Normal,
                            AnchorSlot = 0,
                        },
                    },
                },
            });
            return source;
        }

        private static BuqiItemConfigRow Item(
            string definitionId,
            string displayName,
            BuqiSize size,
            int cooldownTicks)
        {
            return new BuqiItemConfigRow
            {
                DefinitionId = definitionId,
                DisplayName = displayName,
                EffectDescription = "契约道具",
                Size = size,
                BaseCooldownTicks = cooldownTicks,
                ArchetypeId = "balanced",
                Role = "contract",
                PositionHint = "任意",
                Tags = new List<string> { "contract" },
            };
        }

        private static void ExpectHero(
            BuqiBattleLabHeroDefinition hero,
            string displayName,
            int initialExecution,
            int initialBuffer,
            int initialNoiseDebt,
            string label,
            List<string> failures)
        {
            Expect(
                hero.DisplayName == displayName &&
                hero.InitialExecution == initialExecution &&
                hero.InitialBuffer == initialBuffer &&
                hero.InitialNoiseDebt == initialNoiseDebt,
                BuqiText.Format("目录投影：{0}英雄参数错误", label),
                failures);
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private static void RunCheck(
            string name,
            Action<List<string>> check,
            List<string> failures)
        {
            try
            {
                check(failures);
            }
            catch (Exception exception)
            {
                failures.Add(BuqiText.Format("{0}：检查抛出 {1}: {2}",
                    name, exception.GetType().Name, exception.Message));
            }
        }
    }
}
