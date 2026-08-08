using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.UI.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiPveDifficultySelectionTests
    {
        [Test]
        public void TryGetOrCreate_FreezesThreeDeterministicDifficultyCards()
        {
            BuqiLocalOpponentPool pool = CreatePool();
            var service = new BuqiRunBattleService(new BuqiLocalOpponentProvider(pool));
            BuqiRunState run = CreateRun(BuqiRunPhase.PveBattle);
            BuildSnapshot board = CreateBuild("player-board", "damage", 0);

            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                null,
                board,
                out BuqiPveSelection first,
                out string firstError), Is.True, firstError);
            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                first,
                board,
                out BuqiPveSelection restored,
                out string restoredError), Is.True, restoredError);

            Assert.That(first.Cards, Has.Count.EqualTo(3));
            Assert.That(first.Cards[0].Difficulty, Is.EqualTo(BuqiPveDifficulty.Initial));
            Assert.That(first.Cards[1].Difficulty, Is.EqualTo(BuqiPveDifficulty.Intermediate));
            Assert.That(first.Cards[2].Difficulty, Is.EqualTo(BuqiPveDifficulty.Dangerous));
            Assert.That(first.Cards[0].OpponentId, Is.Not.EqualTo(first.Cards[1].OpponentId));
            Assert.That(first.Cards[1].OpponentId, Is.Not.EqualTo(first.Cards[2].OpponentId));
            Assert.That(first.NextRngCursor, Is.EqualTo(run.RngCursor + 3));
            Assert.That(restored.NextRngCursor, Is.EqualTo(first.NextRngCursor));
            Assert.That(restored.Cards[0].OpponentId, Is.EqualTo(first.Cards[0].OpponentId));
            Assert.That(restored.Cards[1].OpponentId, Is.EqualTo(first.Cards[1].OpponentId));
            Assert.That(restored.Cards[2].OpponentId, Is.EqualTo(first.Cards[2].OpponentId));

            foreach (BuqiPveChoiceCard card in first.Cards)
            {
                Assert.That(card.Threat, Is.Not.Null);
                Assert.That(card.Threat.EquippedItemCount, Is.GreaterThan(0));
                Assert.That(card.Reward, Is.Not.Null);
                Assert.That(card.Reward.VictoryProgress, Is.EqualTo(1));
                Assert.That(card.Threat.Rank, Is.EqualTo((int)card.Difficulty + 1));
                Assert.That(card.Reward.Rank, Is.EqualTo((int)card.Difficulty + 1));
            }

            Assert.That(first.CurrentBoard, Is.Not.SameAs(board));
            first.CurrentBoard.SnapshotId = "mutated-copy";
            Assert.That(restored.CurrentBoard.SnapshotId, Is.EqualTo("player-board"));
        }

        [Test]
        public void SelectDifficulty_DirectlyCreatesTheChosenPveBattle()
        {
            BuqiLocalOpponentPool pool = CreatePool();
            var service = new BuqiRunBattleService(new BuqiLocalOpponentProvider(pool));
            BuqiRunState run = CreateRun(BuqiRunPhase.PveBattle);
            BuildSnapshot board = CreateBuild("player-board", "damage", 0);
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                null,
                board,
                out BuqiPveSelection selection,
                out string selectionError), Is.True, selectionError);
            BuqiPveChoiceCard expected = selection.Cards[1];

            Assert.That(service.TrySelectPveDifficultyAndSimulate(
                run,
                selection,
                BuqiPveDifficulty.Intermediate,
                board,
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.True, error);

            Assert.That(session.Kind, Is.EqualTo(BuqiRunBattleKind.Pve));
            Assert.That(session.PveDifficulty, Is.EqualTo(BuqiPveDifficulty.Intermediate));
            Assert.That(session.OpponentId, Is.EqualTo(expected.OpponentId));
            Assert.That(session.Request.Right.SnapshotId, Is.EqualTo(expected.OpponentBuild.SnapshotId));
            Assert.That(session.NextRngCursor, Is.EqualTo(selection.NextRngCursor));
            Assert.That(run.RngCursor, Is.EqualTo(5));
            Assert.That(run.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));
        }

        [Test]
        public void InvalidSelection_DoesNotAdvanceRngOrPhase()
        {
            var service = new BuqiRunBattleService(new BuqiLocalOpponentProvider(CreatePool()));
            BuqiRunState run = CreateRun(BuqiRunPhase.PveBattle);
            BuildSnapshot board = CreateBuild("player-board", "damage", 0);
            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                null,
                board,
                out BuqiPveSelection selection,
                out string selectionError), Is.True, selectionError);
            int cursor = run.RngCursor;
            BuqiRunPhase phase = run.Phase;

            Assert.That(service.TrySelectPveDifficultyAndSimulate(
                run,
                selection,
                (BuqiPveDifficulty)99,
                board,
                BuqiTestSuite.CreateFixtureProvider(),
                out BuqiRunBattleSession session,
                out string error), Is.False);

            Assert.That(session, Is.Null);
            Assert.That(error, Does.Contain("difficulty"));
            Assert.That(run.RngCursor, Is.EqualTo(cursor));
            Assert.That(run.Phase, Is.EqualTo(phase));
        }

        [Test]
        public void TamperedFrozenCard_DoesNotCreateBattleOrAdvanceRun()
        {
            var service = new BuqiRunBattleService(new BuqiLocalOpponentProvider(CreatePool()));
            BuqiRunState run = CreateRun(BuqiRunPhase.PveBattle);
            BuildSnapshot board = CreateBuild("player-board", "damage", 0);
            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                null,
                board,
                out BuqiPveSelection selection,
                out string selectionError), Is.True, selectionError);
            selection.Cards[2].Reward.Rank = 99;

            Assert.That(service.TrySelectPveDifficultyAndSimulate(
                run,
                selection,
                BuqiPveDifficulty.Dangerous,
                board,
                BuqiTestSuite.CreateFixtureProvider(),
                out BuqiRunBattleSession session,
                out string error), Is.False);

            Assert.That(session, Is.Null);
            Assert.That(error, Does.Contain("identity"));
            Assert.That(run.RngCursor, Is.EqualTo(5));
            Assert.That(run.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));
        }

        [Test]
        public void Widget_ExposesCardsAndBoardWithoutStorageAndSelectsOncePerClick()
        {
            var service = new BuqiRunBattleService(new BuqiLocalOpponentProvider(CreatePool()));
            BuqiRunState run = CreateRun(BuqiRunPhase.PveBattle);
            BuildSnapshot board = CreateBuild("player-board", "damage", 0);
            Assert.That(service.TryGetOrCreatePveSelection(
                run,
                null,
                board,
                out BuqiPveSelection selection,
                out string selectionError), Is.True, selectionError);

            var root = new GameObject("PveSelectionWidgetTest");
            try
            {
                var widget = root.AddComponent<PveSelectionWidget>();
                int selectionCount = 0;
                BuqiPveDifficulty selected = default;
                widget.Render(selection, difficulty =>
                {
                    selectionCount++;
                    selected = difficulty;
                });

                Assert.That(widget.Cards, Has.Count.EqualTo(3));
                Assert.That(widget.CurrentBoard.SnapshotId, Is.EqualTo("player-board"));
                Assert.That(typeof(PveSelectionWidget).GetProperty("Storage"), Is.Null);
                Assert.That(widget.Select(BuqiPveDifficulty.Dangerous), Is.True);
                Assert.That(selectionCount, Is.EqualTo(1));
                Assert.That(selected, Is.EqualTo(BuqiPveDifficulty.Dangerous));

                widget.Clear();
                Assert.That(widget.Select(BuqiPveDifficulty.Initial), Is.False);
                Assert.That(selectionCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BuqiRunState CreateRun(BuqiRunPhase phase)
        {
            BuqiRunState run = BuqiRunState.CreateInitial(24680);
            run.Day = 4;
            run.RngCursor = 5;
            run.Phase = phase;
            return run;
        }

        private static BuqiLocalOpponentPool CreatePool()
        {
            var pool = new BuqiLocalOpponentPool();
            string[] definitions = { "damage", "buffer", "passive", "heal", "burn", "poison" };
            for (int index = 0; index < definitions.Length; index++)
            {
                string id = $"monster-{index}";
                pool.Pve.Add(new BuqiRunOpponent
                {
                    OpponentId = id,
                    DisplayName = id,
                    Source = BuqiRunOpponentSource.PvePreset,
                    Build = CreateBuild(id, definitions[index], index),
                });
            }

            for (int index = 0; index < 3; index++)
            {
                string id = $"player-{index}";
                pool.Pvp.Add(new BuqiRunOpponent
                {
                    OpponentId = id,
                    DisplayName = id,
                    Source = BuqiRunOpponentSource.LocalPlayerPreset,
                    Build = CreateBuild(id, definitions[index], index),
                });
            }

            return pool;
        }

        private static BuildSnapshot CreateBuild(string id, string definitionId, int rank)
        {
            BuildSnapshot build = BuqiTestSuite.Snapshot(
                id,
                100 + (rank * 10),
                rank,
                new[] { BuqiTestSuite.Item($"{id}-item", definitionId, 0) });
            build.ArchetypeId = id;
            return build;
        }
    }
}
