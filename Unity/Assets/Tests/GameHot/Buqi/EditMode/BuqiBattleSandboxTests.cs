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
        public void WalkthroughRecord_RequiresPredictionThenResultThenCauseAndChange()
        {
            BuqiSandboxScenario scenario = BuqiBattleSandbox.FindScenario("fast-space-choice");
            BuqiSandboxWalkthroughRecord record = BuqiBattleSandbox.BeginWalkthrough(
                scenario,
                "tester-01",
                "截止日会先触发并在护体建立前造成主要伤害。");

            Assert.That(record.HasBattleResult, Is.False);
            Assert.That(record.IsComplete, Is.False);

            BuqiSandboxRunResult run = BuqiBattleSandbox.Run(scenario);
            BuqiBattleSandbox.BindWalkthroughResult(record, run);
            Assert.That(record.HasBattleResult, Is.True);
            Assert.That(record.BattleLogHash, Is.EqualTo(run.Result.BattleLogHash));
            Assert.That(record.Outcome, Is.EqualTo(run.Result.Outcome));
            Assert.That(record.IsComplete, Is.False);

            BuqiBattleSandbox.CompleteWalkthrough(
                record,
                "护体建立前的截止日伤害先形成血量差。",
                BuqiSandboxChangeKind.Position,
                "把加急通知移到截止日前一格，预期让加速和输出窗口更早形成。");

            Assert.That(record.IsComplete, Is.True);
            Assert.That(record.ParticipantId, Is.EqualTo("tester-01"));
            Assert.That(record.ScenarioId, Is.EqualTo(scenario.Id));
            Assert.That(record.ChangeKind, Is.EqualTo(BuqiSandboxChangeKind.Position));
        }

        [Test]
        public void WalkthroughRecord_RejectsResultBeforePredictionAndMismatchedScenario()
        {
            BuqiSandboxScenario fast = BuqiBattleSandbox.FindScenario("fast-space-choice");
            BuqiSandboxScenario chain = BuqiBattleSandbox.FindScenario("adjacency-chain");
            BuqiSandboxRunResult fastRun = BuqiBattleSandbox.Run(fast);
            BuqiSandboxWalkthroughRecord record = BuqiBattleSandbox.BeginWalkthrough(
                fast,
                "tester-02",
                "联签流程会先把蓄力传给右邻。");

            Assert.Throws<System.InvalidOperationException>(() =>
                BuqiBattleSandbox.BindWalkthroughResult(record, BuqiBattleSandbox.Run(chain)));
            Assert.Throws<System.InvalidOperationException>(() =>
                BuqiBattleSandbox.CompleteWalkthrough(
                    record,
                    "尚未运行",
                    BuqiSandboxChangeKind.Purchase,
                    "先补一张小型辅助法门。"));

            BuqiBattleSandbox.BindWalkthroughResult(record, fastRun);
            Assert.Throws<System.InvalidOperationException>(() =>
                BuqiBattleSandbox.BindWalkthroughResult(record, fastRun));
        }

        [Test]
        public void WalkthroughRecord_RejectsEmptyParticipantPredictionCauseAndChange()
        {
            BuqiSandboxScenario scenario = BuqiBattleSandbox.FindScenario("buffer-loss-counter");
            Assert.Throws<System.ArgumentException>(() =>
                BuqiBattleSandbox.BeginWalkthrough(scenario, "", "预测"));
            Assert.Throws<System.ArgumentException>(() =>
                BuqiBattleSandbox.BeginWalkthrough(scenario, "tester-03", ""));

            BuqiSandboxWalkthroughRecord record = BuqiBattleSandbox.BeginWalkthrough(
                scenario,
                "tester-03",
                "护体清空后会触发风险清单反击。");
            BuqiBattleSandbox.BindWalkthroughResult(record, BuqiBattleSandbox.Run(scenario));
            Assert.Throws<System.ArgumentException>(() =>
                BuqiBattleSandbox.CompleteWalkthrough(
                    record, "", BuqiSandboxChangeKind.Refinement, "改淬炼"));
            Assert.Throws<System.ArgumentException>(() =>
                BuqiBattleSandbox.CompleteWalkthrough(
                    record, "护体被打穿", BuqiSandboxChangeKind.Refinement, ""));
        }

        [Test]
        public void FastBufferWalkthroughVariant_AddsOneLegalBufferWithoutChangingLeftBuildOrSeed()
        {
            BuqiSandboxScenario baseline = BuqiBattleSandbox.FindScenario("fast-space-choice");
            BuqiSandboxScenario variant = BuqiBattleSandbox.CreateFastBufferWalkthroughVariant();
            IItemDefinitionProvider provider = BuqiBattleSandbox.CreateDefinitionProvider();

            Assert.That(variant.Request.BattleSeed, Is.EqualTo(baseline.Request.BattleSeed));
            Assert.That(variant.Request.Left.SnapshotId, Is.EqualTo(baseline.Request.Left.SnapshotId));
            Assert.That(variant.Request.Left.Items.Count, Is.EqualTo(baseline.Request.Left.Items.Count));
            Assert.That(variant.Request.Right.Items.Count, Is.EqualTo(baseline.Request.Right.Items.Count + 1));
            Assert.That(variant.Request.Right.Items[3].DefinitionId, Is.EqualTo("W8-007"));
            Assert.That(variant.Request.Right.Items[3].AnchorSlot, Is.EqualTo(5));
            Assert.That(BuqiBoardValidator.Validate(variant.Request.Left, provider, out List<string> leftErrors),
                Is.True, string.Join("\n", leftErrors));
            Assert.That(BuqiBoardValidator.Validate(variant.Request.Right, provider, out List<string> rightErrors),
                Is.True, string.Join("\n", rightErrors));
        }

        [Test]
        public void FastBufferDelayedDamageWalkthroughVariant_OnlyAddsA02ToLeftDamageCore()
        {
            BuqiSandboxScenario previous = BuqiBattleSandbox.CreateFastBufferWalkthroughVariant();
            BuqiSandboxScenario variant = BuqiBattleSandbox.CreateFastBufferDelayedDamageWalkthroughVariant();
            IItemDefinitionProvider provider = BuqiBattleSandbox.CreateDefinitionProvider();

            Assert.That(variant.Request.BattleSeed, Is.EqualTo(previous.Request.BattleSeed));
            Assert.That(variant.Request.Left.Items.Count, Is.EqualTo(previous.Request.Left.Items.Count));
            Assert.That(variant.Request.Right.Items.Count, Is.EqualTo(previous.Request.Right.Items.Count));
            for (int index = 0; index < previous.Request.Left.Items.Count; index++)
            {
                Assert.That(variant.Request.Left.Items[index].InstanceId,
                    Is.EqualTo(previous.Request.Left.Items[index].InstanceId));
                Assert.That(variant.Request.Left.Items[index].DefinitionId,
                    Is.EqualTo(previous.Request.Left.Items[index].DefinitionId));
                Assert.That(variant.Request.Left.Items[index].AnchorSlot,
                    Is.EqualTo(previous.Request.Left.Items[index].AnchorSlot));
                if (index != 1)
                {
                    Assert.That(variant.Request.Left.Items[index].AnnotationId,
                        Is.EqualTo(previous.Request.Left.Items[index].AnnotationId));
                }
            }
            for (int index = 0; index < previous.Request.Right.Items.Count; index++)
            {
                Assert.That(variant.Request.Right.Items[index].InstanceId,
                    Is.EqualTo(previous.Request.Right.Items[index].InstanceId));
                Assert.That(variant.Request.Right.Items[index].DefinitionId,
                    Is.EqualTo(previous.Request.Right.Items[index].DefinitionId));
                Assert.That(variant.Request.Right.Items[index].AnchorSlot,
                    Is.EqualTo(previous.Request.Right.Items[index].AnchorSlot));
                Assert.That(variant.Request.Right.Items[index].AnnotationId,
                    Is.EqualTo(previous.Request.Right.Items[index].AnnotationId));
            }
            Assert.That(previous.Request.Left.Items[1].AnnotationId, Is.Empty);
            Assert.That(variant.Request.Left.Items[1].AnnotationId, Is.EqualTo("A-02"));
            Assert.That(BuqiBoardValidator.Validate(variant.Request.Left, provider, out List<string> leftErrors),
                Is.True, string.Join("\n", leftErrors));
            Assert.That(BuqiBoardValidator.Validate(variant.Request.Right, provider, out List<string> rightErrors),
                Is.True, string.Join("\n", rightErrors));
        }

        [Test]
        public void FastBufferWalkthroughSummary_MatchesRecordedRoundTwoEvidence()
        {
            BuqiSandboxRunResult run = BuqiBattleSandbox.Run(
                BuqiBattleSandbox.CreateFastBufferWalkthroughVariant());
            BuqiSandboxBattleSummary summary = BuqiBattleSandbox.CreateBattleSummary(run);

            Assert.That(summary.Outcome, Is.EqualTo(BattleOutcome.RightWin));
            Assert.That(summary.DurationTicks, Is.EqualTo(601));
            Assert.That(summary.LeftExecution, Is.EqualTo(24));
            Assert.That(summary.RightExecution, Is.EqualTo(68));
            Assert.That(summary.RightBuffer, Is.EqualTo(42));
            Assert.That(summary.RightBufferAbsorbed, Is.EqualTo(224));
            Assert.That(summary.RightCounterDeclarationCount, Is.EqualTo(2));
            Assert.That(summary.RightCounterDeclaredDamage, Is.EqualTo(22));
            Assert.That(summary.LeftNoiseAccidentCount, Is.EqualTo(3));
            Assert.That(summary.LeftNoiseAccidentDamage, Is.EqualTo(24));
            Assert.That(summary.BattleLogHash,
                Is.EqualTo("a95c8760347873cafc658bf4254a1b97cc7e204dccc6e2c5a94b4dce2e80fbb8"));
        }

        [Test]
        public void FastBufferDelayedDamageWalkthroughSummary_MatchesRecordedRoundThreeEvidence()
        {
            BuqiSandboxRunResult run = BuqiBattleSandbox.Run(
                BuqiBattleSandbox.CreateFastBufferDelayedDamageWalkthroughVariant());
            BuqiSandboxBattleSummary summary = BuqiBattleSandbox.CreateBattleSummary(run);

            Assert.That(summary.Outcome, Is.EqualTo(BattleOutcome.RightWin));
            Assert.That(summary.DurationTicks, Is.EqualTo(601));
            Assert.That(summary.LeftExecution, Is.EqualTo(24));
            Assert.That(summary.RightExecution, Is.EqualTo(70));
            Assert.That(summary.RightBuffer, Is.EqualTo(24));
            Assert.That(summary.RightBufferAbsorbed, Is.EqualTo(244));
            Assert.That(summary.RightCounterDeclarationCount, Is.EqualTo(2));
            Assert.That(summary.RightCounterDeclaredDamage, Is.EqualTo(22));
            Assert.That(summary.LeftNoiseAccidentCount, Is.EqualTo(3));
            Assert.That(summary.LeftNoiseAccidentDamage, Is.EqualTo(24));
            Assert.That(summary.BattleLogHash,
                Is.EqualTo("a23c97daa308b3a9e9315a9631cf94b32bcc089429018a96e59542a0bcfc2cec"));
        }

        [Test]
        public void BattleSummary_ProjectsFinalStateAndKeyEventsWithoutRecalculation()
        {
            BuqiSandboxRunResult run = Run("fast-space-choice");
            BuqiSandboxBattleSummary summary = BuqiBattleSandbox.CreateBattleSummary(run);

            Assert.That(summary.Outcome, Is.EqualTo(run.Result.Outcome));
            Assert.That(summary.DurationTicks, Is.EqualTo(run.Result.DurationTicks));
            Assert.That(summary.LeftExecution, Is.EqualTo(run.Result.LeftExecution));
            Assert.That(summary.RightExecution, Is.EqualTo(run.Result.RightExecution));
            Assert.That(summary.LeftBuffer, Is.EqualTo(run.Result.LeftBuffer));
            Assert.That(summary.RightBuffer, Is.EqualTo(run.Result.RightBuffer));
            Assert.That(summary.BattleLogHash, Is.EqualTo(run.Result.BattleLogHash));
            Assert.That(summary.LeftBufferAbsorbed, Is.EqualTo(SumOpponentSourceReason(
                run, run.Scenario.Request.Right, "BufferAbsorb")));
            Assert.That(summary.RightBufferAbsorbed, Is.EqualTo(SumOpponentSourceReason(
                run, run.Scenario.Request.Left, "BufferAbsorb")));
            Assert.That(summary.LeftNoiseAccidentCount, Is.EqualTo(CountSourceReason(
                run.Log, run.Scenario.Request.Left, "NoiseAccident")));
            Assert.That(summary.LeftNoiseAccidentDamage, Is.EqualTo(SumSourceReason(
                run.Log, run.Scenario.Request.Left, "NoiseAccident")));
            Assert.That(summary.LeftCounterDeclarationCount, Is.EqualTo(
                CountSourceReason(run.Log, run.Scenario.Request.Left, "W8-008-buffer-counter", BuqiEventType.Declare) +
                CountSourceReason(run.Log, run.Scenario.Request.Left, "W8-012-buffer-counter", BuqiEventType.Declare)));
            Assert.That(summary.RightCounterDeclarationCount, Is.EqualTo(
                CountSourceReason(run.Log, run.Scenario.Request.Right, "W8-008-buffer-counter", BuqiEventType.Declare) +
                CountSourceReason(run.Log, run.Scenario.Request.Right, "W8-012-buffer-counter", BuqiEventType.Declare)));
            Assert.That(summary.LeftCounterDeclaredDamage, Is.EqualTo(
                SumSourceReason(run.Log, run.Scenario.Request.Left, "W8-008-buffer-counter", BuqiEventType.Declare) +
                SumSourceReason(run.Log, run.Scenario.Request.Left, "W8-012-buffer-counter", BuqiEventType.Declare)));
            Assert.That(summary.RightCounterDeclaredDamage, Is.EqualTo(
                SumSourceReason(run.Log, run.Scenario.Request.Right, "W8-008-buffer-counter", BuqiEventType.Declare) +
                SumSourceReason(run.Log, run.Scenario.Request.Right, "W8-012-buffer-counter", BuqiEventType.Declare)));
            Assert.That(BuqiBattleSandbox.FormatBattleSummary(summary), Does.Contain(run.Result.BattleLogHash));
        }

        [Test]
        public void BattleSummary_RejectsNullRunAndSummary()
        {
            Assert.Throws<System.ArgumentNullException>(() => BuqiBattleSandbox.CreateBattleSummary(null));
            Assert.Throws<System.ArgumentNullException>(() => BuqiBattleSandbox.FormatBattleSummary(null));
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

        private static int SumOpponentSourceReason(
            BuqiSandboxRunResult run,
            BuildSnapshot sourceSide,
            string reasonCode)
        {
            return SumSourceReason(run.Log, sourceSide, reasonCode);
        }

        private static int CountSourceReason(
            List<BattleEvent> log,
            BuildSnapshot sourceSide,
            string reasonCode,
            BuqiEventType eventType = BuqiEventType.Effect)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Type == eventType &&
                    battleEvent.ReasonCode == reasonCode &&
                    ContainsInstance(sourceSide, battleEvent.SourceInstanceId))
                {
                    count++;
                }
            }
            return count;
        }

        private static int SumSourceReason(
            List<BattleEvent> log,
            BuildSnapshot sourceSide,
            string reasonCode,
            BuqiEventType eventType = BuqiEventType.Effect)
        {
            int sum = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Type == eventType &&
                    battleEvent.ReasonCode == reasonCode &&
                    ContainsInstance(sourceSide, battleEvent.SourceInstanceId))
                {
                    sum += battleEvent.Amount;
                }
            }
            return sum;
        }

        private static bool ContainsInstance(BuildSnapshot snapshot, string instanceId)
        {
            foreach (ItemInstance item in snapshot.Items)
            {
                if (item.InstanceId == instanceId)
                    return true;
            }
            return false;
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
