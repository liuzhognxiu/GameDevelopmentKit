using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.DemoUI.Deployment
{
    public sealed class BuqiDragDeployController
    {
        public const int BoardSlotCount = BuqiRunRules.BoardSlotCount;
        public const int StorageSlotCount = BuqiRunRules.StorageSlotCount;

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
            TryPlanMove(source, target, out _, out BuqiDeploymentSlotRef destination, out int span,
                out string itemId, out string reason);
            var boardSlots = new List<int>();
            if (destination.Area == BuqiDeploymentArea.Board && span > 0)
            {
                for (int slot = destination.Index; slot < destination.Index + span && slot < BoardSlotCount; slot++)
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
            if (!TryPlanMove(source, target, out BuqiDeploymentSnapshot next, out _, out _, out _, out string reason))
                return Rejected(reason);

            View = next;
            return new BuqiDeploymentCommandResult(true, string.Empty, View);
        }

        public BuqiDeploymentCommandResult Reset()
        {
            View = m_OpeningView;
            return new BuqiDeploymentCommandResult(true, string.Empty, View);
        }

        private bool TryPlanMove(
            BuqiDeploymentSlotRef source,
            BuqiDeploymentSlotRef target,
            out BuqiDeploymentSnapshot next,
            out BuqiDeploymentSlotRef sourceDestination,
            out int span,
            out string itemId,
            out string reason)
        {
            next = null;
            sourceDestination = target;
            span = 0;
            itemId = string.Empty;
            reason = string.Empty;

            if (!IsValidSlot(source))
            {
                reason = "来源位置无效";
                return false;
            }
            if (!IsValidSlot(target))
            {
                reason = "目标位置无效";
                return false;
            }

            if (!TryResolveItem(source, out ResolvedItem sourceItem, out reason))
                return false;

            itemId = sourceItem.ItemId;
            span = sourceItem.Span;
            if (!TryResolveOptionalItem(target, out ResolvedItem targetItem, out reason))
                return false;

            if (targetItem != null)
                sourceDestination = targetItem.Location;

            if (targetItem != null && string.Equals(targetItem.ItemId, sourceItem.ItemId, StringComparison.Ordinal))
            {
                next = View;
                return true;
            }

            bool isSwap = targetItem != null;
            string[] boardSlots = CopySlots(View.BoardSlots);
            string[] storageSlots = CopySlots(View.StorageSlots);
            RemoveItem(boardSlots, storageSlots, sourceItem.ItemId);
            if (targetItem != null)
                RemoveItem(boardSlots, storageSlots, targetItem.ItemId);

            if (!TryPlaceItem(boardSlots, storageSlots, sourceItem, sourceDestination, isSwap, out reason))
                return false;
            if (targetItem != null &&
                !TryPlaceItem(boardSlots, storageSlots, targetItem, sourceItem.Location, true, out reason))
            {
                return false;
            }

            if (!TryBuildSnapshot(m_Catalog, boardSlots, storageSlots, out next, out reason))
            {
                if (isSwap && reason.Contains("超出棋盘"))
                    reason = "交换后装备超出棋盘范围";
                else if (isSwap && reason.Contains("重叠"))
                    reason = "交换后位置与其他装备重叠";
                return false;
            }

            return true;
        }

        private bool TryResolveItem(
            BuqiDeploymentSlotRef slot,
            out ResolvedItem item,
            out string reason)
        {
            if (!TryResolveOptionalItem(slot, out item, out reason))
                return false;
            if (item != null)
                return true;

            reason = "来源位置没有装备";
            return false;
        }

        private bool TryResolveOptionalItem(
            BuqiDeploymentSlotRef slot,
            out ResolvedItem item,
            out string reason)
        {
            item = null;
            reason = string.Empty;
            if (slot.Area == BuqiDeploymentArea.Board)
            {
                BuqiDeploymentPlacement placement = FindPlacement(slot.Index);
                if (placement == null)
                    return true;
                item = new ResolvedItem(
                    placement.ItemId,
                    placement.Span,
                    BuqiDeploymentSlotRef.Board(placement.AnchorSlot));
                return true;
            }

            string itemId = View.StorageSlots[slot.Index];
            if (string.IsNullOrEmpty(itemId))
                return true;
            BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(itemId);
            if (definition == null)
            {
                reason = "装备已不存在";
                return false;
            }
            item = new ResolvedItem(itemId, definition.Size, slot);
            return true;
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

        private static bool TryPlaceItem(
            string[] boardSlots,
            string[] storageSlots,
            ResolvedItem item,
            BuqiDeploymentSlotRef destination,
            bool isSwap,
            out string reason)
        {
            reason = string.Empty;
            if (destination.Area == BuqiDeploymentArea.Storage)
            {
                if (!string.IsNullOrEmpty(storageSlots[destination.Index]))
                {
                    reason = isSwap ? "交换后仓库位置已占用" : "目标仓库位置已占用";
                    return false;
                }

                storageSlots[destination.Index] = item.ItemId;
                return true;
            }

            if (destination.Index + item.Span > BoardSlotCount)
            {
                reason = isSwap ? "交换后装备超出棋盘范围" : "装备超出棋盘范围";
                return false;
            }

            for (int slot = destination.Index; slot < destination.Index + item.Span; slot++)
            {
                if (!string.IsNullOrEmpty(boardSlots[slot]))
                {
                    reason = isSwap ? "交换后位置与其他装备重叠" : "目标位置与其他装备重叠";
                    return false;
                }
            }
            for (int slot = destination.Index; slot < destination.Index + item.Span; slot++)
                boardSlots[slot] = item.ItemId;
            return true;
        }

        private static void RemoveItem(string[] boardSlots, string[] storageSlots, string itemId)
        {
            for (int slot = 0; slot < boardSlots.Length; slot++)
            {
                if (string.Equals(boardSlots[slot], itemId, StringComparison.Ordinal))
                    boardSlots[slot] = string.Empty;
            }
            for (int slot = 0; slot < storageSlots.Length; slot++)
            {
                if (string.Equals(storageSlots[slot], itemId, StringComparison.Ordinal))
                    storageSlots[slot] = string.Empty;
            }
        }

        private sealed class ResolvedItem
        {
            public ResolvedItem(string itemId, int span, BuqiDeploymentSlotRef location)
            {
                ItemId = itemId;
                Span = span;
                Location = location;
            }

            public string ItemId { get; }

            public int Span { get; }

            public BuqiDeploymentSlotRef Location { get; }
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
            if (slot.Index < 0)
                return false;
            if (slot.Area == BuqiDeploymentArea.Board)
                return slot.Index < BoardSlotCount;
            if (slot.Area == BuqiDeploymentArea.Storage)
                return slot.Index < StorageSlotCount;
            return false;
        }

        private BuqiDeploymentCommandResult Rejected(string reason)
        {
            return new BuqiDeploymentCommandResult(false, reason, View);
        }
    }
}
