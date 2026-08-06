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
            var board = Slots(8);
            board[0] = "item-m";
            BuqiDragDeployController controller = BuqiDragDeployController.Create(catalog, board, Slots(5));
            board[0] = "item-s";

            Assert.That(controller.View.BoardSlots[0], Is.EqualTo("item-m"));
            Assert.That(controller.View.BoardSlots[1], Is.EqualTo("item-m"));
            Assert.That(controller.View.Placements.Single().Span, Is.EqualTo(2));
        }

        [Test]
        public void Preview_MultiSlotStorageToBoardReturnsEveryCoveredSlot()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Slots(8), new List<string> { "item-m", "item-s", "", "", "" });

            BuqiDeploymentTargetPreview preview = controller.Preview(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(3));

            Assert.That(preview.Accepted, Is.True);
            Assert.That(preview.BoardSlots, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(controller.View.StorageSlots[0], Is.EqualTo("item-m"));
        }

        [Test]
        public void TryMove_OverlapIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Slots(8), new List<string> { "item-m", "item-s", "", "", "" });
            Assert.That(controller.TryMove(BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(1)).Accepted,
                Is.True);
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(1), BuqiDeploymentSlotRef.Board(2));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("目标位置与其他装备重叠"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void TryMove_OutOfRangeIsRejectedWithoutMutation()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Slots(8), new List<string> { "item-l", "", "", "", "" });
            BuqiDeploymentSnapshot before = controller.View;

            BuqiDeploymentCommandResult result = controller.TryMove(
                BuqiDeploymentSlotRef.Storage(0), BuqiDeploymentSlotRef.Board(6));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("装备超出棋盘范围"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void TryMove_BoardToBoardAndBoardToStorageAreAtomic()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), new List<string> { "item-m", "", "", "", "", "", "", "" }, Slots(5));

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
        public void Reset_RestoresOpeningSnapshotInstance()
        {
            BuqiDragDeployController controller = BuqiDragDeployController.Create(
                CreateCatalog(), Slots(8), new List<string> { "item-m", "", "", "", "" });
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
            var board = Slots(8);
            var storage = Slots(5);
            board[0] = "item-s";

            var snapshot = (BuqiDeploymentSnapshot)constructor.Invoke(new object[] { board, storage });
            board[0] = "changed";

            Assert.That(snapshot.BoardSlots[0], Is.EqualTo("item-s"));
            Assert.That(snapshot.StorageSlots.Count, Is.EqualTo(5));
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
    }
}
