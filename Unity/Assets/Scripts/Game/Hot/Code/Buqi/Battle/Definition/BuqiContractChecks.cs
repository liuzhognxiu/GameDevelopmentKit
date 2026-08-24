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
        /// <summary>运行战斗契约 v0.6 的全部行为检查，并返回可读失败列表。</summary>
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
            CheckLatestStatusSchedule(failures);
            CheckLatestControlAndCharge(failures);
            CheckLatestModifierScoping(failures);
            CheckLatestHealingAndStatuses(failures);
            CheckLatestStatusAggregation(failures);
            CheckLatestCritical(failures);
            CheckLatestCriticalRollUniqueness(failures);
            CheckMultiSettlementAndCap(provider, failures);
            CheckAmmoLifecycle(provider, failures);
            CheckCappedAmmo(provider, failures);
            CheckCappedAmmoReplay(provider, failures);
            CheckChargeAmmoReservation(failures);
            CheckFlightLifecycle(provider, failures);
            CheckFlightRefreshSource(provider, failures);
            CheckSingleFlightExit(provider, failures);
            CheckFlightControlMitigation(provider, failures);
            CheckLatestRage(failures);
            CheckExtremeRageAndNoise(failures);
            CheckLatestIntegerBoundaries(failures);
            CheckLatestStorm(failures);
            CheckLatestReplayProjection(failures);
            CheckFlightRefreshReplay(provider, failures);
            CheckRewrite(provider, vectors, failures);
            CheckReliable(provider, vectors, failures);
            CheckUseCount(provider, vectors, failures);
            CheckAnnotationSemantics(provider, failures);
            CheckCanonicalSnapshot(provider, failures);
            CheckLoopCap(provider, vectors, failures);
            CheckMirror(provider, vectors, failures);
            return failures;
        }

        private static void CheckLatestStatusSchedule(List<string> failures)
        {
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("poison-start", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Poison,
                        BuqiTarget.EnemyExecution, 3, "schedule-poison", 30)),
                Definition("tick-ten-use", 11,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "tick-ten-use")),
                Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 100, 0, BuqiTestSuite.Item("poison", "poison-start", 0)),
                ContractSnapshot("R", 100, 0, BuqiTestSuite.Item("target", "tick-ten-use", 0)),
                11);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);

            int poisonTick = FindFirstReasonTick(log, "PoisonDamage");
            int useTick = FindFirstActorReasonTick(log, "target", "tick-ten-use");
            BattleEvent poison = FindEvent(log, "PoisonDamage", 10);
            BattleEvent use = FindActorEvent(log, "target", "tick-ten-use", 10, BuqiEventType.Declare);
            if (BuqiBattleSimulator.TicksPerSecond != 10 || poisonTick != 10 || useTick != 10 ||
                poison == null || use == null || poison.Sequence >= use.Sequence ||
                poison.Phase != BuqiEventPhase.PreTick)
            {
                failures.Add("Latest schedule: per-second status must tick at tick 10 before cooldown/use settlement");
            }
        }

        private static void CheckLatestControlAndCharge(List<string> failures)
        {
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("haste-fixed", 20,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Haste,
                        BuqiTarget.Self, 1, "haste-fixed", 100),
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "haste-use")),
                Definition("slow-fixed", 20,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Delay,
                        BuqiTarget.Self, 9999, "slow-fixed", 100),
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "slow-use")),
                Definition("mutual-control", 20,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Haste,
                        BuqiTarget.Self, 1, "mutual-haste", 100),
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Delay,
                        BuqiTarget.Self, 1, "mutual-slow", 100),
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "mutual-use")),
                Definition("charge-source", 5,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Charge,
                        BuqiTarget.ShortestCooldownEnemyItem, 10, "charge-push")),
                Definition("freeze-source", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Freeze,
                        BuqiTarget.ShortestCooldownEnemyItem, 20, "freeze-opening")),
                Definition("charge-target", 30,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "charged-use")),
                Definition("passive-contract", 1000));

            int hasteTick = FirstUseTick(provider, "haste-fixed", "haste-use");
            int slowTick = FirstUseTick(provider, "slow-fixed", "slow-use");
            BattleRequest mutualRequest = ContractRequest(
                ContractSnapshot("L", 1000, 0, BuqiTestSuite.Item("mutual", "mutual-control", 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 3);
            BuqiBattleSimulator.Simulate(
                mutualRequest, provider, out List<BattleEvent> mutualLog, out SideState mutual, out _);
            int mutualTick = FindFirstActorReasonTick(mutualLog, "mutual", "mutual-use");
            bool hasHaste = HasModifier(mutual, BuqiEffect.Haste);
            if (hasteTick != 10 || slowTick != 38 || mutualTick != 38 || hasHaste)
            {
                failures.Add(BuqiText.Format(
                    "Latest control: Haste=2x, Slow=0.5x, exclusive (hasteTick={0}, slowTick={1}, mutualTick={2}, haste={3}, slow={4})",
                    hasteTick, slowTick, mutualTick, hasHaste,
                    CountReasonAtTick(mutualLog, "mutual-slow", 0)));
            }

            BattleRequest charged = ContractRequest(
                ContractSnapshot("L", 1000, 0, BuqiTestSuite.Item("charger", "charge-source", 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("charged", "charge-target", 0)), 4);
            BuqiBattleSimulator.Simulate(charged, provider, out List<BattleEvent> chargedLog, out _, out _);
            if (FindFirstActorReasonTick(chargedLog, "charged", "charged-use") != 9 ||
                CountReasonAtTick(chargedLog, "ChargeAdvanced", 9) == 0)
            {
                failures.Add("Latest Charge: cooldown advancement must trigger a newly-ready item in the same tick chain");
            }

            BattleRequest frozen = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("freeze", "freeze-source", 0),
                    BuqiTestSuite.Item("charger", "charge-source", 1)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("charged", "charge-target", 0)), 5);
            BuqiBattleSimulator.Simulate(frozen, provider, out List<BattleEvent> frozenLog, out _, out _);
            if (FindFirstActorReasonTick(frozenLog, "charged", "charged-use") != 29 ||
                CountReason(frozenLog, "ChargeBlockedFrozen") == 0)
            {
                failures.Add(BuqiText.Format(
                    "Latest Freeze: frozen items must neither charge nor trigger (useTick={0}, blocked={1})",
                    FindFirstActorReasonTick(frozenLog, "charged", "charged-use"),
                    CountReason(frozenLog, "ChargeBlockedFrozen")));
            }
        }

        private static void CheckLatestHealingAndStatuses(List<string> failures)
        {
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("status-source", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Poison,
                        BuqiTarget.EnemyExecution, 10, "poison-ten", 50),
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Burn,
                        BuqiTarget.EnemyExecution, 10, "burn-ten", 50)),
                Definition("healer", 10,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Heal,
                        BuqiTarget.Self, 100, "full-heal")),
                Definition("regen-start", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Regen,
                        BuqiTarget.Self, 5, "regen-five", 20)),
                Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0, BuqiTestSuite.Item("status", "status-source", 0)),
                ContractSnapshot("R", 50, 20, BuqiTestSuite.Item("heal", "healer", 0)), 6);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);
            if (SumReasonAtTick(log, "Heal", 9) != 50 ||
                SumReasonAtTick(log, "HealOverflow", 9) != 50 ||
                CountReasonAtTick(log, "StatusCleansed", 9) != 2 ||
                SumReasonAtTick(log, "PoisonDamage", 10) != 9 ||
                SumReasonAtTick(log, "BurnDamage", 10) != 1 ||
                SumReasonAtTick(log, "BurnShieldMitigated", 10) != 1 || right.Buffer != 20)
            {
                failures.Add("Latest statuses: Heal must cap and cleanse 10%; Poison bypasses Shield; Shield halves Burn without consumption");
            }

            BattleRequest regen = ContractRequest(
                ContractSnapshot("L", 50, 0, BuqiTestSuite.Item("regen", "regen-start", 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 7);
            BuqiBattleSimulator.Simulate(regen, provider, out List<BattleEvent> regenLog, out _, out _);
            if (FindFirstReasonTick(regenLog, "Regen") != 10 || SumReasonAtTick(regenLog, "Regen", 10) != 5)
                failures.Add("Latest Regen: per-second healing must settle exactly on the 10-tick boundary");

            IItemDefinitionProvider phaseProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("regen-full", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Regen,
                        BuqiTarget.Self, 5, "regen-after-damage", 20)),
                Definition("regen-hit", 11,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 5, "regen-hit")));
            BattleRequest sameTick = ContractRequest(
                ContractSnapshot("L", 100, 0, BuqiTestSuite.Item("a-regen", "regen-full", 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("z-hit", "regen-hit", 0)), 71);
            BattleResult phaseResult = BuqiBattleSimulator.Simulate(
                sameTick, phaseProvider, out List<BattleEvent> phaseLog, out _, out _);
            BattleEvent damage = FindEvent(phaseLog, "Damage", 10);
            BattleEvent healing = FindEvent(phaseLog, "Regen", 10);
            var phaseReplay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = sameTick.Left,
                RightBuild = sameTick.Right,
                Result = phaseResult,
                Log = phaseLog,
                Definitions = phaseProvider,
            });
            phaseReplay.Advance(1f);
            if (damage == null || healing == null || damage.Sequence >= healing.Sequence ||
                healing.Amount != 5 || phaseReplay.Frame.Left.Execution != 100)
                failures.Add("Latest Regen phase: tick-boundary Regen must settle after same-tick trigger damage");
        }

        private static void CheckLatestStatusAggregation(List<string> failures)
        {
            BuqiEffectSpec poison = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Poison,
                BuqiTarget.EnemyExecution, 1, "stacked-poison", 30);
            poison.RepeatCount = 2;
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("stacked-poison", 1000, poison),
                Definition("stack-cleanser", 10,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Heal,
                        BuqiTarget.Self, 1, "stack-heal")));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("poison-a", "stacked-poison", 0),
                    BuqiTestSuite.Item("poison-b", "stacked-poison", 1)),
                ContractSnapshot("R", 99, 0,
                    BuqiTestSuite.Item("cleanser", "stack-cleanser", 0)), 72);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
            if (SumReasonAtTick(log, "StatusApplied", 0) != 4 ||
                SumReasonAtTick(log, "StatusCleansed", 9) != 1 ||
                SumReasonAtTick(log, "PoisonDamage", 10) != 3)
            {
                failures.Add("Latest statuses: repeated applications must stack and Heal must cleanse 10% of the total status amount");
            }
        }

        private static void CheckLatestModifierScoping(List<string> failures)
        {
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("global-slow", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Delay,
                        BuqiTarget.EnemyExecution, 1, "global-slow", 100)),
                Definition("single-haste", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Haste,
                        BuqiTarget.ShortestCooldownEnemyItem, 1, "single-haste", 100)),
                Definition("tempo-target", 20,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "tempo-target-use")),
                Definition("tempo-other", 20,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "tempo-other-use")));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("target", "tempo-target", 0),
                    BuqiTestSuite.Item("other", "tempo-other", 1)),
                ContractSnapshot("R", 1000, 0,
                    BuqiTestSuite.Item("slow", "global-slow", 0),
                    BuqiTestSuite.Item("haste", "single-haste", 1)), 73);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
            if (FindFirstActorReasonTick(log, "target", "tempo-target-use") != 10 ||
                FindFirstActorReasonTick(log, "other", "tempo-other-use") != 38)
            {
                failures.Add("Latest control scope: an item Haste may override side Slow only for that item");
            }
        }

        private static void CheckLatestCritical(List<string> failures)
        {
            BuqiEffectSpec critDamage = CriticalSpec(BuqiEffect.Damage, BuqiTarget.EnemyExecution, 3, "crit-damage", 10000);
            BuqiEffectSpec critBuffer = CriticalSpec(BuqiEffect.Buffer, BuqiTarget.Self, 4, "crit-buffer", 10000);
            BuqiEffectSpec critHeal = CriticalSpec(BuqiEffect.Heal, BuqiTarget.Self, 5, "crit-heal", 10000);
            BuqiEffectSpec critRegen = CriticalSpec(BuqiEffect.Regen, BuqiTarget.Self, 6, "crit-regen", 10000, 30);
            BuqiEffectSpec critBurn = CriticalSpec(BuqiEffect.Burn, BuqiTarget.EnemyExecution, 7, "crit-burn", 10000, 30);
            BuqiEffectSpec critPoison = CriticalSpec(BuqiEffect.Poison, BuqiTarget.EnemyExecution, 8, "crit-poison", 10000, 30);
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("crit-a", 1000, critDamage, critBuffer, critHeal, critRegen),
                Definition("crit-b", 1000, critBurn, critPoison),
                Definition("crit-random", 1000,
                    CriticalSpec(BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "crit-random", 5000)),
                Definition("crit-refined", 10,
                    CriticalSpec(BuqiEffect.Damage, BuqiTarget.EnemyExecution, 10, "crit-refined", 10000,
                        30, BuqiTrigger.OnUse)),
                Definition("passive-contract", 1000));
            BattleRequest all = ContractRequest(
                ContractSnapshot("L", 50, 0,
                    BuqiTestSuite.Item("crit-a", "crit-a", 0),
                    BuqiTestSuite.Item("crit-b", "crit-b", 1)),
                ContractSnapshot("R", 100, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 8);
            BuqiBattleSimulator.Simulate(all, provider, out List<BattleEvent> allLog, out _, out _);
            if (CountReasonAtTick(allLog, "CriticalApplied", 0) != 6 ||
                SumActorDeclaredAtTick(allLog, "crit-a", "crit-damage", 0) != 6 ||
                SumActorDeclaredAtTick(allLog, "crit-a", "crit-buffer", 0) != 8 ||
                SumActorDeclaredAtTick(allLog, "crit-a", "crit-heal", 0) != 10 ||
                SumActorDeclaredAtTick(allLog, "crit-a", "crit-regen", 0) != 12 ||
                SumActorDeclaredAtTick(allLog, "crit-b", "crit-burn", 0) != 14 ||
                SumActorDeclaredAtTick(allLog, "crit-b", "crit-poison", 0) != 16)
            {
                failures.Add("Latest Crit: 100% crit must double Damage/Heal/Shield/Regen/Burn/Poison");
            }

            bool sawHit = false;
            bool sawMiss = false;
            for (ulong seed = 0; seed < 64; seed++)
            {
                BattleRequest random = ContractRequest(
                    ContractSnapshot("L", 100, 0, BuqiTestSuite.Item("crit", "crit-random", 0)),
                    ContractSnapshot("R", 100, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), seed);
                BattleResult first = BuqiBattleSimulator.Simulate(random, provider, out List<BattleEvent> firstLog, out _, out _);
                BattleResult second = BuqiBattleSimulator.Simulate(random, provider, out _, out _, out _);
                int declared = SumActorDeclaredAtTick(firstLog, "crit", "crit-random", 0);
                sawHit |= declared == 2;
                sawMiss |= declared == 1;
                if (first.BattleLogHash != second.BattleLogHash)
                    failures.Add("Latest Crit: the same BattleSeed produced different crit outcomes");
            }
            if (!sawHit || !sawMiss)
                failures.Add("Latest Crit: changing BattleSeed did not produce deterministic hit/miss variation");

            BattleRequest refined = ContractRequest(
                ContractSnapshot("L", 100, 0, BuqiTestSuite.Item("refined", "crit-refined", 0, "A-02")),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 9);
            BuqiBattleSimulator.Simulate(refined, provider, out List<BattleEvent> refinedLog, out _, out _);
            int refinedTick = FindFirstActorReasonTick(refinedLog, "refined", "crit-refined");
            if (SumActorDeclaredAtTick(refinedLog, "refined", "crit-refined", refinedTick) != 26)
                failures.Add("Latest Crit: refinement multiplier must be applied before the deterministic 2x crit");
        }

        private static void CheckLatestCriticalRollUniqueness(List<string> failures)
        {
            BuqiEffectSpec charge = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Charge,
                BuqiTarget.Self, 20, "crit-charge");
            BuqiEffectSpec damage = CriticalSpec(
                BuqiEffect.Damage, BuqiTarget.EnemyExecution,
                1, "crit-charged-use", 5000, 30, BuqiTrigger.OnUse);
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("crit-charged", 10, charge, damage),
                Definition("passive-contract", 1000));
            bool sawSplitRolls = false;
            for (ulong seed = 0; seed < 128 && !sawSplitRolls; seed++)
            {
                BattleRequest request = ContractRequest(
                    ContractSnapshot("L", 1000, 0,
                        BuqiTestSuite.Item("charged", "crit-charged", 0)),
                    ContractSnapshot("R", 1000, 0,
                        BuqiTestSuite.Item("target", "passive-contract", 0)), seed);
                BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
                int ones = CountActorDeclarationsWithAmountAtTick(log, "charged", "crit-charged-use", 0, 1);
                int twos = CountActorDeclarationsWithAmountAtTick(log, "charged", "crit-charged-use", 0, 2);
                sawSplitRolls = ones == 1 && twos == 1;
            }
            if (!sawSplitRolls)
                failures.Add("Latest Crit: separate same-chain activations must use distinct deterministic rolls");
        }

        private static void CheckLatestRage(List<string> failures)
        {
            BuqiEffectSpec rage = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Rage, BuqiTarget.Self, 100, "rage-gain");
            rage.RageThreshold = 100;
            rage.RageDurationTicks = 50;
            rage.RageCooldownReductionBps = 1000;
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("rage-user", 50, rage,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "rage-use")),
                Definition("rage-control", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Delay,
                        BuqiTarget.ShortestCooldownEnemyItem, 1, "rage-slow", 100),
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Freeze,
                        BuqiTarget.ShortestCooldownEnemyItem, 100, "rage-freeze")),
                Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0, BuqiTestSuite.Item("z-control", "rage-control", 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("a-rage", "rage-user", 0)), 10);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);
            if (FindFirstActorReasonTick(log, "a-rage", "rage-use") != 45 ||
                CountReasonAtTick(log, "EnrageStarted", 0) != 1 ||
                CountReasonAtTick(log, "RageGained", 0) != 1 ||
                right.Items[0].FrozenTicks != 0 || HasModifier(right, BuqiEffect.Delay))
            {
                failures.Add("Latest Rage: threshold must cleanse Freeze/Slow and grant 50 ticks of 10% cooldown reduction");
            }

            var replay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> replayLog, out _, out _),
                Log = replayLog,
                Definitions = provider,
            });
            replay.Advance(1f);
            if (replay.Frame.Right.Items[0].FrozenTicks != 0)
                failures.Add("Latest replay: EnrageStarted must project the Freeze cleanse");
        }

        private static void CheckExtremeRageAndNoise(List<string> failures)
        {
            BuqiEffectSpec rage = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Rage,
                BuqiTarget.Self, 1000, "rage-burst");
            rage.RageThreshold = 1;
            IItemDefinitionProvider rageProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(), Definition("rage-burst", 1000, rage),
                Definition("passive-contract", 1000));
            BattleRequest rageRequest = ContractRequest(
                ContractSnapshot("L", 10000, 0, BuqiTestSuite.Item("rage", "rage-burst", 0)),
                ContractSnapshot("R", 10000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 74);
            BuqiBattleSimulator.Simulate(rageRequest, rageProvider, out List<BattleEvent> rageLog, out SideState rageSide, out _);
            if (CountReasonAtTick(rageLog, "EnrageStarted", 0) != 1 ||
                CountReasonAtTick(rageLog, "RageConsumed", 0) != 1 || rageSide.Rage != 0)
            {
                failures.Add("Latest Rage boundary: many threshold crossings must aggregate without unbounded log expansion");
            }

            IItemDefinitionProvider noiseProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("noise-burst", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Noise,
                        BuqiTarget.Self, 1000, "noise-burst")),
                Definition("passive-contract", 1000));
            BattleRequest noiseRequest = ContractRequest(
                ContractSnapshot("L", 10000, 0, BuqiTestSuite.Item("noise", "noise-burst", 0)),
                ContractSnapshot("R", 10000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 75);
            BuqiBattleSimulator.Simulate(noiseRequest, noiseProvider, out List<BattleEvent> noiseLog, out SideState noiseSide, out _);
            if (CountReasonAtTick(noiseLog, "NoiseAccident", 0) != 1 ||
                SumReasonAtTick(noiseLog, "NoiseAccident", 0) != 800 || noiseSide.Noise != 0)
            {
                failures.Add("Latest Noise boundary: threshold incidents must aggregate with equivalent damage and remainder");
            }
        }

        private static void CheckChargeAmmoReservation(List<string> failures)
        {
            BuqiEffectSpec charge = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Charge,
                BuqiTarget.Self, 40, "ammo-charge");
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                DefinitionWithAmmo("charged-ammo", 10, 1, charge,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 3, "charged-ammo-use")),
                Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("charged", "charged-ammo", 0)),
                ContractSnapshot("R", 1000, 0,
                    BuqiTestSuite.Item("target", "passive-contract", 0)), 76);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out SideState left, out _);
            if (CountReasonAtTick(log, "AmmoConsumed", 0) != 1 ||
                CountActorReasonAtTick(log, "charged", "charged-ammo-use", 0) != 1 ||
                left.Items[0].AmmoRemaining != 0 || left.Items[0].IsEnabled ||
                left.Items[0].CooldownProgress > 0)
            {
                failures.Add("Latest Ammo/Charge: queued charge activations must reserve and consume only available ammunition");
            }
        }

        private static void CheckLatestIntegerBoundaries(List<string> failures)
        {
            IItemDefinitionProvider resourceProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("max-buffer", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Buffer,
                        BuqiTarget.Self, int.MaxValue, "max-buffer")),
                DefinitionWithAmmo("max-ammo", 1000, int.MaxValue,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Ammo,
                        BuqiTarget.Self, int.MaxValue, "max-ammo")),
                Definition("passive-contract", 1000));
            BattleRequest resources = ContractRequest(
                ContractSnapshot("L", 1000, BuqiBattleSimulator.BufferCap,
                    BuqiTestSuite.Item("buffer", "max-buffer", 0),
                    BuqiTestSuite.Item("ammo", "max-ammo", 1)),
                ContractSnapshot("R", 1000, 0,
                    BuqiTestSuite.Item("target", "passive-contract", 0)), 78);
            BuqiBattleSimulator.Simulate(resources, resourceProvider, out _, out SideState resourceSide, out _);
            if (resourceSide.Buffer != BuqiBattleSimulator.BufferCap ||
                resourceSide.Items[1].AmmoRemaining != int.MaxValue ||
                !resourceSide.Items[1].IsEnabled)
            {
                failures.Add("Latest integer boundary: Buffer and Ammo additions must saturate without wrapping");
            }

            IItemDefinitionProvider flightProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("max-flight", 1000,
                    Spec(BuqiTrigger.OnBattleStart, BuqiEffect.Flight,
                        BuqiTarget.Self, int.MaxValue, "max-flight", int.MaxValue)),
                Definition("max-freeze", 10,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Freeze,
                        BuqiTarget.ShortestCooldownEnemyItem, int.MaxValue, "max-freeze")),
                Definition("passive-contract", 1000));
            BattleRequest flight = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("flight", "max-flight", 0)),
                ContractSnapshot("R", 1000, 0,
                    BuqiTestSuite.Item("freeze", "max-freeze", 0)), 79);
            BuqiBattleSimulator.Simulate(flight, flightProvider, out List<BattleEvent> flightLog, out _, out _);
            if (SumReasonAtTick(flightLog, "FlightFreezeMitigation", 9) != 1073741823 ||
                SumReasonAtTick(flightLog, "FreezeApplied", 9) != 1073741824)
            {
                failures.Add("Latest Flight boundary: 50% control mitigation must not overflow at int.MaxValue");
            }

            IItemDefinitionProvider cooldownProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("max-cooldown", int.MaxValue,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "max-cooldown-use")),
                Definition("passive-contract", 1000));
            BattleRequest cooldown = ContractRequest(
                ContractSnapshot("L", 1000, 0,
                    BuqiTestSuite.Item("cooldown", "max-cooldown", 0, "A-02")),
                ContractSnapshot("R", 1000, 0,
                    BuqiTestSuite.Item("target", "passive-contract", 0)), 80);
            BattleResult cooldownResult = BuqiBattleSimulator.Simulate(
                cooldown, cooldownProvider, out List<BattleEvent> cooldownLog, out SideState cooldownSide, out _);
            if (cooldownResult.Outcome == BattleOutcome.InvalidBuild ||
                CountReason(cooldownLog, "max-cooldown-use") != 0 ||
                cooldownSide.Items[0].CooldownProgress <= 0)
            {
                failures.Add("Latest cooldown boundary: refinement and fixed-point progress must saturate without wrapping");
            }
        }

        private static void CheckLatestStorm(List<string> failures)
        {
            var rules = new BuqiBattleRuleConfig
            {
                StormStartTicks = 300,
                StormBaseDamage = 1,
                StormRampDamage = 1,
            };
            IItemDefinitionProvider provider = CreateContractProvider(
                rules, Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 110000, 60, BuqiTestSuite.Item("left", "passive-contract", 0)),
                ContractSnapshot("R", 100000, 60, BuqiTestSuite.Item("right", "passive-contract", 0)), 12);
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out SideState left, out SideState right);
            if (result.Outcome != BattleOutcome.LeftWin ||
                result.TerminationReason != TerminationReason.Storm.ToString() ||
                result.DurationTicks <= 601 || left.Buffer != 60 || right.Buffer != 60 ||
                SumReasonAtTick(log, "StormDamage", 300) != 2 ||
                SumReasonAtTick(log, "StormDamage", 301) != 4)
            {
                failures.Add("Latest Storm: no hard cap; per-tick ramping true damage must continue past tick 600 until death");
            }
        }

        private static void CheckLatestReplayProjection(List<string> failures)
        {
            BuqiEffectSpec rage = Spec(
                BuqiTrigger.OnBattleStart, BuqiEffect.Rage, BuqiTarget.Self, 100, "rage-replay");
            rage.RageThreshold = 100;
            IItemDefinitionProvider provider = CreateContractProvider(
                new BuqiBattleRuleConfig { StormStartTicks = 20, StormBaseDamage = 2, StormRampDamage = 1 },
                Definition("rage-replay", 1000, rage),
                Definition("passive-contract", 1000));
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 100, 0, BuqiTestSuite.Item("rage", "rage-replay", 0)),
                ContractSnapshot("R", 10, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 13);
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out _, out _);
            var replay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = result,
                Log = log,
                Definitions = provider,
            });
            replay.SkipToResult();
            if (!string.IsNullOrEmpty(replay.Frame.Error) ||
                replay.Frame.Left.Rage != 0 || replay.Frame.Left.EnragedTicks <= 0 ||
                replay.Frame.Right.Execution != result.RightExecution)
            {
                failures.Add("Latest replay: Rage/Enrage and source-less Storm damage must project to the recorded result");
            }

            string validRuleVersion = result.RuleVersion;
            result.RuleVersion = "legacy-rule";
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            bool rejectedRule = ReplayRejected(request, result, log, provider);
            result.RuleVersion = validRuleVersion;

            string validSimulationVersion = result.SimulationVersion;
            result.SimulationVersion = "legacy-simulation";
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            bool rejectedSimulation = ReplayRejected(request, result, log, provider);
            result.SimulationVersion = validSimulationVersion;

            string validContentVersion = result.ContentVersion;
            result.ContentVersion = "foreign-content";
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            bool rejectedContent = ReplayRejected(request, result, log, provider);
            result.ContentVersion = validContentVersion;

            string validSnapshotHash = result.LeftSnapshotHash;
            result.LeftSnapshotHash = "foreign-snapshot";
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            bool rejectedSnapshot = ReplayRejected(request, result, log, provider);
            result.LeftSnapshotHash = validSnapshotHash;
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            if (!rejectedRule || !rejectedSimulation || !rejectedContent || !rejectedSnapshot)
                failures.Add("Latest replay validation: incompatible rule/simulation/content/snapshot contracts must be rejected");
        }

        private static bool ReplayRejected(
            BattleRequest request,
            BattleResult result,
            List<BattleEvent> log,
            IItemDefinitionProvider provider)
        {
            try
            {
                _ = new BattleReplayController(new BattleReplayData
                {
                    LeftBuild = request.Left,
                    RightBuild = request.Right,
                    Result = result,
                    Log = log,
                    Definitions = provider,
                });
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static IItemDefinitionProvider CreateContractProvider(
            BuqiBattleRuleConfig rules,
            params BuqiItemDefinition[] definitions)
        {
            var map = new Dictionary<string, BuqiItemDefinition>(StringComparer.Ordinal);
            foreach (BuqiItemDefinition definition in definitions)
                map[definition.DefinitionId] = definition;
            return new DictionaryDefinitionProvider(BuqiTestSuite.FixtureContentVersion, map, rules);
        }

        private static BuqiItemDefinition Definition(
            string id,
            int cooldownTicks,
            params BuqiEffectSpec[] effects)
        {
            var definition = new BuqiItemDefinition
            {
                DefinitionId = id,
                Size = 1,
                BaseCooldownTicks = cooldownTicks,
            };
            definition.Effects.AddRange(effects);
            return definition;
        }

        private static BuqiItemDefinition DefinitionWithAmmo(
            string id,
            int cooldownTicks,
            int ammoCapacity,
            params BuqiEffectSpec[] effects)
        {
            BuqiItemDefinition definition = Definition(id, cooldownTicks, effects);
            definition.AmmoCapacity = ammoCapacity;
            return definition;
        }

        private static BuqiEffectSpec Spec(
            BuqiTrigger trigger,
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            string reason,
            int durationTicks = 30)
        {
            return new BuqiEffectSpec
            {
                Trigger = trigger,
                Effect = effect,
                Target = target,
                Amount = amount,
                ReasonCode = reason,
                DurationTicks = durationTicks,
            };
        }

        private static BuqiEffectSpec CriticalSpec(
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            string reason,
            int chanceBps,
            int durationTicks = 30,
            BuqiTrigger trigger = BuqiTrigger.OnBattleStart)
        {
            BuqiEffectSpec spec = Spec(trigger, effect, target, amount, reason, durationTicks);
            spec.CriticalChanceBps = chanceBps;
            return spec;
        }

        private static BuildSnapshot ContractSnapshot(
            string id,
            int execution,
            int buffer,
            params ItemInstance[] items)
        {
            return BuqiTestSuite.Snapshot(id, execution, buffer, items);
        }

        private static BattleRequest ContractRequest(
            BuildSnapshot left,
            BuildSnapshot right,
            ulong seed)
        {
            BattleRequest request = BuqiTestSuite.Request(left, right);
            request.BattleSeed = seed;
            return request;
        }

        private static int FirstUseTick(
            IItemDefinitionProvider provider,
            string definitionId,
            string reason)
        {
            BattleRequest request = ContractRequest(
                ContractSnapshot("L", 1000, 0, BuqiTestSuite.Item("actor", definitionId, 0)),
                ContractSnapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive-contract", 0)), 2);
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
            return FindFirstActorReasonTick(log, "actor", reason);
        }

        private static bool HasModifier(SideState side, BuqiEffect effect)
        {
            foreach (TimedModifier modifier in side.SideModifiers)
            {
                if (modifier.Effect == effect)
                    return true;
            }
            foreach (ItemState item in side.Items)
            {
                foreach (TimedModifier modifier in item.Modifiers)
                {
                    if (modifier.Effect == effect)
                        return true;
                }
            }
            return false;
        }

        private static BattleEvent FindEvent(
            List<BattleEvent> log,
            string reason,
            int tick)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ReasonCode == reason)
                    return battleEvent;
            }
            return null;
        }

        private static BattleEvent FindActorEvent(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick,
            BuqiEventType type)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick && battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason && battleEvent.Type == type)
                {
                    return battleEvent;
                }
            }
            return null;
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
                    failures.Add("确定性：重复模拟改变了结果或哈希");
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
                failures.Add("相邻关系：空位未阻断相邻响应");

            Simulate(vectors, "adjacency-chain", provider, out List<BattleEvent> adjacentLog, failures);
            if (CountReason(adjacentLog, "adjacent-response") == 0)
                failures.Add("相邻关系：相接装备未触发相邻响应");
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
                failures.Add("就绪使用：一件就绪装备错误触发了另一件装备的 OnUse");
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
                failures.Add("聚合结算：第 0 时刻的护体未吸收同刻普通伤害");
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
            if (accidentCount != 1 || SumReasonAtTick(log, "NoiseAccident", firstNoiseTick) != 16)
                failures.Add("失衡：21 点失衡值未聚合为两次事故的 16 点伤害");
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
                failures.Add("治疗/再生：道基未从受伤初始值恢复");
            if (CountReason(log, "Heal") == 0)
                failures.Add("治疗：未记录直接治疗");
            if (CountReason(log, "Regen") == 0)
                failures.Add("再生：未记录周期治疗");
        }

        private static void CheckPoisonBypassesShield(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("poison", "poison", 0)),
                BuqiTestSuite.Snapshot("R", 100, 60, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);

            if (right.Execution >= 100)
                failures.Add("中毒：未降低道基");
            if (right.Buffer != 60)
                failures.Add("中毒：本应绕过护体却消耗了护体");
            if (CountReason(log, "PoisonDamage") == 0)
                failures.Add("中毒：未记录中毒伤害");
        }

        private static void CheckBurnUsesShield(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("burn", "burn", 0)),
                BuqiTestSuite.Snapshot("R", 100, 60, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out SideState right);

            if (right.Buffer >= 60)
                failures.Add("灼烧：未优先消耗护体");
            if (CountReason(log, "BurnDamage") == 0 && CountReason(log, "BurnShieldAbsorb") == 0)
                failures.Add("灼烧：未记录灼烧伤害或护体吸收");
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
                failures.Add("冻结：被冻结的敌方装备未损失冷却进度");
            if (CountReason(freezeLog, "FreezeApplied") == 0)
                failures.Add("冻结：未记录冻结施加事件");
        }

        private static void CheckCriticalDamage(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest plain = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("critical", "critical", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(plain, provider, out List<BattleEvent> plainLog, out _, out _);
            if (SumActorDeclaredAtTick(plainLog, "critical", "critical-strike", 0) != 20 ||
                CountReasonAtTick(plainLog, "CriticalApplied", 0) != 1)
            {
                failures.Add("暴击：配置倍率未确定性放大伤害或缺少日志");
            }

            plain.Left.Items[0].AnnotationId = "A-05";
            BuqiBattleSimulator.Simulate(plain, provider, out List<BattleEvent> annotatedLog, out _, out _);
            if (SumActorDeclaredAtTick(annotatedLog, "critical", "critical-strike", 0) != 17)
                failures.Add("暴击：未与既有铭刻倍率按整数规则组合");
        }

        private static void CheckCriticalOverflow(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest overflow = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot(
                    "L", 10000, 0,
                    BuqiTestSuite.Item("critical-overflow", "critical-overflow", 0, "A-02")),
                BuqiTestSuite.Snapshot("R", 10000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(overflow, provider, out List<BattleEvent> overflowLog, out _, out _);
            int overflowDamage = SumActorDeclaredAtTick(
                overflowLog, "critical-overflow", "critical-overflow", 35);
            if (overflowDamage != 1300)
            {
                failures.Add(BuqiText.Format(
                    "Critical: maximum configured multiplier expected 1300 damage, actual {0}",
                    overflowDamage));
            }
        }

        private static void CheckSaturatedDamage(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest scaled = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("scaled", "saturated-flight", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BattleResult scaledResult = BuqiBattleSimulator.Simulate(
                scaled, provider, out List<BattleEvent> scaledLog, out _, out _);
            if (scaledResult.Outcome != BattleOutcome.LeftWin || scaledResult.DurationTicks != 10 ||
                SumActorDeclaredAtTick(
                    scaledLog, "scaled", "saturated-flight-strike", 9) != int.MaxValue)
            {
                failures.Add("Damage: post-saturation flight scaling overflowed the integer result");
            }

            BattleRequest repeated = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("multi", "saturated-multi", 0)),
                BuqiTestSuite.Snapshot("R", 100, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BattleResult repeatedResult = BuqiBattleSimulator.Simulate(
                repeated, provider, out _, out _, out SideState repeatedRight);
            if (repeatedResult.Outcome != BattleOutcome.LeftWin || repeatedResult.DurationTicks != 1 ||
                repeatedRight.Execution != int.MinValue)
            {
                failures.Add("Damage: repeated saturated settlements wrapped execution above zero");
            }
        }

        private static void CheckMultiSettlementAndCap(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0,
                    BuqiTestSuite.Item("multi", "multi-ammo", 0),
                    BuqiTestSuite.Item("response", "adjacent-response", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);
            if (CountActorDeclarationsAtTick(log, "multi", "multi-strike", 9) != 3 ||
                CountActorDeclarationsAtTick(log, "response", "adjacent-response", 9) != 1)
            {
                failures.Add("多重：一次主动未独立结算三次，或错误重复触发相邻链");
            }
            if (CountReasonAtTick(log, "AmmoConsumed", 9) != 1)
                failures.Add("多重：一次主动的多次结算错误消耗了多发弹药");

            BattleRequest capped = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("cap", "multi-cap", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(capped, provider, out List<BattleEvent> cappedLog, out _, out _);
            if (CountActorDeclarationsAtTick(cappedLog, "cap", "multi-cap", 0) != 4 ||
                CountReasonAtTick(cappedLog, "PerItemLoopCapReached", 0) != 1)
            {
                failures.Add("多重：重复结算未接入单物品事件上限与截断日志");
            }
        }

        private static void CheckAmmoLifecycle(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest exhausted = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("limited", "ammo-limited", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(
                exhausted, provider, out List<BattleEvent> exhaustedLog, out SideState exhaustedLeft, out _);
            if (CountActorDeclarations(exhaustedLog, "limited", "ammo-shot") != 1 ||
                exhaustedLeft.Items[0].AmmoRemaining != 0 || exhaustedLeft.Items[0].IsEnabled)
            {
                failures.Add("弹药：最后一发后物品未停用或仍继续主动触发");
            }

            BattleRequest refilled = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0,
                    BuqiTestSuite.Item("limited", "ammo-limited", 0),
                    BuqiTestSuite.Item("refill", "ammo-refill", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(refilled, provider, out List<BattleEvent> refillLog, out _, out _);
            if (CountActorDeclarations(refillLog, "limited", "ammo-shot") < 2 ||
                CountReason(refillLog, "AmmoRefilled") == 0)
            {
                failures.Add("弹药：补充效果未恢复已停用物品的后续主动使用");
            }
        }

        private static void CheckCappedAmmo(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest capped = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 1, BuqiTestSuite.Item("capped", "ammo-capped", 0)),
                BuqiTestSuite.Snapshot("R", 4, 0, BuqiTestSuite.Item("breaker", "buffer-breaker", 0)));
            BattleResult cappedResult = BuqiBattleSimulator.Simulate(
                capped, provider, out List<BattleEvent> cappedLog, out SideState cappedLeft, out _);
            if (cappedResult.DurationTicks != 20 ||
                CountReasonAtTick(cappedLog, "AmmoConsumed", 19) != 0 ||
                cappedLeft.Items[0].AmmoRemaining != 1 || !cappedLeft.Items[0].IsEnabled)
            {
                failures.Add("Ammo: a capped active declaration consumed ammo before settlement acceptance");
            }
        }

        private static void CheckCappedAmmoReplay(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 1, BuqiTestSuite.Item("capped", "ammo-capped", 0)),
                BuqiTestSuite.Snapshot("R", 4, 0, BuqiTestSuite.Item("breaker", "buffer-breaker", 0)));
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out _, out _);
            var replay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = result,
                Log = log,
                Definitions = provider,
            });
            replay.SkipToResult();
            BattleReplayItemFrame item = FindReplayItem(replay.Frame.Left, "capped");
            if (item == null || item.AmmoRemaining != 1 || !item.IsEnabled)
                failures.Add("Replay: capped ammo use diverged from simulator state");
        }

        private static void CheckFlightLifecycle(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest timed = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("flight", "flight", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(timed, provider, out List<BattleEvent> timedLog, out _, out _);
            if (CountReasonAtTick(timedLog, "FlightStarted", 0) != 1 ||
                SumActorDeclaredAtTick(timedLog, "flight", "flight-strike", 9) != 15 ||
                CountReasonAtTick(timedLog, "FlightEnded", 11) != 1 ||
                SumReasonAtTick(timedLog, "FlightEndDamage", 11) != 7)
            {
                failures.Add("飞行：进入、飞行增益、自然停飞或停飞伤害语义不完整");
            }

            BattleRequest explicitLeave = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("flight", "flight-leave", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(explicitLeave, provider, out List<BattleEvent> leaveLog, out _, out _);
            if (CountReasonAtTick(leaveLog, "FlightEnded", 9) != 1 ||
                SumReasonAtTick(leaveLog, "FlightEndDamage", 9) != 7)
            {
                failures.Add("飞行：显式停飞未结束状态或结算停飞伤害");
            }
        }

        private static void CheckFlightRefreshSource(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest refreshed = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("strong", "flight-source-strong", 0),
                    BuqiTestSuite.Item("weak", "flight-source-weak", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(refreshed, provider, out List<BattleEvent> refreshedLog, out _, out _);
            if (CountActorReasonAtTick(refreshedLog, "strong", "FlightEndDamage", 20) != 1 ||
                SumReasonAtTick(refreshedLog, "FlightEndDamage", 20) != 9)
            {
                failures.Add("Flight: a weaker refresh replaced the retained end-damage source");
            }
        }

        private static void CheckSingleFlightExit(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest repeated = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot(
                    "L", 100, 0, BuqiTestSuite.Item("flight", "flight-leave-repeat", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(repeated, provider, out List<BattleEvent> repeatedLog, out _, out _);
            if (CountReasonAtTick(repeatedLog, "FlightEndDamage", 9) != 1 ||
                SumReasonAtTick(repeatedLog, "FlightEndDamage", 9) != 7)
            {
                failures.Add("Flight: repeated leave declarations applied end damage more than once");
            }

            BattleRequest rewritten = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot(
                    "L", 100, 0, BuqiTestSuite.Item("flight", "flight-leave", 0, "A-03")),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BuqiBattleSimulator.Simulate(rewritten, provider, out List<BattleEvent> rewriteLog, out _, out _);
            if (CountReasonAtTick(rewriteLog, "FlightEndDamage", 9) != 1 ||
                SumReasonAtTick(rewriteLog, "FlightEndDamage", 9) != 7)
            {
                failures.Add("Flight: A-03 rewritten leave applied end damage more than once");
            }
        }

        private static void CheckFlightControlMitigation(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest freeze = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("flight", "flight-long", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("freeze", "freeze", 0)));
            BuqiBattleSimulator.Simulate(freeze, provider, out List<BattleEvent> freezeLog, out _, out _);
            if (SumReasonAtTick(freezeLog, "FreezeApplied", 39) != 5 ||
                SumReasonAtTick(freezeLog, "FlightFreezeMitigation", 39) != 5)
            {
                failures.Add("飞行：冻结持续时间未按 50% 免疫向上减半");
            }

            BattleRequest delay = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("flight", "flight-long", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("delay", "delay-odd", 0)));
            BuqiBattleSimulator.Simulate(delay, provider, out List<BattleEvent> delayLog, out _, out _);
            if (SumReasonAtTick(delayLog, "FlightDelayMitigation", 29) != 4)
                failures.Add("飞行：迟滞持续时间未按 50% 免疫向上减半");
        }

        private static void CheckNewMechanicReplayProjection(
            IItemDefinitionProvider provider,
            List<string> failures)
        {
            BattleRequest ammoRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("limited", "ammo-limited", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BattleResult ammoResult = BuqiBattleSimulator.Simulate(
                ammoRequest, provider, out List<BattleEvent> ammoLog, out _, out _);
            var ammoReplay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = ammoRequest.Left,
                RightBuild = ammoRequest.Right,
                Result = ammoResult,
                Log = ammoLog,
                Definitions = provider,
            });
            ammoReplay.SkipToResult();
            BattleReplayItemFrame ammoFrame = FindReplayItem(ammoReplay.Frame.Left, "limited");
            if (ammoFrame == null || ammoFrame.AmmoRemaining != 0 || ammoFrame.IsEnabled ||
                !string.IsNullOrEmpty(ammoReplay.Frame.Error))
            {
                failures.Add("回放：未从日志重建弹药耗尽与停用状态");
            }

            BattleRequest flightRequest = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0, BuqiTestSuite.Item("flight", "flight", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BattleResult flightResult = BuqiBattleSimulator.Simulate(
                flightRequest, provider, out List<BattleEvent> flightLog, out _, out _);
            var flightReplay = new BattleReplayController(new BattleReplayData
            {
                LeftBuild = flightRequest.Left,
                RightBuild = flightRequest.Right,
                Result = flightResult,
                Log = flightLog,
                Definitions = provider,
            });
            flightReplay.Advance(0.5f);
            if (!flightReplay.Frame.Left.IsFlying || flightReplay.Frame.Left.FlyingTicks != 6)
                failures.Add("回放：飞行剩余时长未随回放时刻推进");
            flightReplay.SkipToResult();
            if (flightReplay.Frame.Left.IsFlying || !string.IsNullOrEmpty(flightReplay.Frame.Error))
                failures.Add("回放：停飞状态或最终战斗投影不一致");
        }

        private static void CheckFlightRefreshReplay(IItemDefinitionProvider provider, List<string> failures)
        {
            BattleRequest request = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 100, 0,
                    BuqiTestSuite.Item("strong", "flight-source-strong", 0),
                    BuqiTestSuite.Item("weak", "flight-source-weak", 1)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out _, out _);
            BattleReplayData data = new BattleReplayData
            {
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = result,
                Log = log,
                Definitions = provider,
            };

            var jumped = new BattleReplayController(data);
            jumped.Advance(1f);
            var stepped = new BattleReplayController(data);
            stepped.Advance(0.5f);
            stepped.Advance(0.5f);
            if (jumped.Frame.Left.FlyingTicks != 10 || stepped.Frame.Left.FlyingTicks != 10)
                failures.Add("Replay: flight refresh duration drifted during jump or incremental playback");

            bool hasFlightEndFeedback = false;
            foreach (BattleReplayFeedbackEvent feedback in jumped.FeedbackEvents)
            {
                if (feedback.Kind == BattleReplayFeedbackKind.Damage &&
                    feedback.Side == BattleReplayFeedbackSide.Left && feedback.Value == 9)
                {
                    hasFlightEndFeedback = true;
                    break;
                }
            }
            if (!hasFlightEndFeedback)
                failures.Add("Replay: flight-end damage did not create damage feedback");
        }

        private static BattleReplayItemFrame FindReplayItem(BattleReplaySideFrame side, string instanceId)
        {
            foreach (BattleReplayItemFrame item in side.Items)
            {
                if (item.InstanceId == instanceId)
                    return item;
            }
            return null;
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
                failures.Add("A-03：首次直接效果不是 100% + 50%");
            if (adjacentResponses != 1)
                failures.Add("A-03：复写错误地再次触发了相邻响应");
        }

        private static void CheckReliable(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "reliable", provider, out List<BattleEvent> log, failures);
            if (CountReason(log, "A04Immune") == 0)
                failures.Add("A-04：有效的敌方延迟未记录为免疫");
            if (CountReason(log, "interfered-response") != 0)
                failures.Add("A-04：被免疫的延迟错误触发了 OnFirstInterfered");
        }

        private static void CheckUseCount(
            IItemDefinitionProvider provider,
            List<BuqiTestVector> vectors,
            List<string> failures)
        {
            Simulate(vectors, "use-count", provider, out List<BattleEvent> log, failures);
            int burstTick = FindFirstReasonTick(log, "count-burst");
            if (burstTick != 29)
                failures.Add("使用次数：第三次使用未在第 29 时刻触发");
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
                failures.Add("A-01：冷却未按取整规则降低 15%");
            if (left.Items[1].EffectiveBaseCooldownTicks != 36)
                failures.Add("A-02：冷却未增加 20%");
            int a02Tick = FindFirstActorReasonTick(log, "a02", "strike");
            if (SumActorDeclaredAtTick(log, "a02", "strike", a02Tick) != 13)
                failures.Add("A-02：非开局效果未增加 30%");
            int a05Tick = FindFirstActorReasonTick(log, "a05", "noise");
            if (SumActorDeclaredAtTick(log, "a05", "noise", a05Tick) != 21 ||
                SumActorReasonAtTick(log, "a05", "NoiseChange", a05Tick) != 20)
            {
                failures.Add("A-05：失衡来源数值未准确减少 1");
            }
            if (SumActorReasonAtTick(log, "a06", "NoiseChange", 0) != 3)
                failures.Add("A-06：开局失衡未在第 0 时刻施加");
            int a06Tick = FindFirstActorReasonTick(log, "a06", "strike");
            if (SumActorDeclaredAtTick(log, "a06", "strike", a06Tick) != 14)
                failures.Add("A-06：伤害未按取整规则增加 35%");

            BattleRequest quality = BuqiTestSuite.Request(
                BuqiTestSuite.Snapshot("L", 1000, 0, BuqiTestSuite.Item("quality", "damage", 0)),
                BuqiTestSuite.Snapshot("R", 1000, 0, BuqiTestSuite.Item("target", "passive", 0)));
            quality.Left.Items[0].Quality = (int)BuqiQuality.Improved;
            BuqiBattleSimulator.Simulate(quality, provider, out List<BattleEvent> qualityLog, out _, out _);
            int qualityTick = FindFirstActorReasonTick(qualityLog, "quality", "strike");
            if (SumActorDeclaredAtTick(qualityLog, "quality", "strike", qualityTick) != 16)
                failures.Add("品质：改良品质倍率不是 1.60");
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
                failures.Add("快照：规范哈希受输入装备顺序影响");
            }

            IItemDefinitionProvider tempoProvider = CreateContractProvider(
                new BuqiBattleRuleConfig(),
                Definition("temporary-tempo", 20,
                    Spec(BuqiTrigger.OnUse, BuqiEffect.Damage,
                        BuqiTarget.EnemyExecution, 1, "temporary-use")),
                Definition("passive-contract", 1000));
            BuildSnapshot tempoFirst = ContractSnapshot("tempo", 1000, 0,
                BuqiTestSuite.Item("tempo", "temporary-tempo", 0));
            BuildSnapshot tempoSecond = ContractSnapshot("tempo", 1000, 0,
                BuqiTestSuite.Item("tempo", "temporary-tempo", 0));
            tempoFirst.Items[0].TemporaryModifiers.Add(new TemporaryModifier
            {
                Effect = BuqiEffect.Haste,
                SourceInstanceId = "temporary-source",
                RemainingTicks = 100,
                Bps = 1000,
            });
            tempoFirst.Items[0].TemporaryModifiers.Add(new TemporaryModifier
            {
                Effect = BuqiEffect.Delay,
                SourceInstanceId = "temporary-source",
                RemainingTicks = 100,
                Bps = 1000,
            });
            tempoSecond.Items[0].TemporaryModifiers.Add(tempoFirst.Items[0].TemporaryModifiers[1]);
            tempoSecond.Items[0].TemporaryModifiers.Add(tempoFirst.Items[0].TemporaryModifiers[0]);
            BuildSnapshot passiveFirst = ContractSnapshot("target", 1000, 0,
                BuqiTestSuite.Item("target", "passive-contract", 0));
            BuildSnapshot passiveSecond = ContractSnapshot("target", 1000, 0,
                BuqiTestSuite.Item("target", "passive-contract", 0));
            BattleResult tempoResultFirst = BuqiBattleSimulator.Simulate(
                ContractRequest(tempoFirst, passiveFirst, 77), tempoProvider,
                out List<BattleEvent> tempoLogFirst, out _, out _);
            BattleResult tempoResultSecond = BuqiBattleSimulator.Simulate(
                ContractRequest(tempoSecond, passiveSecond, 77), tempoProvider,
                out List<BattleEvent> tempoLogSecond, out _, out _);
            if (BuqiCrypto.SnapshotHash(tempoFirst) != BuqiCrypto.SnapshotHash(tempoSecond) ||
                tempoResultFirst.BattleLogHash != tempoResultSecond.BattleLogHash ||
                FindFirstActorReasonTick(tempoLogFirst, "tempo", "temporary-use") !=
                FindFirstActorReasonTick(tempoLogSecond, "tempo", "temporary-use"))
            {
                failures.Add("Snapshot determinism: temporary modifier order must not change the hash or battle result");
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
                failures.Add("循环上限：同一装备的第五个事件未被截断");
            if (CountReason(log, "PerItemLoopCapReached") != 1)
                failures.Add("循环上限：每件装备的截断未准确记录一次");
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
                failures.Add("镜像：交换输入后未产生镜像结果");
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
                failures.Add(BuqiText.Format("缺少向量：{0}", id));
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
                failures.Add(BuqiText.Format("{0}：期望 {1}，实际为 {2}", label, expected, result.Outcome));
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

        private static int CountActorDeclarationsWithAmountAtTick(
            List<BattleEvent> log,
            string actorId,
            string reason,
            int tick,
            int amount)
        {
            int count = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick == tick &&
                    battleEvent.ActorInstanceId == actorId &&
                    battleEvent.ReasonCode == reason &&
                    battleEvent.Type == BuqiEventType.Declare &&
                    battleEvent.Amount == amount)
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
