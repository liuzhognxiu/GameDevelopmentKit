using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>一次目标解析的阵营与法门实例集合。</summary>
    public sealed class ResolvedTargets
    {
        /// <summary>目标所属阵营；对执行值等阵营目标可只设置此字段。</summary>
        public SideState Side;

        /// <summary>具体法门目标；阵营目标可为空。</summary>
        public List<ItemState> Items = new List<ItemState>();
    }

    /// <summary>
    /// 战斗契约规定的九类目标选择器。
    /// 所有并列选择都以锚点和实例 ID 打破平局，保证跨端确定性。
    /// </summary>
    public static class BuqiTargeting
    {
        /// <summary>将固定棋盘格映射为法门索引，空格使用 -1。</summary>
        public static int[] BuildSlotOwner(SideState side)
        {
            var owner = new int[BuqiBoardValidator.BoardSlotCount];
            for (int index = 0; index < owner.Length; index++)
                owner[index] = -1;

            for (int itemIndex = 0; itemIndex < side.Items.Count; itemIndex++)
            {
                ItemState item = side.Items[itemIndex];
                int endSlot = item.AnchorSlot + item.Size - 1;
                for (int slot = item.AnchorSlot; slot <= endSlot && slot < owner.Length; slot++)
                    owner[slot] = itemIndex;
            }

            return owner;
        }

        /// <summary>
        /// 返回来源法门边界外紧贴的左侧或右侧法门。
        /// 契约明确规定空格会阻断相邻关系，因此不得跨越空格继续搜索下一张法门。
        /// </summary>
        public static ItemState GetAdjacent(SideState side, ItemState source, bool left)
        {
            int[] owner = BuildSlotOwner(side);
            int slot = left ? source.AnchorSlot - 1 : source.AnchorSlot + source.Size;
            // 目标格为空即没有相邻法门；这里故意不使用 while 跳过空格。
            if (slot < 0 || slot >= owner.Length || owner[slot] < 0)
                return null;
            return side.Items[owner[slot]];
        }

        public static List<ItemState> GetAllAdjacent(SideState side, ItemState source)
        {
            var result = new List<ItemState>();
            ItemState left = GetAdjacent(side, source, true);
            ItemState right = GetAdjacent(side, source, false);
            if (left != null)
                result.Add(left);
            if (right != null && right != left)
                result.Add(right);
            return result;
        }

        /// <summary>
        /// 按当前冷却进度选择最短或最长目标；相同进度时按锚点、实例 ID 稳定决胜。
        /// </summary>
        public static ItemState GetByCooldown(SideState side, bool shortest)
        {
            ItemState best = null;
            foreach (ItemState item in side.Items)
            {
                if (best == null || IsBetterCooldownTarget(item, best, shortest))
                    best = item;
            }
            return best;
        }

        public static ItemState GetByAnchor(SideState side, bool leftmost)
        {
            ItemState best = null;
            foreach (ItemState item in side.Items)
            {
                if (best == null || IsBetterAnchorTarget(item, best, leftmost))
                    best = item;
            }
            return best;
        }

        /// <summary>
        /// 将目标枚举解析为阵营目标或具体法门目标；无合法目标时返回空集合，由模拟器记录 NoTarget。
        /// </summary>
        public static ResolvedTargets Resolve(
            BuqiTarget target,
            SideState own,
            SideState enemy,
            ItemState source)
        {
            var result = new ResolvedTargets();
            ItemState item;
            switch (target)
            {
                case BuqiTarget.EnemyExecution:
                    result.Side = enemy;
                    break;
                case BuqiTarget.Self:
                    result.Side = own;
                    result.Items.Add(source);
                    break;
                case BuqiTarget.LeftAdjacentItem:
                    item = GetAdjacent(own, source, true);
                    AddItemTarget(result, own, item);
                    break;
                case BuqiTarget.RightAdjacentItem:
                    item = GetAdjacent(own, source, false);
                    AddItemTarget(result, own, item);
                    break;
                case BuqiTarget.AllAdjacentItems:
                    result.Items.AddRange(GetAllAdjacent(own, source));
                    if (result.Items.Count > 0)
                        result.Side = own;
                    break;
                case BuqiTarget.ShortestCooldownEnemyItem:
                    AddItemTarget(result, enemy, GetByCooldown(enemy, true));
                    break;
                case BuqiTarget.LongestCooldownEnemyItem:
                    AddItemTarget(result, enemy, GetByCooldown(enemy, false));
                    break;
                case BuqiTarget.LeftmostEnemyItem:
                    AddItemTarget(result, enemy, GetByAnchor(enemy, true));
                    break;
                case BuqiTarget.RightmostEnemyItem:
                    AddItemTarget(result, enemy, GetByAnchor(enemy, false));
                    break;
            }
            return result;
        }

        private static void AddItemTarget(ResolvedTargets result, SideState side, ItemState item)
        {
            if (item == null)
                return;
            result.Side = side;
            result.Items.Add(item);
        }

        private static bool IsBetterCooldownTarget(ItemState candidate, ItemState current, bool shortest)
        {
            if (candidate.CooldownProgress != current.CooldownProgress)
            {
                return shortest
                    ? candidate.CooldownProgress < current.CooldownProgress
                    : candidate.CooldownProgress > current.CooldownProgress;
            }
            return IsBetterAnchorTarget(candidate, current, true);
        }

        private static bool IsBetterAnchorTarget(ItemState candidate, ItemState current, bool leftmost)
        {
            if (candidate.AnchorSlot != current.AnchorSlot)
                return leftmost ? candidate.AnchorSlot < current.AnchorSlot : candidate.AnchorSlot > current.AnchorSlot;
            return string.CompareOrdinal(candidate.InstanceId, current.InstanceId) < 0;
        }
    }
}
