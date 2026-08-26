using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public static class BuqiLinkTopology
    {
        public static BuqiLinkItem GetAdjacent(
            BuqiLinkBoard board,
            BuqiLinkItem source,
            BuqiLinkDirection direction)
        {
            if (board == null || source == null || direction == BuqiLinkDirection.AnyAdjacent)
                return null;

            int targetSlot = direction == BuqiLinkDirection.Clockwise
                ? source.AnchorSlot + source.Size
                : source.AnchorSlot - 1;
            if (targetSlot < 0 || targetSlot >= BuqiLinkBoard.SlotCount)
                return null;
            foreach (BuqiLinkItem item in board.Items)
            {
                if (item == source)
                    continue;
                if (targetSlot >= item.AnchorSlot && targetSlot < item.AnchorSlot + item.Size)
                    return item;
            }
            return null;
        }

        public static IReadOnlyList<BuqiLinkItem> GetAllAdjacent(BuqiLinkBoard board, BuqiLinkItem source)
        {
            var result = new List<BuqiLinkItem>(2);
            BuqiLinkItem clockwise = GetAdjacent(board, source, BuqiLinkDirection.Clockwise);
            BuqiLinkItem counterClockwise = GetAdjacent(board, source, BuqiLinkDirection.CounterClockwise);
            if (clockwise != null)
                result.Add(clockwise);
            if (counterClockwise != null && counterClockwise != clockwise)
                result.Add(counterClockwise);
            return result;
        }
    }
}
