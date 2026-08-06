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
            Assert.That(result.Reason, Is.EqualTo("\u76EE\u6807\u4F4D\u7F6E\u4E0E\u5176\u4ED6\u88C5\u5907\u91CD\u53E0"));
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
            Assert.That(result.Reason, Is.EqualTo("\u88C5\u5907\u8D85\u51FA\u68CB\u76D8\u8303\u56F4"));
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
            Assert.That(result.Reason, Is.EqualTo("\u6765\u6E90\u4F4D\u7F6E\u6CA1\u6709\u88C5\u5907"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        private static BuqiUIDemoCatalog CreateCatalog()
        {
            var catalog = new BuqiUIDemoCatalog();
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-s", Name = "\u77ED\u5203", Size = 1 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-m", Name = "\u4E2D\u9635", Size = 2 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-l", Name = "\u957F\u9635", Size = 3 });
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
