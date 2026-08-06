using System;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
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
            Assert.That(result.Reason, Is.EqualTo("当前阶段不能选择该项"));
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
            Assert.That(second.Reason, Is.EqualTo("预测已经提交"));
            Assert.That(controller.View, Is.SameAs(lockedView));
            Assert.That(controller.View.Prediction, Is.EqualTo("Win"));
        }

        [TestCase("OpenDragDeploy")]
        [TestCase("ApplyDeployment")]
        public void CommandType_ContainsDeploymentCommands(string commandName)
        {
            Assert.That(Enum.GetNames(typeof(BuqiUIDemoCommandType)), Contains.Item(commandName));
        }

        [Test]
        public void Command_ContainsTypedDeploymentPayload()
        {
            Assert.That(typeof(BuqiUIDemoCommand).GetField("Deployment")?.FieldType,
                Is.EqualTo(typeof(BuqiDeploymentSnapshot)));
        }

        [Test]
        public void OpenDragDeploy_IsAcceptedOnlyInBoardEditorWithoutMutation()
        {
            BuqiUIDemoController controller = CreateController();
            BuqiUIDemoView starter = controller.View;

            BuqiUIDemoCommandResult rejected = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });

            Assert.That(rejected.Accepted, Is.False);
            Assert.That(controller.View, Is.SameAs(starter));

            AdvanceTo(controller, BuqiUIDemoPhase.BoardEditor);
            BuqiUIDemoView boardEditor = controller.View;
            BuqiUIDemoCommandResult accepted = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });

            Assert.That(accepted.Accepted, Is.True, accepted.Reason);
            Assert.That(controller.View, Is.SameAs(boardEditor));
        }

        [Test]
        public void ApplyDeployment_ValidOwnedSnapshotReplacesBoardAtomically()
        {
            BuqiUIDemoController controller = CreateController();
            AdvanceTo(controller, BuqiUIDemoPhase.BoardEditor);
            BuqiUIDemoView before = controller.View;
            var board = Enumerable.Repeat(string.Empty, 8).ToList();
            board[3] = "item-01";

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(
                    board,
                    Enumerable.Repeat(string.Empty, 5).ToList()),
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View, Is.Not.SameAs(before));
            Assert.That(controller.View.BoardSlots[0].Empty, Is.True);
            Assert.That(controller.View.BoardSlots[3].Id, Is.EqualTo("item-01"));
            Assert.That(controller.View.BoardSlots[4].Id, Is.EqualTo("item-01"));
        }

        [Test]
        public void ApplyDeployment_InvalidSnapshotsAreRejectedWithoutMutation()
        {
            BuqiUIDemoController controller = CreateController();
            AdvanceTo(controller, BuqiUIDemoPhase.BoardEditor);
            BuqiUIDemoView before = controller.View;

            var malformedBoard = Enumerable.Repeat(string.Empty, 7).ToList();
            var overlapBoard = Enumerable.Repeat(string.Empty, 8).ToList();
            overlapBoard[0] = "item-01";
            overlapBoard[1] = "item-02";
            var unknownBoard = Enumerable.Repeat(string.Empty, 8).ToList();
            unknownBoard[0] = "missing-item";
            var addedStorage = Enumerable.Repeat(string.Empty, 5).ToList();
            addedStorage[0] = "item-02";
            var currentBoard = Enumerable.Repeat(string.Empty, 8).ToList();
            currentBoard[0] = "item-01";

            BuqiDeploymentSnapshot[] invalid =
            {
                null,
                new BuqiDeploymentSnapshot(malformedBoard, Enumerable.Repeat(string.Empty, 5).ToList()),
                new BuqiDeploymentSnapshot(overlapBoard, Enumerable.Repeat(string.Empty, 5).ToList()),
                new BuqiDeploymentSnapshot(unknownBoard, Enumerable.Repeat(string.Empty, 5).ToList()),
                new BuqiDeploymentSnapshot(currentBoard, addedStorage),
            };
            string[] expectedReasons =
            {
                "\u90E8\u7F72\u5FEB\u7167\u4E0D\u53EF\u7528",
                "\u68CB\u76D8\u4F4D\u7F6E\u6570\u91CF\u65E0\u6548",
                "\u68CB\u76D8\u4E0A\u5B58\u5728\u91CD\u53E0\u88C5\u5907",
                "\u88C5\u5907\u5DF2\u4E0D\u5B58\u5728",
                "\u90E8\u7F72\u5FEB\u7167\u4E0E\u5F53\u524D\u88C5\u5907\u4E0D\u4E00\u81F4",
            };

            for (int index = 0; index < invalid.Length; index++)
            {
                BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.ApplyDeployment,
                    Deployment = invalid[index],
                });

                Assert.That(result.Accepted, Is.False);
                Assert.That(result.Reason, Is.EqualTo(expectedReasons[index]));
                Assert.That(controller.View, Is.SameAs(before));
            }
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
                    DisplayName = $"装备 {index}",
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
                    DisplayName = $"改造 {index}",
                    Summary = $"改造效果 {index}",
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
                DisplayName = "对手快照",
                Build = "demo",
                Snapshot = snapshot,
            });
            return catalog;
        }
    }
}
