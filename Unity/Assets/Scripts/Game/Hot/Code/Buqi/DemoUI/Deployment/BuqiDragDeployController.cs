using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.DemoUI.Deployment
{
    public sealed class BuqiDragDeployController
    {
        public const int BoardSlotCount = 8;
        public const int StorageSlotCount = 5;

        private readonly BuqiUIDemoCatalog m_Catalog;
        private readonly BuqiDeploymentSnapshot m_OpeningView;

        private BuqiDragDeployController(BuqiUIDemoCatalog catalog, BuqiDeploymentSnapshot openingView)
        {
            m_Catalog = catalog;
            m_OpeningView = openingView;
            View = openingView;
        }

        public BuqiDeploymentSnapshot View { get; private set; }

        public static BuqiDragDeployController Create(BuqiUIDemoCatalog catalog)
        {
            return Create(catalog, null, null);
        }

        public static BuqiDragDeployController Create(
            BuqiUIDemoCatalog catalog,
            IReadOnlyList<string> boardSlots,
            IReadOnlyList<string> storageSlots)
        {
            if (!TryCreate(catalog, boardSlots, storageSlots, out BuqiDragDeployController controller, out string error))
                throw new ArgumentException(error, nameof(catalog));
            return controller;
        }

        public static bool TryCreate(
            BuqiUIDemoCatalog catalog,
            IReadOnlyList<string> boardSlots,
            IReadOnlyList<string> storageSlots,
            out BuqiDragDeployController controller,
            out string error)
        {
            controller = null;
            if (catalog == null)
            {
                error = "装备目录不可用";
                return false;
            }

            if (!TryBuildSnapshot(catalog, boardSlots, storageSlots, out BuqiDeploymentSnapshot snapshot, out error))
                return false;

            controller = new BuqiDragDeployController(catalog, snapshot);
            return true;
        }

        public BuqiDeploymentTargetPreview Preview(BuqiDeploymentSlotRef source, BuqiDeploymentSlotRef target)
        {
            ResolveMove(source, target, out BuqiDeploymentPlacement placement, out int span, out string itemId,
                out List<BuqiDeploymentPlacement> remaining, out string reason);
            var boardSlots = new List<int>();
            if (target.Area == BuqiDeploymentArea.Board && span > 0)
            {
                for (int slot = target.Index; slot < target.Index + span; slot++)
                    boardSlots.Add(slot);
            }

            return new BuqiDeploymentTargetPreview(
                source,
                target,
                itemId,
                span,
                boardSlots.AsReadOnly(),
                string.IsNullOrEmpty(reason),
                reason);
        }

        public BuqiDeploymentCommandResult TryMove(BuqiDeploymentSlotRef source, BuqiDeploymentSlotRef target)
        {
            ResolveMove(source, target, out BuqiDeploymentPlacement placement, out int span, out string itemId,
                out List<BuqiDeploymentPlacement> remaining, out string reason);
            if (!string.IsNullOrEmpty(reason))
                return Rejected(reason);

            string[] boardSlots = CopySlots(View.BoardSlots);
            string[] storageSlots = CopySlots(View.StorageSlots);
            RemoveSource(boardSlots, storageSlots, source, placement);
            if (target.Area == BuqiDeploymentArea.Board)
            {
                for (int slot = target.Index; slot < target.Index + span; slot++)
                    boardSlots[slot] = itemId;
            }
            else
            {
                storageSlots[target.Index] = itemId;
            }

            if (!TryBuildSnapshot(m_Catalog, boardSlots, storageSlots, out BuqiDeploymentSnapshot next, out reason))
                return Rejected(reason);

            View = next;
            return new BuqiDeploymentCommandResult(true, string.Empty, View);
        }

        public BuqiDeploymentCommandResult Reset()
        {
            View = m_OpeningView;
            return new BuqiDeploymentCommandResult(true, string.Empty, View);
        }

        private void ResolveMove(
            BuqiDeploymentSlotRef source,
            BuqiDeploymentSlotRef target,
            out BuqiDeploymentPlacement placement,
            out int span,
            out string itemId,
            out List<BuqiDeploymentPlacement> remaining,
            out string reason)
        {
            placement = null;
            span = 0;
            itemId = string.Empty;
            remaining = new List<BuqiDeploymentPlacement>();
            reason = string.Empty;

            if (!IsValidSlot(source))
            {
                reason = "来源位置无效";
                return;
            }
            if (!IsValidSlot(target))
            {
                reason = "目标位置无效";
                return;
            }

            if (source.Area == BuqiDeploymentArea.Storage)
            {
                itemId = View.StorageSlots[source.Index];
                if (string.IsNullOrEmpty(itemId))
                {
                    reason = "来源位置没有装备";
                    return;
                }
                BuqiUIDemoItemDefinition item = m_Catalog.FindItem(itemId);
                if (item == null)
                {
                    reason = "装备已不存在";
                    return;
                }
                span = item.Size;
            }
            else
            {
                placement = FindPlacement(source.Index);
                if (placement == null)
                {
                    reason = "来源位置没有装备";
                    return;
                }
                itemId = placement.ItemId;
                span = placement.Span;
            }

            foreach (BuqiDeploymentPlacement candidate in View.Placements)
            {
                if (placement != null && candidate.ItemId == placement.ItemId && candidate.AnchorSlot == placement.AnchorSlot)
                    continue;
                remaining.Add(candidate);
            }

            if (target.Area == BuqiDeploymentArea.Board)
            {
                if (target.Index + span > BoardSlotCount)
                {
                    reason = "装备超出棋盘范围";
                    return;
                }
                for (int slot = target.Index; slot < target.Index + span; slot++)
                {
                    if (IsOccupiedByRemaining(slot, remaining))
                    {
                        reason = "目标位置与其他装备重叠";
                        return;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(View.StorageSlots[target.Index]) && source != target)
            {
                reason = "目标仓库位置已占用";
                return;
            }
        }

        private BuqiDeploymentPlacement FindPlacement(int boardSlot)
        {
            foreach (BuqiDeploymentPlacement placement in View.Placements)
            {
                if (boardSlot >= placement.AnchorSlot && boardSlot < placement.AnchorSlot + placement.Span)
                    return placement;
            }
            return null;
        }

        private static bool IsOccupiedByRemaining(int slot, List<BuqiDeploymentPlacement> placements)
        {
            foreach (BuqiDeploymentPlacement placement in placements)
            {
                if (slot >= placement.AnchorSlot && slot < placement.AnchorSlot + placement.Span)
                    return true;
            }
            return false;
        }

        private static void RemoveSource(
            string[] boardSlots,
            string[] storageSlots,
            BuqiDeploymentSlotRef source,
            BuqiDeploymentPlacement placement)
        {
            if (source.Area == BuqiDeploymentArea.Storage)
            {
                storageSlots[source.Index] = string.Empty;
                return;
            }

            for (int slot = placement.AnchorSlot; slot < placement.AnchorSlot + placement.Span; slot++)
                boardSlots[slot] = string.Empty;
        }

        private static bool TryBuildSnapshot(
            BuqiUIDemoCatalog catalog,
            IReadOnlyList<string> boardInput,
            IReadOnlyList<string> storageInput,
            out BuqiDeploymentSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (!TryCopyFixedSlots(boardInput, BoardSlotCount, out string[] boardSlots, out error, true) ||
                !TryCopyFixedSlots(storageInput, StorageSlotCount, out string[] storageSlots, out error, false))
                return false;

            var placements = new List<BuqiDeploymentPlacement>();
            var occupied = new bool[BoardSlotCount];
            var boardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int slot = 0; slot < boardSlots.Length; slot++)
            {
                string itemId = boardSlots[slot];
                if (string.IsNullOrEmpty(itemId) || occupied[slot])
                    continue;
                if (!boardIds.Add(itemId))
                {
                    error = "同一装备不能重复上阵";
                    return false;
                }

                BuqiUIDemoItemDefinition item = catalog.FindItem(itemId);
                if (item == null)
                {
                    error = "装备已不存在";
                    return false;
                }
                if (item.Size < 1 || slot + item.Size > BoardSlotCount)
                {
                    error = "装备超出棋盘范围";
                    return false;
                }
                for (int offset = 0; offset < item.Size; offset++)
                {
                    int targetSlot = slot + offset;
                    if (occupied[targetSlot])
                    {
                        error = "棋盘上存在重叠装备";
                        return false;
                    }
                    if (!string.IsNullOrEmpty(boardSlots[targetSlot]) && boardSlots[targetSlot] != itemId)
                    {
                        error = "棋盘上存在重叠装备";
                        return false;
                    }
                    occupied[targetSlot] = true;
                    boardSlots[targetSlot] = itemId;
                }
                placements.Add(new BuqiDeploymentPlacement(itemId, slot, item.Size));
            }

            var storageIds = new HashSet<string>(StringComparer.Ordinal);
            for (int slot = 0; slot < storageSlots.Length; slot++)
            {
                string itemId = storageSlots[slot];
                if (string.IsNullOrEmpty(itemId))
                    continue;
                if (catalog.FindItem(itemId) == null)
                {
                    error = "装备已不存在";
                    return false;
                }
                if (!storageIds.Add(itemId) || boardIds.Contains(itemId))
                {
                    error = "同一装备不能重复放置";
                    return false;
                }
            }

            snapshot = new BuqiDeploymentSnapshot(
                Array.AsReadOnly(boardSlots),
                Array.AsReadOnly(storageSlots),
                Array.AsReadOnly(placements.ToArray()));
            return true;
        }

        private static bool TryCopyFixedSlots(
            IReadOnlyList<string> input,
            int count,
            out string[] copy,
            out string error,
            bool isBoard)
        {
            copy = new string[count];
            error = string.Empty;
            if (input != null && input.Count != count)
            {
                error = isBoard
                    ? "棋盘位置数量无效"
                    : "仓库位置数量无效";
                return false;
            }
            for (int slot = 0; slot < count; slot++)
                copy[slot] = input == null ? string.Empty : input[slot] ?? string.Empty;
            return true;
        }

        private static string[] CopySlots(IReadOnlyList<string> slots)
        {
            var copy = new string[slots.Count];
            for (int index = 0; index < slots.Count; index++)
                copy[index] = slots[index] ?? string.Empty;
            return copy;
        }

        private static bool IsValidSlot(BuqiDeploymentSlotRef slot)
        {
            return slot.Index >= 0 && (slot.Area == BuqiDeploymentArea.Board
                ? slot.Index < BoardSlotCount
                : slot.Index < StorageSlotCount);
        }

        private BuqiDeploymentCommandResult Rejected(string reason)
        {
            return new BuqiDeploymentCommandResult(false, reason, View);
        }
    }
}
