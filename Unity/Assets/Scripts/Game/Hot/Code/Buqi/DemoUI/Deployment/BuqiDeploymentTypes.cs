using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.DemoUI.Deployment
{
    public enum BuqiDeploymentArea
    {
        Board,
        Storage,
    }

    public readonly struct BuqiDeploymentSlotRef : IEquatable<BuqiDeploymentSlotRef>
    {
        public BuqiDeploymentSlotRef(BuqiDeploymentArea area, int index)
        {
            Area = area;
            Index = index;
        }

        public BuqiDeploymentArea Area { get; }
        public int Index { get; }

        public static BuqiDeploymentSlotRef Board(int index)
        {
            return new BuqiDeploymentSlotRef(BuqiDeploymentArea.Board, index);
        }

        public static BuqiDeploymentSlotRef Storage(int index)
        {
            return new BuqiDeploymentSlotRef(BuqiDeploymentArea.Storage, index);
        }

        public bool Equals(BuqiDeploymentSlotRef other)
        {
            return Area == other.Area && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is BuqiDeploymentSlotRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Area * 397) ^ Index;
        }

        public static bool operator ==(BuqiDeploymentSlotRef left, BuqiDeploymentSlotRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BuqiDeploymentSlotRef left, BuqiDeploymentSlotRef right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class BuqiDeploymentPlacement
    {
        public BuqiDeploymentPlacement(string itemId, int anchorSlot, int span)
        {
            ItemId = itemId ?? string.Empty;
            AnchorSlot = anchorSlot;
            Span = span;
        }

        public string ItemId { get; }
        public int AnchorSlot { get; }
        public int Span { get; }
    }

    public sealed class BuqiDeploymentSnapshot
    {
        public BuqiDeploymentSnapshot(
            IReadOnlyList<string> boardSlots,
            IReadOnlyList<string> storageSlots)
            : this(CopySlots(boardSlots), CopySlots(storageSlots), Array.Empty<BuqiDeploymentPlacement>())
        {
        }

        internal BuqiDeploymentSnapshot(
            IReadOnlyList<string> boardSlots,
            IReadOnlyList<string> storageSlots,
            IReadOnlyList<BuqiDeploymentPlacement> placements)
        {
            BoardSlots = boardSlots;
            StorageSlots = storageSlots;
            Placements = placements;
        }

        public IReadOnlyList<string> BoardSlots { get; }
        public IReadOnlyList<string> StorageSlots { get; }
        public IReadOnlyList<BuqiDeploymentPlacement> Placements { get; }

        private static IReadOnlyList<string> CopySlots(IReadOnlyList<string> slots)
        {
            if (slots == null)
                return Array.Empty<string>();
            var copy = new string[slots.Count];
            for (int index = 0; index < slots.Count; index++)
                copy[index] = slots[index] ?? string.Empty;
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class BuqiDeploymentTargetPreview
    {
        internal BuqiDeploymentTargetPreview(
            BuqiDeploymentSlotRef source,
            BuqiDeploymentSlotRef target,
            string itemId,
            int span,
            IReadOnlyList<int> boardSlots,
            bool accepted,
            string reason)
        {
            Source = source;
            Target = target;
            ItemId = itemId ?? string.Empty;
            Span = span;
            BoardSlots = boardSlots;
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public BuqiDeploymentSlotRef Source { get; }
        public BuqiDeploymentSlotRef Target { get; }
        public string ItemId { get; }
        public int Span { get; }
        public IReadOnlyList<int> BoardSlots { get; }
        public bool Accepted { get; }
        public string Reason { get; }
    }

    public sealed class BuqiDeploymentCommandResult
    {
        internal BuqiDeploymentCommandResult(bool accepted, string reason, BuqiDeploymentSnapshot view)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            View = view;
        }

        public bool Accepted { get; }
        public string Reason { get; }
        public BuqiDeploymentSnapshot View { get; }
    }
}
