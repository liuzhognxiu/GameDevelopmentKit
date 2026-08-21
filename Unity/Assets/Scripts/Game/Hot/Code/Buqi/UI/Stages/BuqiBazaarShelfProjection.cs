using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    /// <summary>
    /// UI-only compatibility boundary for the supply contract. The runtime supply owns
    /// shelf generation; this projection only normalizes anchor/span data for rendering.
    /// </summary>
    public static class BuqiBazaarShelfProjection
    {
        public const int ShelfSlotCount = BuqiBazaarSupplyView.DefaultShelfSlotCount;

        public static IReadOnlyList<BuqiDemoOfferView> Project(
            IReadOnlyList<BuqiDemoOfferView> offers,
            int shelfSlotCount = ShelfSlotCount,
            IReadOnlyDictionary<string, int> suppliedAnchors = null)
        {
            int capacity = shelfSlotCount > 0 ? shelfSlotCount : ShelfSlotCount;
            var result = new List<BuqiDemoOfferView>(offers?.Count ?? 0);
            var occupied = new bool[capacity];
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (offers == null)
                return result;

            foreach (BuqiDemoOfferView source in offers)
            {
                if (source == null || string.IsNullOrEmpty(source.Id) || !ids.Add(source.Id))
                    continue;

                int span = source.Span > 0 ? source.Span : source.Item?.Size ?? 0;
                span = Math.Max(1, Math.Min(capacity, span));
                int anchor = source.AnchorSlot;
                if (suppliedAnchors != null && suppliedAnchors.TryGetValue(source.Id, out int suppliedAnchor))
                    anchor = suppliedAnchor;
                if (!CanPlace(occupied, anchor, span))
                    anchor = FindFirstFit(occupied, span);

                BuqiDemoOfferView projected = Clone(source);
                projected.AnchorSlot = anchor;
                projected.Span = span;
                if (anchor >= 0)
                {
                    for (int slot = anchor; slot < anchor + span; slot++)
                        occupied[slot] = true;
                }
                result.Add(projected);
            }

            return result;
        }

        private static bool CanPlace(bool[] occupied, int anchor, int span)
        {
            if (anchor < 0 || anchor + span > occupied.Length)
                return false;
            for (int slot = anchor; slot < anchor + span; slot++)
            {
                if (occupied[slot])
                    return false;
            }
            return true;
        }

        private static int FindFirstFit(bool[] occupied, int span)
        {
            for (int anchor = 0; anchor + span <= occupied.Length; anchor++)
            {
                if (CanPlace(occupied, anchor, span))
                    return anchor;
            }
            return -1;
        }

        private static BuqiDemoOfferView Clone(BuqiDemoOfferView source)
        {
            BuqiDemoItemView item = source.Item;
            return new BuqiDemoOfferView
            {
                Id = source.Id,
                Item = item == null ? null : new BuqiDemoItemView
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Size = item.Size,
                    Price = item.Price,
                    SellPrice = item.SellPrice,
                    CooldownTicks = item.CooldownTicks,
                    EffectDescription = item.EffectDescription,
                    Quality = item.Quality,
                    ArchetypeId = item.ArchetypeId,
                    Role = item.Role,
                    PositionHint = item.PositionHint,
                    UpgradeSummary = item.UpgradeSummary,
                    Tags = item.Tags == null ? new List<string>() : new List<string>(item.Tags),
                    Empty = item.Empty,
                    Selected = item.Selected,
                    Locked = item.Locked,
                    Slot = item.Slot,
                    AnchorSlot = item.AnchorSlot,
                },
                Price = source.Price,
                AnchorSlot = source.AnchorSlot,
                Span = source.Span,
                Sold = source.Sold,
                Locked = source.Locked,
            };
        }
    }
}
