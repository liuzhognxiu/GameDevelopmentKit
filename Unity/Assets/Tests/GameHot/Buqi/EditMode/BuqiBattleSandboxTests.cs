using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    /// <summary>Step 2 九法门沙盒的内容、联动、布局、过滤和生命周期回归。</summary>
    public sealed class BuqiBattleSandboxTests
    {
        [Test]
        public void SandboxCatalog_HasNineItemsThreeArchetypesAndThreeAnnotations()
        {
            Assert.That(BuqiBattleSandbox.ItemInfos.Count, Is.EqualTo(9));
            var archetypes = new HashSet<BuqiSandboxArchetype>();
            foreach (BuqiSandboxItemInfo info in BuqiBattleSandbox.ItemInfos.Values)
                archetypes.Add(info.Archetype);
            Assert.That(archetypes.Count, Is.EqualTo(3));

            List<BuqiSandboxScenario> scenarios = BuqiBattleSandbox.CreateScenarios();
            Assert.That(scenarios.Count, Is.EqualTo(3));
            var annotations = new HashSet<string>();
            foreach (BuqiSandboxScenario scenario in scenarios)
            {
                CollectAnnotations(scenario.Request.Left, annotations);
                CollectAnnotations(scenario.Request.Right, annotations);
            }
            Assert.That(annotations, Does.Contain("A-01"));
            Assert.That(annotations, Does.Contain("A-03"));
            Assert.That(annotations, Does.Contain("A-04"));
        }

        [Test]
        public void SandboxScenarios_AreValidAndRepeatDeterministically()
        {
            IItemDefinitionProvider provider = BuqiBattleSandbox.CreateDefinitionProvider();
            foreach (BuqiSandboxScenario scenario in BuqiBattleSandbox.CreateScenarios())
            {
                Assert.That(BuqiBoardValidator.Validate(scenario.Request.Left, provider, out List<string> leftErrors),
                    Is.True, string.Join("\n", leftErrors));
                Assert.That(BuqiBoardValidator.Validate(scenario.Request.Right, provider, out List<string> rightErrors),
                    Is.True, string.Join("\n", rightErrors));

                BuqiSandboxRunResult run = BuqiBattleSandbox.Run(scenario);
                Assert.That(run.Result.Outcome, Is.Not.EqualTo(BattleOutcome.InvalidBuild));
                Assert.That(run.Log, Is.Not.Empty);
                Assert.That(run.LeftBoardText, Does.Contain("[7]"));
                Assert.That(run.RightBoardText, Does.Contain("[7]"));

                BuqiSandboxRepeatResult repeat = BuqiBattleSandbox.Repeat(scenario, 100);
                Assert.That(repeat.IsDeterministic, Is.True, scenario.Id);
                Assert.That(repeat.CompletedRuns, Is.EqualTo(100));
            }
        }

        [Test]
        public void FastScenario_CoversSizesHasteChargeNoiseAndUrgentAnnotation()
        {
            BuqiSandboxRunResult run = Run("fast-space-choice");
            Assert.That(run.LeftBoardText, Does.Contain("截止日(L)"));
            Assert.That(run.LeftBoardText, Does.Contain("冲刺看板(M)"));
            Assert.That(run.LeftBoardText, Does.Contain("加急通知(S)"));
            AssertReason(run.Log, "W8-006-opening-haste");
            AssertReason(run.Log, "W8-005-adjacent-charge");
            AssertReason(run.Log, "W8-006-noise");
            AssertSourceReasonAmount(run.Log, "fast-urgent", "NoiseChange", 1);
        }

        [Test]
        public void BufferScenario_GainsLosesAndConvertsBufferToCounterDamage()
        {
            BuqiSandboxRunResult run = Run("buffer-loss-counter");
            AssertReason(run.Log, "BufferGain");
            AssertReason(run.Log, "BufferAbsorb");
            AssertReason(run.Log, "W8-008-buffer-counter");
            AssertReason(run.Log, "W8-012-buffer-counter");
        }

        [Test]
        public void ChainScenario_ProducesAdjacentResponsesChargeAndRewrite()
        {
            BuqiSandboxRunResult run = Run("adjacency-chain");
            AssertReason(run.Log, "W8-013-pass-charge");
            AssertReason(run.Log, "W8-014-adjacent-charge");
            AssertReason(run.Log, "W8-015-adjacent-haste");
            BattleEvent firstUse = FindEvent(run.Log, "W8-014-damage");
            Assert.That(CountDeclarationsAtTick(run.Log, "W8-014-damage", firstUse.Tick), Is.EqualTo(2));
        }

        [Test]
        public void LogFilter_FiltersTickChainSourceAndReasonWithoutMutatingLog()
        {
            BuqiSandboxRunResult run = Run("adjacency-chain");
            BattleEvent sample = FindEvent(run.Log, "W8-014-adjacent-charge");
            int originalCount = run.Log.Count;
            var filter = new BuqiSandboxLogFilter
            {
                Tick = sample.Tick,
                ChainId = sample.ChainId,
                SourceInstanceId = sample.SourceInstanceId,
                ReasonCode = sample.ReasonCode,
            };

            List<BattleEvent> filtered = BuqiBattleSandbox.FilterLog(run.Log, filter);
            Assert.That(filtered, Is.Not.Empty);
            foreach (BattleEvent battleEvent in filtered)
            {
                Assert.That(battleEvent.Tick, Is.EqualTo(filter.Tick));
                Assert.That(battleEvent.ChainId, Does.Contain(filter.ChainId));
                Assert.That(battleEvent.SourceInstanceId, Does.Contain(filter.SourceInstanceId));
                Assert.That(battleEvent.ReasonCode, Does.Contain(filter.ReasonCode));
            }
            Assert.That(run.Log.Count, Is.EqualTo(originalCount));
        }

        [Test]
        public void SandboxRun_CloseAndReopenModel_DoesNotReuseMutableState()
        {
            BuqiSandboxScenario scenario = BuqiBattleSandbox.FindScenario("buffer-loss-counter");
            BuqiSandboxRunResult first = BuqiBattleSandbox.Run(scenario);
            BuqiSandboxRunResult second = BuqiBattleSandbox.Run(scenario);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Log, Is.Not.SameAs(first.Log));
            Assert.That(second.LeftFinal, Is.Not.SameAs(first.LeftFinal));
            Assert.That(second.RightFinal, Is.Not.SameAs(first.RightFinal));
            Assert.That(second.Result.BattleLogHash, Is.EqualTo(first.Result.BattleLogHash));
            Assert.That(second.Log.Count, Is.EqualTo(first.Log.Count));
        }

        private static BuqiSandboxRunResult Run(string scenarioId)
        {
            BuqiSandboxScenario scenario = BuqiBattleSandbox.FindScenario(scenarioId);
            Assert.That(scenario, Is.Not.Null, scenarioId);
            return BuqiBattleSandbox.Run(scenario);
        }

        private static void CollectAnnotations(BuildSnapshot snapshot, HashSet<string> annotations)
        {
            foreach (ItemInstance item in snapshot.Items)
            {
                if (!string.IsNullOrEmpty(item.AnnotationId))
                    annotations.Add(item.AnnotationId);
            }
        }

        private static void AssertReason(List<BattleEvent> log, string reasonCode)
        {
            Assert.That(FindEvent(log, reasonCode), Is.Not.Null, reasonCode);
        }

        private static void AssertSourceReasonAmount(
            List<BattleEvent> log,
            string sourceInstanceId,
            string reasonCode,
            int amount)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.SourceInstanceId == sourceInstanceId &&
                    battleEvent.ReasonCode == reasonCode &&
                    battleEvent.Amount == amount)
                {
                    return;
                }
            }

            Assert.Fail(BuqiText.Format(
                "Missing event source={0}, reason={1}, amount={2}",
                sourceInstanceId,
                reasonCode,
                amount));
        }

        private static BattleEvent FindEvent(List<BattleEvent> log, string reasonCode)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.ReasonCode == reasonCode)
                    return battleEvent;
            }
            return null;
        }

        private static int CountDeclarationsAtTick(
            List<BattleEvent> log,
            string reasonCode,
            int tick)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reasonCode &&
                    battleEvent.Type == BuqiEventType.Declare)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
