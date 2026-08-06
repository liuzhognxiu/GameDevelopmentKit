using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiUIDemoControllerTests
    {
        [Test]
        public void Create_AlwaysReturnsSameStarterState()
        {
            BuqiUIDemoCatalog catalog = CreateDemoCatalog();

            BuqiUIDemoController first = BuqiUIDemoController.Create(catalog);
            BuqiUIDemoController second = BuqiUIDemoController.Create(catalog);

            Assert.That(first.View.Phase, Is.EqualTo(BuqiUIDemoPhase.StarterSelection));
            Assert.That(first.View.Coins, Is.EqualTo(second.View.Coins));
            Assert.That(first.View.Choices.Select(choice => choice.Id),
                Is.EqualTo(second.View.Choices.Select(choice => choice.Id)));
            Assert.That(first.View.BoardSlots.Count, Is.EqualTo(8));
            Assert.That(first.View.StorageSlots.Count, Is.EqualTo(5));
        }

        [Test]
        public void SelectChoice_OutsideChoicePhaseIsRejectedWithoutMutation()
        {
            BuqiUIDemoController controller = CreateController();
            BuqiUIDemoView before = controller.View;

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectChoice,
                PrimaryId = "prepare-coin",
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u9009\u62E9\u8BE5\u9879"));
            Assert.That(controller.View, Is.SameAs(before));
        }

        [Test]
        public void SubmitPrediction_AfterPredictionIsLockedIsRejected()
        {
            BuqiUIDemoController controller = CreateController();
            AdvanceTo(controller, BuqiUIDemoPhase.Prediction);

            BuqiUIDemoCommandResult first = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SubmitPrediction,
                PrimaryId = "Win",
            });
            BuqiUIDemoView lockedView = controller.View;
            BuqiUIDemoCommandResult second = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SubmitPrediction,
                PrimaryId = "Lose",
            });

            Assert.That(first.Accepted, Is.True);
            Assert.That(second.Accepted, Is.False);
            Assert.That(second.Reason, Is.EqualTo("\u9884\u6D4B\u5DF2\u7ECF\u63D0\u4EA4"));
            Assert.That(controller.View, Is.SameAs(lockedView));
            Assert.That(controller.View.Prediction, Is.EqualTo("Win"));
        }

        private static BuqiUIDemoController CreateController()
        {
            return BuqiUIDemoController.Create(CreateDemoCatalog());
        }

        private static void AdvanceTo(BuqiUIDemoController controller, BuqiUIDemoPhase target)
        {
            if (controller.View.Phase == BuqiUIDemoPhase.StarterSelection)
            {
                Assert.That(controller.Execute(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectStarter,
                    PrimaryId = controller.View.Choices[0].Id,
                }).Accepted, Is.True);
            }

            while (controller.View.Phase < target)
            {
                BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.NextPhase,
                });
                Assert.That(result.Accepted, Is.True, result.Reason);
            }
        }

        private static BuqiUIDemoCatalog CreateDemoCatalog()
        {
            BuqiConfigCatalog source = CreateSourceCatalog();
            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error),
                Is.True, error);
            return catalog;
        }

        private static BuqiConfigCatalog CreateSourceCatalog()
        {
            var catalog = new BuqiConfigCatalog();
            for (int index = 1; index <= 7; index++)
            {
                catalog.Items.Add(new BuqiItemConfigRow
                {
                    DefinitionId = $"item-{index:00}",
                    DisplayName = $"\u88C5\u5907 {index}",
                    Size = index == 1
                        ? Game.Hot.Buqi.Battle.BuqiSize.M
                        : Game.Hot.Buqi.Battle.BuqiSize.S,
                    BasePrice = index + 1,
                    BaseCooldownTicks = 20 + index,
                });
            }

            for (int index = 1; index <= 3; index++)
            {
                catalog.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"mod-{index:00}",
                    DisplayName = $"\u6539\u9020 {index}",
                    Summary = $"\u6539\u9020\u6548\u679C {index}",
                });
            }

            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = "snapshot-01",
                ArchetypeId = "demo",
            };
            snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = "opponent-item-01",
                DefinitionId = "item-01",
                AnchorSlot = 0,
            });
            catalog.Echoes.Add(new BuqiEchoConfigRow
            {
                EchoId = "echo-01",
                DisplayName = "\u5BF9\u624B\u5FEB\u7167",
                Build = "demo",
                Snapshot = snapshot,
            });
            return catalog;
        }
    }
}
