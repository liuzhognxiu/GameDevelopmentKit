#if UNITY_EDITOR || BUQI_HEADLESS
using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 独立于 approved hash 的行为契约断言。
    /// 只有这些语义检查全部通过后，才允许人工执行 update-hashes 更新批准基线，避免错误实现自我批准。
    /// </summary>
    public static class BuqiContractChecks
    {
        /// <summary>运行战斗契约 v0.4 的全部行为检查，并返回可读失败列表。</summary>
        public static List<string> RunAll()
        {
            var failures = new List<string>();

            // 同一组检查由 Unity EditMode 和 .NET 无头端执行，保证两端没有分叉测试口径。
            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            List<BuqiTestVector> vectors = BuqiTestSuite.CreateVectors();

            // 顺序本身不影响结果，但按“确定性基础 -> 规则语义 -> 边界终止 -> 对称性”排列，便于诊断。
            CheckDeterminism(provider, vectors, failures);
            CheckInvalidRequests(provider, failures);
            CheckAdjacency(provider, vectors, failures);
            CheckReadyUseIsolation(provider, failures);
            CheckSameTickBuffer(provider, vectors, failures);
            CheckNoise(provider, vectors, failures);
            CheckHealAndRegen(provider, failures);
            CheckPoisonBypassesShield(provider, failures);
            CheckBurnUsesShield(provider, failures);
            CheckFreezeStopsCooldown(provider, failures);
            CheckChargeCap(provider, vectors, failures);
            CheckChargeDeclarationConsumption(provider, failures);
            CheckChargeSameSourceDeclareSequence(provider, failures);
            CheckChargeSameTickAndLogSemantics(provider, failures);
            CheckRewrite(provider, vectors, failures);
            CheckReliable(provider, vectors, failures);
            CheckUseCount(provider, vectors, failures);
            CheckAnnotationSemantics(provider, failures);
            CheckCanonicalSnapshot(provider, failures);
            CheckLoopCap(provider, vectors, failures);
            CheckOvertime(provider, vectors, failures);
            CheckHardCap(provider, failures);
            CheckMirror(provider, vectors, failures);
            return failures;
        }

        private static void CheckDeterminism(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BuqiTestVector vector = RequireVector(vectors, "determinism-basic", failures);
            if (vector == null)
                return;
            string firstHash = string.Empty;
            BattleResult firstResult = null;
            for (int index = 0; index < 100; index++)
            {
                BattleResult result = BuqiBattleSimulator.Simulate(vector.Request, provider, out _, out _, out _);
                if (index == 0)
                {
                    firstHash = result.BattleLogHash;
                    firstResult = result;
                }
                else if (result.BattleLogHash != firstHash || !SameResult(firstResult, result))
                {
                    failures.Add("determinism: repeated simulation changed result or hash");
                    return;
                }
            }
        }

        private static void CheckInvalidRequests(IItemDefinitionProvider provider, List<string> failures)
        {
            AssertOutcome(null, provider, BattleOutcome.InvalidBuild, "null request", failures);

            BattleRequest wrongRule = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("l", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("r", "damage", 0)));
            wrongRule.RuleVersion = "0.3.0";
            AssertOutcome(wrongRule, provider, BattleOutcome.InvalidBuild, "rule version mismatch", failures);

            BattleRequest wrongContent = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("l", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("r", "damage", 0)));
            wrongContent.Left.ContentVersion = "unknown";
            AssertOutcome(wrongContent, provider, BattleOutcome.InvalidBuild, "content version mismatch", failures);

            BattleRequest wrongAnnotation = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("l", "damage", 0, "A-99")),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("r", "damage", 0)));
            AssertOutcome(wrongAnnotation, provider, BattleOutcome.InvalidBuild, "annotation mismatch", failures);

            BattleRequest duplicateAcrossSides = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("same", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("same", "damage", 0)));
            AssertOutcome(
                duplicateAcrossSides, provider, BattleOutcome.InvalidBuild,
                "cross-side duplicate instance id", failures);

            BattleRequest excessiveInitialBuffer = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot(
                    "L", 100, BuqiBattleSimulator.BufferCap + 1,
                    BuqiTestSuite.Item("buffer-over-cap", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("buffer-target", "damage", 0)));
            AssertOutcome(
                excessiveInitialBuffer, provider, BattleOutcome.InvalidBuild,
                "initial buffer exceeds cap", failures);

            BattleRequest excessiveInitialNoise = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("noise-over-cap", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("noise-target", "damage", 0)));
            excessiveInitialNoise.Left.InitialNoiseDebt = BuqiBattleSimulator.NoiseThreshold;
            AssertOutcome(
                excessiveInitialNoise, provider, BattleOutcome.InvalidBuild,
                "initial noise reaches threshold", failures);
        }

        private static void CheckAdjacency(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BattleResult gap = Simulate(vectors, "gap-blocks-adjacent", provider, out List<BattleEvent> gapLog, failures);
            if (gap == null)
                return;
            if (CountReason(gapLog, "adjacent-response") != 0)
                failures.Add("adjacency: a gap did not block adjacent response");

            Simulate(vectors, "adjacency-chain", provider, out List<BattleEvent> adjacentLog, failures);
            if (CountReason(adjacentLog, "adjacent-response") == 0)
                failures.Add("adjacency: touching items did not trigger adjacent response");
        }

        private static void CheckReadyUseIsolation(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0,
                    BuqiTestSuite.Item("fast", "charge", 0),
                    BuqiTestSuite.Item("slow", "damage", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
            int slowDeclarationsBeforeTick29 = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick < 29 && battleEvent.ActorInstanceId == "slow" &&
                    battleEvent.Type == BuqiEventType.Declare)
                {
                    slowDeclarationsBeforeTick29++;
                }
            }
            if (slowDeclarationsBeforeTick29 != 0)
                failures.Add("ready-use: one ready item triggered another item's OnUse");
        }

        private static void CheckSameTickBuffer(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BattleResult result = Simulate(vectors, "same-tick-buffer", provider, out List<BattleEvent> log, failures);
            if (result == null)
                return;
            if (SumReasonAtTick(log, "BufferGain", 0) != 20 ||
                SumReasonAtTick(log, "BufferAbsorb", 0) != 15 ||
                SumReasonAtTick(log, "Damage", 0) != 0)
            {
                failures.Add("aggregate: tick-0 buffer did not absorb tick-0 normal damage");
            }
        }

        private static void CheckNoise(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BattleResult result = Simulate(vectors, "noise-threshold", provider, out List<BattleEvent> log, failures);
            if (result == null)
                return;
            int firstNoiseTick = FindFirstReasonTick(log, "noise");
            int accidentCount = CountReasonAtTick(log, "NoiseAccident", firstNoiseTick);
            if (accidentCount != 2)
                failures.Add("noise: 21 noise did not produce exactly two accidents");
        }

        private static void CheckHealAndRegen(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 30, 0,
                    BuqiTestSuite.Item("heal", "heal", 0),
                    BuqiTestSuite.Item("regen", "regen", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out SideState left, out _);

            if (left.Execution <= 30)
                failures.Add("heal/regen: execution did not increase above the wounded starting value");
            if (CountReason(log, "Heal") == 0)
                failures.Add("heal: direct heal was not logged");
            if (CountReason(log, "Regen") == 0)
                failures.Add("regen: periodic heal was not logged");
        }

        private static void CheckPoisonBypassesShield(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("poison", "poison", 0)),
                BuqiTestSuite.Snapshot("R", 100, 60, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);

            if (right.Execution >= 100)
                failures.Add("poison: poison did not reduce execution");
            if (right.Buffer != 60)
                failures.Add("poison: poison consumed shield even though it should bypass shield");
            if (CountReason(log, "PoisonDamage") == 0)
                failures.Add("poison: poison damage was not logged");
        }

        private static void CheckBurnUsesShield(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("burn", "burn", 0)),
                BuqiTestSuite.Snapshot("R", 100, 60, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);

            if (right.Buffer >= 60)
                failures.Add("burn: burn did not consume shield first");
            if (CountReason(log, "BurnDamage") == 0 && CountReason(log, "BurnShieldAbsorb") == 0)
                failures.Add("burn: burn damage or shield absorption was not logged");
        }

        private static void CheckFreezeStopsCooldown(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest controlRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("left", "passive", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("attacker", "damage", 0)));
            BuqiBattleSimulator.Simulate(controlRequest, provider, out List<BattleEvent> controlLog, out _, out _);

            BattleRequest freezeRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("freeze", "freeze", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("attacker", "damage", 0)));
            BuqiBattleSimulator.Simulate(freezeRequest, provider, out List<BattleEvent> freezeLog, out _, out _);

            int controlUses = CountActorDeclarations(controlLog, "attacker", "strike");
            int frozenUses = CountActorDeclarations(freezeLog, "attacker", "strike");
            if (frozenUses >= controlUses)
                failures.Add("freeze: frozen enemy item did not lose cooldown progress");
            if (CountReason(freezeLog, "FreezeApplied") == 0)
                failures.Add("freeze: freeze application was not logged");
        }

        private static void CheckChargeCap(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BuqiTestVector vector = RequireVector(vectors, "charge-cap", failures);
            if (vector == null)
                return;
            BuqiBattleSimulator.Simulate(vector.Request, provider, out _, out SideState left, out _);
            if (left.Items[0].Charge != BuqiBattleSimulator.ChargeCap)
                failures.Add("charge: final charge exceeded or missed cap 9");
        }

        private static void CheckChargeDeclarationConsumption(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest consuming = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("consumer", "charge-consumer", 1)),
                BuqiTestSuite.Snapshot("R", 100, 0,
                    BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                consuming, provider, out List<BattleEvent> consumingLog, out SideState consumingLeft, out _);
            if (consumingLeft.Items[1].Charge != 0 ||
                SumActorDeclaredAtTick(consumingLog, "consumer", "charge-consume-a", 0) != 7 ||
                SumActorDeclaredAtTick(consumingLog, "consumer", "charge-consume-b", 0) != 1)
            {
                failures.Add("charge: declaration-time consumption was not single-use and deterministic");
            }
            if (CountReasonAtTick(consumingLog, "ChargeConsumed", 0) != 1)
                failures.Add("charge: consumed charge was not logged once at declaration");

            BattleRequest reader = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("reader", "charge-reader", 1)),
                BuqiTestSuite.Snapshot("R", 100, 0,
                    BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                reader, provider, out List<BattleEvent> readerLog, out SideState readerLeft, out _);
            if (readerLeft.Items[1].Charge != 3 ||
                SumActorDeclaredAtTick(readerLog, "reader", "charge-read-a", 0) != 7 ||
                SumActorDeclaredAtTick(readerLog, "reader", "charge-read-b", 0) != 7)
            {
                failures.Add("charge: read-only effects did not reuse the same declared snapshot");
            }

            BattleRequest rewrite = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("rewrite", "charge-rewrite", 1, "A-03")),
                BuqiTestSuite.Snapshot("R", 100, 0,
                    BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                rewrite, provider, out List<BattleEvent> rewriteLog, out SideState rewriteLeft, out _);
            int rewriteTick = FindFirstActorReasonTick(rewriteLog, "rewrite", "charge-rewrite");
            if (rewriteTick < 0 ||
                rewriteLeft.Items[1].Charge != 0 ||
                CountActorDeclarationsAtTick(rewriteLog, "rewrite", "charge-rewrite", rewriteTick) != 2 ||
                SumActorDeclaredAtTick(rewriteLog, "rewrite", "charge-rewrite", rewriteTick) != 12 ||
                CountReasonAtTick(rewriteLog, "ChargeConsumed", rewriteTick) != 1)
            {
                failures.Add("charge: A-03 rewrite did not reuse the direct declaration snapshot without double consumption");
            }

            BattleRequest noTarget = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("no-target", "charge-no-target", 1)),
                BuqiTestSuite.Snapshot("R", 100, 0,
                    BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                noTarget, provider, out List<BattleEvent> noTargetLog, out SideState noTargetLeft, out _);
            if (noTargetLeft.Items[1].Charge != 3 ||
                CountActorReasonAtTick(noTargetLog, "no-target", "NoValidTarget", 0) != 1 ||
                CountReasonAtTick(noTargetLog, "ChargeConsumed", 0) != 0)
            {
                failures.Add("charge: declaration without a valid target consumed or read charge");
            }
        }

        private static void CheckChargeSameSourceDeclareSequence(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest sameActor = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("sequenced", "same-actor-charge-sequence", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0,
                    BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                sameActor, provider, out List<BattleEvent> log, out SideState left, out _);

            if (left.Items[0].Charge != 0 ||
                left.Buffer != 7 ||
                SumActorDeclaredAtTick(log, "sequenced", "a-same-actor-buffer", 0) != 7 ||
                SumActorReasonAtTick(log, "sequenced", "ChargeConsumed", 0) != -3)
            {
                failures.Add("charge: same-source Declare sequence did not let prior charge feed a later consuming declaration");
            }
        }

        private static void CheckChargeSameTickAndLogSemantics(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest sameTick = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 8, 0,
                    BuqiTestSuite.Item("left-source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("left-consumer", "charge-consumer", 1)),
                BuqiTestSuite.Snapshot("R", 8, 0,
                    BuqiTestSuite.Item("right-source", "opening-charge-source", 0),
                    BuqiTestSuite.Item("right-consumer", "charge-consumer", 1)));
            BattleResult result = BuqiBattleSimulator.Simulate(
                sameTick, provider, out List<BattleEvent> log, out SideState left, out SideState right);

            if (result.Outcome != BattleOutcome.Draw ||
                result.DurationTicks != 1 ||
                left.Execution != 0 ||
                right.Execution != 0)
            {
                failures.Add("charge: same-tick bilateral consuming declarations did not resolve simultaneously");
            }

            if (CountReasonAtTick(log, "ChargeConsumed", 0) != 2 ||
                SumReasonAtTick(log, "ChargeConsumed", 0) != -6 ||
                !AllReasonEventsAtTickMatch(log, "ChargeConsumed", 0, BuqiEventPhase.Declare, BuqiEventType.Effect))
            {
                failures.Add("charge: consumption log events were not negative Declare-phase resource changes");
            }
        }

        private static void CheckRewrite(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "rewrite", provider, out List<BattleEvent> log, failures);
            int firstUseTick = FindFirstReasonTick(log, "adjacent-source");
            int sourceDamage = SumDeclaredAtTick(log, "adjacent-source", firstUseTick);
            int adjacentResponses = CountReasonAtTick(log, "adjacent-response", firstUseTick);
            if (sourceDamage != 12)
                failures.Add("A-03: first direct effect was not 100% + 50%");
            if (adjacentResponses != 1)
                failures.Add("A-03: rewrite retriggered adjacent response");
        }

        private static void CheckReliable(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "reliable", provider, out List<BattleEvent> log, failures);
            if (CountReason(log, "A04Immune") == 0)
                failures.Add("A-04: effective enemy delay was not logged as immune");
            if (CountReason(log, "interfered-response") != 0)
                failures.Add("A-04: immune delay incorrectly triggered OnFirstInterfered");
        }

        private static void CheckUseCount(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "use-count", provider, out List<BattleEvent> log, failures);
            int burstTick = FindFirstReasonTick(log, "count-burst");
            if (burstTick != 29)
                failures.Add("use-count: third use did not trigger at tick 29");
        }

        /// <summary>
        /// 集中验证 A-01 加急、A-02 延期、A-05 静音、A-06 超额及品质倍率。
        /// A-03 复写和 A-04 可靠因包含链与免疫语义，分别由独立检查覆盖。
        /// </summary>
        private static void CheckAnnotationSemantics(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest annotations = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0,
                    BuqiTestSuite.Item("a01", "damage", 0, "A-01"),
                    BuqiTestSuite.Item("a02", "damage", 1, "A-02"),
                    BuqiTestSuite.Item("a05", "noise", 2, "A-05"),
                    BuqiTestSuite.Item("a06", "damage", 3, "A-06")),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(annotations, provider, out List<BattleEvent> log, out SideState left, out _);

            if (left.Items[0].EffectiveBaseCooldownTicks != 26)
                failures.Add("A-01: cooldown was not reduced by 15% with rounding");
            if (left.Items[1].EffectiveBaseCooldownTicks != 36)
                failures.Add("A-02: cooldown was not increased by 20%");
            int a02Tick = FindFirstActorReasonTick(log, "a02", "strike");
            if (SumActorDeclaredAtTick(log, "a02", "strike", a02Tick) != 13)
                failures.Add("A-02: non-opening effect was not increased by 30%");
            int a05Tick = FindFirstActorReasonTick(log, "a05", "noise");
            if (SumActorDeclaredAtTick(log, "a05", "noise", a05Tick) != 21 ||
                SumActorReasonAtTick(log, "a05", "NoiseChange", a05Tick) != 20)
            {
                failures.Add("A-05: noise source was not reduced by exactly one");
            }
            if (SumActorReasonAtTick(log, "a06", "NoiseChange", 0) != 3)
                failures.Add("A-06: opening noise was not applied at tick 0");
            int a06Tick = FindFirstActorReasonTick(log, "a06", "strike");
            if (SumActorDeclaredAtTick(log, "a06", "strike", a06Tick) != 14)
                failures.Add("A-06: damage was not increased by 35% with rounding");

            BattleRequest quality = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("quality", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            quality.Left.Items[0].Quality = (int)BuqiQuality.Improved;
            BuqiBattleSimulator.Simulate(quality, provider, out List<BattleEvent> qualityLog, out _, out _);
            int qualityTick = FindFirstActorReasonTick(qualityLog, "quality", "strike");
            if (SumActorDeclaredAtTick(qualityLog, "quality", "strike", qualityTick) != 16)
                failures.Add("quality: improved multiplier was not 1.60");
        }

        private static void CheckCanonicalSnapshot(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BuildSnapshot first = BuqiTestSuite.Snapshot("same", 100, 0,
                BuqiTestSuite.Item("b", "damage", 1),
                BuqiTestSuite.Item("a", "buffer", 0));
            BuildSnapshot second = BuqiTestSuite.Snapshot("same", 100, 0,
                BuqiTestSuite.Item("a", "buffer", 0),
                BuqiTestSuite.Item("b", "damage", 1));
            if (!BuqiBoardValidator.Validate(first, provider, out _) ||
                BuqiCrypto.SnapshotHash(first) != BuqiCrypto.SnapshotHash(second))
            {
                failures.Add("snapshot: canonical hash depended on input item order");
            }
        }

        /// <summary>
        /// 当前向量验证单实例每 tick 第五个声明被截断；全场 64 事件框架由模拟器和压力测试共同守护。
        /// </summary>
        private static void CheckLoopCap(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "loop-cap", provider, out List<BattleEvent> log, failures);
            if (CountReasonAtTick(log, "cap-5", 0) != 0)
                failures.Add("loop-cap: fifth event from one item was not truncated");
            if (CountReason(log, "PerItemLoopCapReached") != 1)
                failures.Add("loop-cap: per-item truncation was not logged exactly once");
        }

        /// <summary>验证 tick 450 双方劫火经 Aggregate 同时造成直接伤害并允许平局。</summary>
        private static void CheckOvertime(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BattleResult result = Simulate(vectors, "overtime", provider, out List<BattleEvent> log, failures);
            if (result == null)
                return;
            if (result.Outcome != BattleOutcome.Draw || result.TerminationReason != TerminationReason.Overtime.ToString())
                failures.Add("overtime: simultaneous direct damage did not produce overtime draw");
            if (CountReasonAtTick(log, "OvertimeDamage", BuqiBattleSimulator.NormalTickCount) != 2)
                failures.Add("overtime: both sides did not receive aggregate direct damage at tick 450");
        }

        /// <summary>验证 tick 600 后按执行值、护体、失衡的固定顺序裁决。</summary>
        private static void CheckHardCap(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest executionRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("left", "passive", 0)),
                BuqiTestSuite.Snapshot("R", 999, 60, BuqiTestSuite.Item("right", "passive", 0)));
            BattleResult executionResult = BuqiBattleSimulator.Simulate(
                executionRequest, provider, out _, out _, out _);
            if (executionResult.TerminationReason != TerminationReason.HardCap.ToString() ||
                executionResult.Outcome != BattleOutcome.LeftWin)
            {
                failures.Add("hard-cap: execution was not the first comparison key");
            }

            BattleRequest noiseRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 5, BuqiTestSuite.Item("left", "passive", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 5, BuqiTestSuite.Item("right", "passive", 0)));
            noiseRequest.Left.InitialNoiseDebt = 2;
            noiseRequest.Right.InitialNoiseDebt = 4;
            BattleResult noiseResult = BuqiBattleSimulator.Simulate(
                noiseRequest, provider, out _, out _, out _);
            if (noiseResult.Outcome != BattleOutcome.LeftWin)
                failures.Add("hard-cap: lower noise did not win after execution and buffer tied");
        }

        private static void CheckMirror(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            BuqiTestVector vector = RequireVector(vectors, "mirror", failures);
            if (vector == null)
                return;
            BattleResult original = BuqiBattleSimulator.Simulate(vector.Request, provider, out _, out _, out _);
            var swapped = new BattleRequest
            {
                RuleVersion = vector.Request.RuleVersion,
                BattleSeed = vector.Request.BattleSeed,
                RoundIndex = vector.Request.RoundIndex,
                Left = vector.Request.Right,
                Right = vector.Request.Left,
            };
            BattleResult mirror = BuqiBattleSimulator.Simulate(swapped, provider, out _, out _, out _);
            if (original.DurationTicks != mirror.DurationTicks ||
                original.LeftExecution != mirror.RightExecution ||
                original.RightExecution != mirror.LeftExecution ||
                MirrorOutcome(original.Outcome) != mirror.Outcome)
            {
                failures.Add("mirror: swapped input did not produce mirrored result");
            }
        }

        private static BattleResult Simulate(
            List<BuqiTestVector> vectors,
            string id,
            IItemDefinitionProvider provider,
            out List<BattleEvent> log,
            List<string> failures)
        {
            BuqiTestVector vector = RequireVector(vectors, id, failures);
            if (vector == null)
            {
                log = new List<BattleEvent>();
                return null;
            }
            return BuqiBattleSimulator.Simulate(vector.Request, provider, out log, out _, out _);
        }

        private static BuqiTestVector RequireVector(
            List<BuqiTestVector> vectors,
            string id,
            List<string> failures)
        {
            BuqiTestVector vector = BuqiTestSuite.FindVector(vectors, id);
            if (vector == null)
                failures.Add(BuqiText.Format("missing vector: {0}", id));
            return vector;
        }

        private static void AssertOutcome(
            BattleRequest request,
            IItemDefinitionProvider provider,
            BattleOutcome expected,
            string label,
            List<string> failures)
        {
            BattleResult result = BuqiBattleSimulator.Simulate(request, provider, out _, out _, out _);
            if (result.Outcome != expected)
                failures.Add(BuqiText.Format("{0}: expected {1}, got {2}", label, expected, result.Outcome));
        }

        private static bool SameResult(BattleResult left, BattleResult right)
        {
            return left.Outcome == right.Outcome &&
                   left.DurationTicks == right.DurationTicks &&
                   left.LeftExecution == right.LeftExecution &&
                   left.RightExecution == right.RightExecution &&
                   left.LeftBuffer == right.LeftBuffer &&
                   left.RightBuffer == right.RightBuffer &&
                   left.LeftNoise == right.LeftNoise &&
                   left.RightNoise == right.RightNoise &&
                   left.TerminationReason == right.TerminationReason;
        }

        private static BattleOutcome MirrorOutcome(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.LeftWin) return BattleOutcome.RightWin;
            if (outcome == BattleOutcome.RightWin) return BattleOutcome.LeftWin;
            return outcome;
        }

        private static int CountReason(List<BattleEvent> log, string reason)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.ReasonCode == reason)
                    count++;
            }
            return count;
        }

        private static int CountReasonAtTick(List<BattleEvent> log, string reason, int tick)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reason)
                    count++;
            }
            return count;
        }

        private static int CountActorReasonAtTick(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason)
                {
                    count++;
                }
            }
            return count;
        }

        private static int FindFirstReasonTick(List<BattleEvent> log, string reason)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.ReasonCode == reason)
                    return battleEvent.Tick;
            }
            return -1;
        }

        private static int SumReasonAtTick(List<BattleEvent> log, string reason, int tick)
        {
            int total = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reason)
                    total += battleEvent.Amount;
            }
            return total;
        }

        private static int SumDeclaredAtTick(List<BattleEvent> log, string reason, int tick)
        {
            int total = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reason &&
                    battleEvent.Type == BuqiEventType.Declare)
                {
                    total += battleEvent.Amount;
                }
            }
            return total;
        }

        private static int FindFirstActorReasonTick(
            List<BattleEvent> log,
            string actorId,
            string reason)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.ActorInstanceId == actorId && battleEvent.ReasonCode == reason)
                    return battleEvent.Tick;
            }
            return -1;
        }

        private static int CountActorDeclarationsAtTick(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason && battleEvent.Type == BuqiEventType.Declare)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountActorDeclarations(List<BattleEvent> log, string actorId, string reason)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason &&
                    battleEvent.Type == BuqiEventType.Declare)
                {
                    count++;
                }
            }
            return count;
        }

        private static int SumActorDeclaredAtTick(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick)
        {
            int total = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason && battleEvent.Type == BuqiEventType.Declare)
                {
                    total += battleEvent.Amount;
                }
            }
            return total;
        }

        private static int SumActorReasonAtTick(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick)
        {
            int total = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason)
                {
                    total += battleEvent.Amount;
                }
            }
            return total;
        }

        private static bool AllReasonEventsAtTickMatch(
            List<BattleEvent> log,
            string reason,
            int tick,
            BuqiEventPhase phase,
            BuqiEventType type)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reason &&
                    (battleEvent.Phase != phase || battleEvent.Type != type))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
#endif
