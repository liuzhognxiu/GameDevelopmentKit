using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiDragDeployControllerTests
    {
        [Test]
        public void Create_NormalizesMultiSlotPlacementAndCopiesInput()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var board = Board();
            board[0] = "item-m";
            BuqiDragDeployController controller = BuqiDragDeployController.Create(catalog, board, Storage());
            board[0] = "item-s";

            Assert.That(controller.View.BoardSlots[0], Is.EqualTo("item-m"));
            Assert.That(controller.View.BoardSlots[1], Is.EqualTo("item-m"));
            Assert.That(controller.View.Placements.Single().Span, Is.EqualTo(2));
        }

        [Test]
        public void Preview_MultiSlotStorageToBoardReturnsEveryCoveredSlot()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-m", "item-s"));

            BuqiDeploymentTargetPreview preview = controller.Preview(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(3));

            Assert.That(preview.Accepted, Is.True);
            Assert.That(preview.BoardSlots, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_OccupiedContinuationSwapsAtomically()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-m", "item-s"));
            Assert.That(controller.TryMove(BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(1)).Accepted,
                Is.True);
            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(1), BuqiDeploymentSlotRef.Board(2));

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.BoardSlots[1], Is.EqualTo("item-s"));
            Assert.That(controller.View.BoardSlots[2], Is.Empty);
            Assert.That(controller.View.StorageSlots[1], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_OutOfRangeIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-l"));
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(8));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("装备超出棋盘范围"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void TryMove_UnknownAreaIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-s"));
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0),
                new BuqiDeploymentSlotRef((BuqiDeploymentArea)99, 1));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("目标位置无效"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void TryMove_BoardToBoardAndBoardToStorageAreAtomic()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board("item-m"), Storage());

            Assert.That(controller.TryMove(BuqiDeploymentSlotRef.Board(1), BuqiDeploymentSlotRef.Board(4)).Accepted,
                Is.True);
            Assert.That(controller.View.BoardSlots[0], Is.Empty);
            Assert.That(controller.View.BoardSlots[4], Is.EqualTo("item-m"));
            Assert.That(controller.View.BoardSlots[5], Is.EqualTo("item-m"));

            Assert.That(controller.TryMove(BuqiDeploymentSlotRef.Board(5), BuqiDeploymentSlotRef.Storage(2)).Accepted,
                Is.True);
            Assert.That(controller.View.BoardSlots[4], Is.Empty);
            Assert.That(controller.View.BoardSlots[5], Is.Empty);
            Assert.That(controller.View.StorageSlots[2], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_OccupiedStorageSlotsSwapAtomically()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-s", "item-m"));

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Storage(1));

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-m"));
            Assert.That(controller.View.StorageSlots[1], Is.EqualTo("item-s"));
        }

        [Test]
        public void TryMove_OccupiedBoardSlotsSwapAtomically()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(),
                Board("item-s", "", "", "item-m"),
                Storage());

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Board(0), BuqiDeploymentSlotRef.Board(3));

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.BoardSlots[0], Is.EqualTo("item-m"));
            Assert.That(controller.View.BoardSlots[1], Is.EqualTo("item-m"));
            Assert.That(controller.View.BoardSlots[3], Is.EqualTo("item-s"));
            Assert.That(controller.View.BoardSlots[4], Is.Empty);
        }

        [Test]
        public void TryMove_OccupiedBoardAndStorageSlotsSwapAtomically()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(),
                Board("", "", "", "item-m"),
                Storage("item-s"));

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(3));

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.BoardSlots[3], Is.EqualTo("item-s"));
            Assert.That(controller.View.BoardSlots[4], Is.Empty);
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_IllegalOccupiedSwapIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(),
                Board("", "", "", "", "", "", "", "", "item-s"),
                Storage("item-l"));
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Board(8), BuqiDeploymentSlotRef.Storage(0));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("交换后装备超出棋盘范围"));
            Assert.That(controller.View, Is.SameAs(before));
            Assert.That(controller.View.BoardSlots[8], Is.EqualTo("item-s"));
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-l"));
        }

        [Test]
        public void Reset_RestoresOpeningSnapshotInstance()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Board(), Storage("item-m"));
            BuqiDeploymentSnapshot opening = controller.View;
            Assert.That(controller.TryMove(BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(2)).Accepted,
                Is.True);

            BuqiDeploymentCommandResult result = controller.Reset();

            Assert.That(result.Accepted, Is.True);
            Assert.That(controller.View, Is.SameAs(opening));
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_EmptyOrStaleSourceIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(CreateCatalog());
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(0));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("来源位置没有装备"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void Snapshot_PublicSlotConstructorCopiesPayload()
        {
            var signature = new[]
            {
                typeof(IReadOnlyList<string>),
                typeof(IReadOnlyList<string>),
            };
            System.Reflection.ConstructorInfo constructor = typeof(BuqiDeploymentSnapshot).GetConstructor(signature);
            Assert.That(constructor, Is.Not.Null);
            var board = Board();
            var storage = Storage();
            board[0] = "item-s";

            var snapshot = (BuqiDeploymentSnapshot)constructor.Invoke(new object[] { board, storage });
            board[0] = "changed";

            Assert.That(snapshot.BoardSlots[0], Is.EqualTo("item-s"));
            Assert.That(snapshot.StorageSlots.Count, Is.EqualTo(BuqiDragDeployController.StorageSlotCount));
        }

        private static BuqiUIDemoCatalog CreateCatalog()
        {
            var catalog = new BuqiUIDemoCatalog();
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-s", Name = "短刃", Size = 1 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-m", Name = "中阵", Size = 2 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-l", Name = "长阵", Size = 3 });
            return catalog;
        }

        private static List<string> Slots(int count)
        {
            var slots = new List<string>(count);
            for (int index = 0; index < count; index++)
                slots.Add(string.Empty);
            return slots;
        }

        private static List<string> Board(params string[] itemIds)
        {
            return FixedSlots(BuqiDragDeployController.BoardSlotCount, itemIds);
        }

        private static List<string> Storage(params string[] itemIds)
        {
            return FixedSlots(BuqiDragDeployController.StorageSlotCount, itemIds);
        }

        private static List<string> FixedSlots(int count, IReadOnlyList<string> itemIds)
        {
            List<string> slots = Slots(count);
            for (int index = 0; index < itemIds.Count; index++)
                slots[index] = itemIds[index] ?? string.Empty;
            return slots;
        }
    }
}
