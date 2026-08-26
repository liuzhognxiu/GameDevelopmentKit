using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.BattleLab
{
    /// <summary>
    /// 配置尺寸棋盘的原子修改边界。失败操作不会发布新的视图。
    /// </summary>
    public sealed class BuqiBattleLabBoard
    {
        private readonly int m_SlotCount;
        private List<BuqiBattleLabPlacement> m_Placements;
        private BuqiBattleLabBoardView m_View;

        public BuqiBattleLabBoard(int slotCount)
        {
            if (slotCount < 8 || slotCount > 10)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            m_SlotCount = slotCount;
            m_Placements = new List<BuqiBattleLabPlacement>();
            m_View = CreateView(m_Placements);
        }

        public BuqiBattleLabBoardView View => m_View;

        public BuqiBattleLabPlacementPreview Preview(
            string definitionId,
            int size,
            int anchorSlot,
            string ignoredInstanceId)
        {
            var placement = new BuqiBattleLabPlacement(
                ignoredInstanceId ?? string.Empty,
                definitionId,
                definitionId,
                size,
                default,
                anchorSlot,
                string.Empty);
            bool accepted = TryValidatePlacement(
                m_Placements,
                placement,
                ignoredInstanceId,
                out string reason);

            return new BuqiBattleLabPlacementPreview(
                BuqiBattleLabSide.Player,
                anchorSlot,
                size,
                CreateCoveredSlots(anchorSlot, size),
                accepted,
                reason);
        }

        public bool TryAdd(BuqiBattleLabPlacement placement, out string reason)
        {
            if (placement != null && string.IsNullOrEmpty(placement.InstanceId))
            {
                reason = "实例标识不可用";
                return false;
            }

            if (placement != null && string.IsNullOrEmpty(placement.DefinitionId))
            {
                reason = "道具定义不可用";
                return false;
            }

            var candidate = new List<BuqiBattleLabPlacement>(m_Placements);
            if (!TryValidatePlacement(candidate, placement, string.Empty, out reason))
                return false;

            candidate.Add(placement);
            Commit(candidate);
            return true;
        }

        public bool TryMove(string instanceId, int anchorSlot, out string reason)
        {
            int sourceIndex = FindPlacementIndex(instanceId);
            if (sourceIndex < 0)
            {
                reason = "来源位置没有道具";
                return false;
            }

            var candidate = new List<BuqiBattleLabPlacement>(m_Placements);
            BuqiBattleLabPlacement source = candidate[sourceIndex];
            var moved = new BuqiBattleLabPlacement(
                source.InstanceId,
                source.DefinitionId,
                source.DisplayName,
                source.Size,
                source.Quality,
                anchorSlot,
                source.AnnotationId);
            if (!TryValidatePlacement(candidate, moved, instanceId, out reason))
                return false;

            candidate[sourceIndex] = moved;
            Commit(candidate);
            return true;
        }

        public bool TryRemove(string instanceId, out string reason)
        {
            int sourceIndex = FindPlacementIndex(instanceId);
            if (sourceIndex < 0)
            {
                reason = "来源位置没有道具";
                return false;
            }

            var candidate = new List<BuqiBattleLabPlacement>(m_Placements);
            candidate.RemoveAt(sourceIndex);
            Commit(candidate);
            reason = string.Empty;
            return true;
        }

        public bool Clear()
        {
            if (m_Placements.Count == 0)
                return false;

            Commit(new List<BuqiBattleLabPlacement>());
            return true;
        }

        public IReadOnlyList<BuqiBattleLabPlacement> CopyPlacements()
        {
            return Array.AsReadOnly(m_Placements.ToArray());
        }

        private bool TryValidatePlacement(
            IReadOnlyList<BuqiBattleLabPlacement> candidate,
            BuqiBattleLabPlacement placement,
            string ignoredInstanceId,
            out string reason)
        {
            if (placement == null)
            {
                reason = "道具尺寸必须为 1 至 3 格";
                return false;
            }

            if (placement.Size < 1 || placement.Size > 3)
            {
                reason = "道具尺寸必须为 1 至 3 格";
                return false;
            }

            if (placement.AnchorSlot < 0 || placement.AnchorSlot >= m_SlotCount)
            {
                reason = "目标位置无效";
                return false;
            }

            if (placement.AnchorSlot + placement.Size > m_SlotCount)
            {
                reason = $"需要连续 {placement.Size} 格";
                return false;
            }

            for (int index = 0; index < candidate.Count; index++)
            {
                BuqiBattleLabPlacement existing = candidate[index];
                if (IsIgnored(existing, ignoredInstanceId))
                    continue;
                if (string.Equals(
                        existing.InstanceId,
                        placement.InstanceId,
                        StringComparison.Ordinal))
                {
                    reason = "同一实例不能重复放置";
                    return false;
                }
            }

            for (int index = 0; index < candidate.Count; index++)
            {
                BuqiBattleLabPlacement existing = candidate[index];
                if (IsIgnored(existing, ignoredInstanceId))
                    continue;
                if (RangesOverlap(existing, placement))
                {
                    reason = $"与{existing.DisplayName}重叠";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private int FindPlacementIndex(string instanceId)
        {
            for (int index = 0; index < m_Placements.Count; index++)
            {
                if (string.Equals(
                        m_Placements[index].InstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        private void Commit(List<BuqiBattleLabPlacement> placements)
        {
            m_Placements = placements;
            m_View = CreateView(m_Placements);
        }

        private BuqiBattleLabBoardView CreateView(
            IReadOnlyList<BuqiBattleLabPlacement> placements)
        {
            var occupiedInstanceIds = new string[m_SlotCount];
            for (int placementIndex = 0;
                 placementIndex < placements.Count;
                 placementIndex++)
            {
                BuqiBattleLabPlacement placement = placements[placementIndex];
                for (int offset = 0; offset < placement.Size; offset++)
                    occupiedInstanceIds[placement.AnchorSlot + offset] = placement.InstanceId;
            }

            return new BuqiBattleLabBoardView(
                m_SlotCount,
                placements,
                occupiedInstanceIds);
        }

        private IReadOnlyList<int> CreateCoveredSlots(int anchorSlot, int size)
        {
            if (size < 1 || size > 3)
                return Array.AsReadOnly(Array.Empty<int>());

            var coveredSlots = new List<int>(size);
            for (int offset = 0; offset < size; offset++)
            {
                int slot = anchorSlot + offset;
                if (slot >= 0 && slot < m_SlotCount)
                    coveredSlots.Add(slot);
            }
            return Array.AsReadOnly(coveredSlots.ToArray());
        }

        private static bool IsIgnored(
            BuqiBattleLabPlacement placement,
            string ignoredInstanceId)
        {
            return !string.IsNullOrEmpty(ignoredInstanceId) &&
                   string.Equals(
                       placement.InstanceId,
                       ignoredInstanceId,
                       StringComparison.Ordinal);
        }

        private static bool RangesOverlap(
            BuqiBattleLabPlacement left,
            BuqiBattleLabPlacement right)
        {
            int leftEnd = left.AnchorSlot + left.Size;
            int rightEnd = right.AnchorSlot + right.Size;
            return left.AnchorSlot < rightEnd && right.AnchorSlot < leftEnd;
        }
    }
}
