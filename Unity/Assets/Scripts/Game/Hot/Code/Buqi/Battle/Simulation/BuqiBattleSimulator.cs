using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>Declare 阶段产生的稳定效果声明；此时不直接修改双方执行值或护体。</summary>
    internal sealed class DeclaredEffect
    {
        public ItemState Actor;
        public BuqiEffectSpec Spec;
        public int ChainDepth;
        public string ChainId = string.Empty;
        public bool IsRewrite;
        /// <summary>本批 Declare 队列中的插入顺序；同来源声明按契约以 sequence 决定先后。</summary>
        public int DeclarationOrder;
        /// <summary>多重结算中的零基结算序号。</summary>
        public int RepeatIndex;
        public bool ConsumesAmmo;
    }

    /// <summary>等待 Aggregate 阶段统一结算的整数数值及其日志来源。</summary>
    internal sealed class PendingAmount
    {
        public int Amount;
        public int SourceAnchorSlot;
        public string SourceInstanceId = string.Empty;
        public string ChainId = string.Empty;
        public string EffectId = string.Empty;
        public string ReasonCode = string.Empty;
    }

    /// <summary>等待 Aggregate 阶段写入运行时状态的加速或延迟修正。</summary>
    internal sealed class PendingModifier
    {
        public BuqiEffect Effect;
        public int Bps;
        public int DurationTicks;
        public string SourceInstanceId = string.Empty;
        public bool FromEnemy;
        public List<ItemState> TargetItems = new List<ItemState>();
    }

    internal sealed class PendingFreeze
    {
        public int DurationTicks;
        public int SourceAnchorSlot;
        public string SourceInstanceId = string.Empty;
        public string ChainId = string.Empty;
        public string EffectId = string.Empty;
        public List<ItemState> TargetItems = new List<ItemState>();
    }

    internal sealed class PendingFlight
    {
        public bool Enter;
        public int DurationTicks;
        public int DamageBonusBps;
        public int EndDamage;
        public int SourceAnchorSlot;
        public string SourceInstanceId = string.Empty;
        public string ChainId = string.Empty;
        public string EffectId = string.Empty;
    }

    internal sealed class PendingRage
    {
        public int Amount;
        public int Threshold;
        public int DurationTicks;
        public int CooldownReductionBps;
        public int SourceAnchorSlot;
        public string SourceInstanceId = string.Empty;
        public string ChainId = string.Empty;
        public string EffectId = string.Empty;
    }

    /// <summary>
    /// 单阵营单 tick 的结算桶。Resolve 只向桶内声明，Aggregate 再按固定顺序修改状态。
    /// </summary>
    internal sealed class TickAccumulator
    {
        public List<PendingAmount> Buffer = new List<PendingAmount>();
        public List<PendingAmount> NormalDamage = new List<PendingAmount>();
        public List<PendingAmount> Heal = new List<PendingAmount>();
        public List<PendingAmount> Noise = new List<PendingAmount>();
        public List<TimedStatus> NewStatuses = new List<TimedStatus>();
        public List<PendingFreeze> Freezes = new List<PendingFreeze>();
        public List<PendingModifier> Modifiers = new List<PendingModifier>();
        public List<PendingFlight> Flights = new List<PendingFlight>();
        public List<PendingRage> Rage = new List<PendingRage>();
    }

    /// <summary>
    /// 《不器》战斗契约 v0.6 的确定性纯 C# 模拟器。
    /// 1 tick = 100 ms；不读取 Time、Unity Random、场景或网络，Unity 与无头端直接编译同一份源码。
    /// </summary>
    public static class BuqiBattleSimulator
    {
        /// <summary>1 秒包含 10 个基础 tick；默认从 tick 300 起进入沙暴。</summary>
        public const int TicksPerSecond = 10;
        public const int StormStartTicks = 300;
        public const int StormBaseDamage = 1;
        public const int StormRampDamage = 1;
        public const int BufferCap = 60;
        public const int NoiseThreshold = 10;
        public const int NoiseAccidentDamage = 8;
        public const int DefaultMaxExecution = 100;
        public const int StatusTickIntervalTicks = TicksPerSecond;

        /// <summary>防止全场触发链异常膨胀的单 tick 上限。</summary>
        public const int MaxEventsPerTick = 64;

        /// <summary>防止单个法门在同 tick 自循环的声明上限。</summary>
        public const int MaxEventsPerItemPerTick = 4;

        /// <summary>冷却与 basis points 共用的整数基准 10000。</summary>
        public const int CooldownUnit = 10000;
        public const string RuleVersion = "0.6.0";
        public const string SimulationVersion = "battle-core-0.6.0";

        /// <summary>
        /// 模拟一场战斗并返回结果、完整稳定日志和双方最终运行时状态。
        /// 非法请求不会抛出业务异常，而是返回 InvalidBuild 并生成可重复的空日志哈希。
        /// </summary>
        public static BattleResult Simulate(
            BattleRequest request,
            IItemDefinitionProvider provider,
            out List<BattleEvent> log,
            out SideState leftFinal,
            out SideState rightFinal)
        {
            log = new List<BattleEvent>();
            leftFinal = null;
            rightFinal = null;

            BattleResult result = CreateInitialResult(request);
            if (!ValidateRequest(request, provider))
            {
                PopulateInvalidResult(result, request);
                result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
                return result;
            }

            SideState left = BuildSide(request.Left, provider);
            SideState right = BuildSide(request.Right, provider);
            var accumulators = new Dictionary<SideState, TickAccumulator>
            {
                [left] = new TickAccumulator(),
                [right] = new TickAccumulator(),
            };
            int nextSequence = 0;
            BattleOutcome outcome = BattleOutcome.Draw;
            string terminationReason = string.Empty;
            int durationTicks = 0;
            BuqiBattleRuleConfig rules = ResolveBattleRules(provider);

            for (int tick = 0; ; tick++)
            {
                int processedEvents = 0;
                bool loopCapLogged = false;
                var perItemCount = new Dictionary<string, int>(StringComparer.Ordinal);
                var interferedSides = new HashSet<SideState>();
                var queue = new List<DeclaredEffect>();
                ResetActiveUseCounters(left);
                ResetActiveUseCounters(right);

                // tick 0 的开战声明必须存在于首次冷却推进之前：先声明双方 OnBattleStart，
                // 再推进冷却和声明主动使用，避免开战效果被首次 ready 使用抢先结算。
                if (tick == 0)
                {
                    EnqueueTriggerForSide(left, provider, BuqiTrigger.OnBattleStart, queue, "battle-start", 0, false);
                    EnqueueTriggerForSide(right, provider, BuqiTrigger.OnBattleStart, queue, "battle-start", 0, false);
                    EnqueueOpeningNoise(left, accumulators[left]);
                    EnqueueOpeningNoise(right, accumulators[right]);
                }

                ApplyStatusTicks(left, accumulators[left], ref nextSequence, log, tick);
                ApplyStatusTicks(right, accumulators[right], ref nextSequence, log, tick);
                AdvanceFlight(left, accumulators[left], ref nextSequence, log, tick);
                AdvanceFlight(right, accumulators[right], ref nextSequence, log, tick);
                AdvanceCooldowns(left);
                AdvanceCooldowns(right);
                AdvanceRage(left, ref nextSequence, log, tick);
                AdvanceRage(right, ref nextSequence, log, tick);
                ExpireModifiers(left);
                ExpireModifiers(right);
                EnqueuePendingConditions(left, provider, queue, tick);
                EnqueuePendingConditions(right, provider, queue, tick);
                EnqueueReadyUses(left, provider, queue, tick);
                EnqueueReadyUses(right, provider, queue, tick);

                SortQueue(queue);
                ProcessQueue(
                    queue, left, right, accumulators, provider, interferedSides,
                    perItemCount, ref processedEvents, ref nextSequence, log, tick,
                    request.BattleSeed, ref loopCapLogged);

                var responseQueue = new List<DeclaredEffect>();
                if (interferedSides.Contains(left))
                    EnqueueFirstInterfered(left, provider, responseQueue, tick);
                if (interferedSides.Contains(right))
                    EnqueueFirstInterfered(right, provider, responseQueue, tick);
                SortQueue(responseQueue);
                ProcessQueue(
                    responseQueue, left, right, accumulators, provider, interferedSides,
                    perItemCount, ref processedEvents, ref nextSequence, log, tick,
                    request.BattleSeed, ref loopCapLogged);

                // 双方的 Declare/Resolve/Chain 全部结束后才分别聚合；在此之前任何一方都不能因先执行而提前结束战斗。
                ApplyAggregate(left, accumulators[left], ref nextSequence, log, tick);
                ApplyAggregate(right, accumulators[right], ref nextSequence, log, tick);
                ResetAccumulator(accumulators[left]);
                ResetAccumulator(accumulators[right]);

                if (left.Execution <= 0 || right.Execution <= 0)
                {
                    outcome = DetermineOutcome(left, right);
                    terminationReason = TerminationReason.Normal.ToString();
                    durationTicks = tick + 1;
                    SortAndResequenceLog(log);
                    break;
                }

                if (tick >= rules.StormStartTicks)
                {
                    ApplyStorm(left, rules, tick, ref nextSequence, log);
                    ApplyStorm(right, rules, tick, ref nextSequence, log);
                    if (left.Execution <= 0 || right.Execution <= 0)
                    {
                        outcome = DetermineOutcome(left, right);
                        terminationReason = TerminationReason.Storm.ToString();
                        durationTicks = tick + 1;
                        SortAndResequenceLog(log);
                        break;
                    }
                }

                SortAndResequenceLog(log);
                nextSequence = log.Count;
            }

            result.Outcome = outcome;
            result.TerminationReason = terminationReason;
            result.DurationTicks = durationTicks;
            result.LeftExecution = left.Execution;
            result.RightExecution = right.Execution;
            result.LeftBuffer = left.Buffer;
            result.RightBuffer = right.Buffer;
            result.LeftNoise = left.Noise;
            result.RightNoise = right.Noise;
            result.BattleLogHash = BuqiCrypto.BattleLogHash(result, log);
            leftFinal = left;
            rightFinal = right;
            return result;
        }

        private static BattleResult CreateInitialResult(BattleRequest request)
        {
            return new BattleResult
            {
                RuleVersion = RuleVersion,
                SimulationVersion = SimulationVersion,
                ContentVersion = request?.Left?.ContentVersion ?? string.Empty,
                BattleSeed = request?.BattleSeed ?? 0,
                RoundIndex = request?.RoundIndex ?? 0,
                LeftSnapshotHash = BuqiCrypto.SnapshotHash(request == null ? null : request.Left),
                RightSnapshotHash = BuqiCrypto.SnapshotHash(request == null ? null : request.Right),
            };
        }

        /// <summary>
        /// 校验规则版本、双方快照、内容版本和跨阵营实例 ID 唯一性。
        /// 跨阵营唯一性是稳定日志与来源索引的前提，不能只在单侧校验。
        /// </summary>
        private static bool ValidateRequest(BattleRequest request, IItemDefinitionProvider provider)
        {
            if (request == null || provider == null)
                return false;
            if (!string.Equals(request.RuleVersion, RuleVersion, StringComparison.Ordinal))
                return false;
            if (!BuqiBoardValidator.Validate(request.Left, provider, out _))
                return false;
            if (!BuqiBoardValidator.Validate(request.Right, provider, out _))
                return false;
            if (!string.Equals(request.Left.ContentVersion, request.Right.ContentVersion, StringComparison.Ordinal))
                return false;

            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemInstance item in request.Left.Items)
                instanceIds.Add(item.InstanceId);
            foreach (ItemInstance item in request.Right.Items)
            {
                if (!instanceIds.Add(item.InstanceId))
                    return false;
            }
            return true;
        }

        private static void PopulateInvalidResult(BattleResult result, BattleRequest request)
        {
            result.Outcome = BattleOutcome.InvalidBuild;
            result.TerminationReason = TerminationReason.InvalidBuild.ToString();
            result.DurationTicks = 0;
            result.LeftExecution = request?.Left?.InitialExecution ?? 0;
            result.RightExecution = request?.Right?.InitialExecution ?? 0;
            result.LeftBuffer = request?.Left?.InitialBuffer ?? 0;
            result.RightBuffer = request?.Right?.InitialBuffer ?? 0;
            result.LeftNoise = request?.Left?.InitialNoiseDebt ?? 0;
            result.RightNoise = request?.Right?.InitialNoiseDebt ?? 0;
        }

        private static SideState BuildSide(BuildSnapshot snapshot, IItemDefinitionProvider provider)
        {
            var side = new SideState
            {
                Execution = snapshot.InitialExecution,
                MaxExecution = Math.Max(DefaultMaxExecution, snapshot.InitialExecution),
                Buffer = snapshot.InitialBuffer,
                Noise = snapshot.InitialNoiseDebt,
            };
            var items = new List<ItemInstance>(snapshot.Items);
            items.Sort(CompareItemInstances);
            foreach (ItemInstance instance in items)
            {
                provider.TryGet(instance.DefinitionId, out BuqiItemDefinition definition);
                IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(instance.AnnotationId);
                int baseCooldownTicks = Math.Max(
                    1,
                    refinement.AdjustBaseCooldownTicks(definition.BaseCooldownTicks));

                var state = new ItemState
                {
                    InstanceId = instance.InstanceId,
                    DefinitionId = instance.DefinitionId,
                    AnnotationId = instance.AnnotationId,
                    Quality = instance.Quality,
                    AnchorSlot = instance.AnchorSlot,
                    Size = definition.Size,
                    EffectiveBaseCooldownTicks = baseCooldownTicks,
                    CooldownProgress = SaturateToInt((long)baseCooldownTicks * CooldownUnit),
                    AmmoCapacity = Math.Max(0, definition.AmmoCapacity),
                    AmmoRemaining = definition.AmmoCapacity > 0 ? definition.AmmoCapacity : -1,
                    IsEnabled = true,
                };
                if (instance.TemporaryModifiers != null)
                {
                    var modifiers = new List<TemporaryModifier>(instance.TemporaryModifiers);
                    modifiers.Sort(BuqiCrypto.CompareTemporaryModifiers);
                    foreach (TemporaryModifier modifier in modifiers)
                    {
                        AddInitialModifier(state.Modifiers, new TimedModifier
                        {
                            Effect = modifier.Effect,
                            Bps = FixedModifierBps(modifier.Effect),
                            RemainingTicks = modifier.RemainingTicks,
                            SourceInstanceId = modifier.SourceInstanceId,
                            FromEnemy = modifier.Effect == BuqiEffect.Delay,
                        });
                    }
                }
                side.Items.Add(state);
            }
            return side;
        }

        private static BuqiBattleRuleConfig ResolveBattleRules(IItemDefinitionProvider provider)
        {
            BuqiBattleRuleConfig rules = provider is IBuqiBattleRuleProvider ruleProvider &&
                                         ruleProvider.BattleRules != null
                ? ruleProvider.BattleRules.Clone()
                : new BuqiBattleRuleConfig();
            rules.StormStartTicks = Math.Max(0, rules.StormStartTicks);
            rules.StormBaseDamage = Math.Max(1, rules.StormBaseDamage);
            rules.StormRampDamage = Math.Max(1, rules.StormRampDamage);
            return rules;
        }

        private static void ResetActiveUseCounters(SideState side)
        {
            foreach (ItemState item in side.Items)
            {
                item.ActiveUsesThisTick = 0;
                item.AmmoReservationsThisTick = 0;
            }
        }

        private static void ApplyStatusTicks(
            SideState side,
            TickAccumulator accumulator,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            bool shouldSettle = tick > 0 && tick % StatusTickIntervalTicks == 0;
            SortStatuses(side.Statuses);

            for (int index = side.Statuses.Count - 1; index >= 0; index--)
            {
                TimedStatus status = side.Statuses[index];
                if (shouldSettle)
                {
                    if (status.Effect == BuqiEffect.Regen)
                    {
                        if (status.Amount > 0)
                        {
                            accumulator.Heal.Add(new PendingAmount
                            {
                                Amount = status.Amount,
                                SourceAnchorSlot = status.SourceAnchorSlot,
                                SourceInstanceId = status.SourceInstanceId,
                                ChainId = status.ChainId,
                                EffectId = status.EffectId,
                                ReasonCode = "Regen",
                            });
                        }
                    }
                    else if (status.Effect == BuqiEffect.Poison)
                    {
                        ApplyExecutionDamage(side, status.Amount);
                        AppendStatusPreTickEvent(
                            ref nextSequence, log, tick, status, status.Amount, "PoisonDamage");
                    }
                    else if (status.Effect == BuqiEffect.Burn)
                    {
                        int damage = side.Buffer > 0 ? 1 : 2;
                        if (side.Buffer > 0)
                        {
                            AppendStatusPreTickEvent(
                                ref nextSequence, log, tick, status, 1, "BurnShieldMitigated");
                        }
                        ApplyExecutionDamage(side, damage);
                        AppendStatusPreTickEvent(
                            ref nextSequence, log, tick, status, damage, "BurnDamage");
                        status.Amount--;
                        AppendStatusPreTickEvent(
                            ref nextSequence, log, tick, status, 1, "BurnLayerDecay");
                    }
                }

                status.RemainingTicks--;
                if (status.RemainingTicks <= 0 || status.Amount <= 0)
                    side.Statuses.RemoveAt(index);
            }
        }

        private static void AdvanceRage(
            SideState side,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            if (side.EnragedTicks <= 0)
                return;
            side.EnragedTicks--;
            if (side.EnragedTicks > 0)
                return;
            side.RageCooldownReductionBps = 0;
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.PreTick, 0,
                BuqiText.Format("enrage@end@{0}", tick), string.Empty, string.Empty,
                string.Empty, BuqiEventType.Effect, 0, string.Empty, "EnrageEnded");
        }

        private static void ApplyStorm(
            SideState side,
            BuqiBattleRuleConfig rules,
            int tick,
            ref int nextSequence,
            List<BattleEvent> log)
        {
            long elapsed = tick - (long)rules.StormStartTicks;
            int damage = SaturateToInt(rules.StormBaseDamage + elapsed * rules.StormRampDamage);
            ApplyExecutionDamage(side, damage);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.PostTick, 0,
                BuqiText.Format("storm@{0}", tick), string.Empty, string.Empty,
                string.Empty, BuqiEventType.Effect, damage, string.Empty, "StormDamage");
        }

        /// <summary>
        /// 在本 tick 的声明前推进冷却，并记录 ReadyThisTick。
        /// A-04 只免疫敌方 Delay 且忽略友方 Haste；A-01/A-02 在构建运行时状态时已修正基础冷却。
        /// </summary>
        private static void AdvanceCooldowns(SideState side)
        {
            foreach (ItemState item in side.Items)
            {
                if (!item.IsEnabled)
                {
                    item.ReadyThisTick = false;
                    continue;
                }
                if (item.FrozenTicks > 0)
                {
                    item.FrozenTicks--;
                    item.ReadyThisTick = false;
                    continue;
                }

                BuqiEffect tempo = ResolveTempo(side, item);
                int advance = tempo == BuqiEffect.Haste
                    ? 20000
                    : tempo == BuqiEffect.Delay ? 5000 : CooldownUnit;
                if (side.EnragedTicks > 0 && side.RageCooldownReductionBps > 0)
                {
                    int denominator = CooldownUnit -
                        Clamp(side.RageCooldownReductionBps, 0, 9999);
                    advance = SaturateToInt(
                        ((long)advance * CooldownUnit + denominator / 2) / denominator);
                }
                item.CooldownProgress = SaturateToInt(
                    (long)item.CooldownProgress - advance);
                item.ReadyThisTick = item.CooldownProgress <= 0;
                if (item.ReadyThisTick)
                    item.CooldownProgress = SaturateToInt(
                        (long)item.CooldownProgress +
                        (long)item.EffectiveBaseCooldownTicks * CooldownUnit);
            }
        }

        private static BuqiEffect ResolveTempo(SideState side, ItemState item)
        {
            if (HasModifier(item.Modifiers, item, BuqiEffect.Haste))
                return BuqiEffect.Haste;
            if (HasModifier(item.Modifiers, item, BuqiEffect.Delay))
                return BuqiEffect.Delay;
            if (HasModifier(side.SideModifiers, item, BuqiEffect.Haste))
                return BuqiEffect.Haste;
            if (HasModifier(side.SideModifiers, item, BuqiEffect.Delay))
                return BuqiEffect.Delay;
            return BuqiEffect.Damage;
        }

        private static bool HasModifier(
            List<TimedModifier> modifiers,
            ItemState item,
            BuqiEffect effect)
        {
            foreach (TimedModifier modifier in modifiers)
            {
                IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(item.AnnotationId);
                if (!refinement.AllowsModifier(modifier.Effect, modifier.FromEnemy))
                    continue;
                if (modifier.Effect == effect)
                    return true;
            }
            return false;
        }

        private static void EnqueueReadyUses(
            SideState side,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> queue,
            int tick)
        {
            foreach (ItemState item in side.Items)
            {
                if (!item.ReadyThisTick)
                    continue;
                EnqueueActiveUse(side, item, provider, queue, tick);
            }
        }

        /// <summary>
        /// 只为当前 ready 的具体法门入队 OnUse，并单独处理其相邻响应、使用次数和 A-03 复写。
        /// 不能为每一张 ready 法门遍历整侧再次触发其它法门的 OnUse。
        /// </summary>
        private static void EnqueueActiveUse(
            SideState side,
            ItemState actor,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> queue,
            int tick,
            string inheritedChainId = null,
            int chainDepth = 0)
        {
            if (!actor.IsEnabled || actor.FrozenTicks > 0)
                return;
            if (actor.AmmoCapacity > 0 &&
                actor.AmmoRemaining <= actor.AmmoReservationsThisTick)
            {
                return;
            }

            provider.TryGet(actor.DefinitionId, out BuqiItemDefinition definition);
            string chainId = string.IsNullOrEmpty(inheritedChainId)
                ? BuqiText.Format("{0}@use@{1}", actor.InstanceId, tick)
                : inheritedChainId;
            actor.ReadyThisTick = false;
            actor.ActiveUsesThisTick++;
            var directDeclarations = new List<DeclaredEffect>();
            foreach (BuqiEffectSpec spec in definition.Effects)
            {
                if (spec.Trigger == BuqiTrigger.OnUse)
                {
                    EnqueueDeclarations(actor, spec, chainId, chainDepth, false, queue, directDeclarations);
                }
            }

            if (actor.AmmoCapacity > 0 && directDeclarations.Count > 0)
            {
                directDeclarations[0].ConsumesAmmo = true;
                actor.AmmoReservationsThisTick++;
            }

            foreach (ItemState adjacent in GetLinearAdjacent(side, actor, provider))
            {
                provider.TryGet(adjacent.DefinitionId, out BuqiItemDefinition adjacentDefinition);
                foreach (BuqiEffectSpec spec in adjacentDefinition.Effects)
                {
                    if (spec.Trigger != BuqiTrigger.OnAdjacentUse)
                        continue;
                    if (!adjacent.IsEnabled)
                        continue;
                    adjacent.AdjacentUseCount++;
                    EnqueueDeclarations(adjacent, spec, chainId, chainDepth + 1, false, queue);
                }
            }

            actor.OwnUseCount++;
            EnqueueUseCountReached(actor, definition, queue, tick);
            // A-03 只复写首次主动使用的直接效果；复写声明深度为 1，且不会再次触发相邻响应。
            IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId);
            if (refinement.RewritesFirstActiveUse && !actor.RewriteUsed)
            {
                actor.RewriteUsed = true;
                foreach (DeclaredEffect source in directDeclarations)
                {
                    DeclaredEffect rewrite = NewDeclaration(actor, source.Spec, chainId, chainDepth + 1, true);
                    rewrite.DeclarationOrder = queue.Count;
                    queue.Add(rewrite);
                }
            }
        }

        private static void EnqueueUseCountReached(
            ItemState actor,
            BuqiItemDefinition definition,
            List<DeclaredEffect> queue,
            int tick)
        {
            foreach (BuqiEffectSpec spec in definition.Effects)
            {
                if (spec.Trigger != BuqiTrigger.OnUseCountReached || spec.UseCountThreshold <= 0)
                    continue;
                if (actor.OwnUseCount < spec.UseCountThreshold)
                    continue;
                EnqueueDeclarations(
                    actor, spec, BuqiText.Format("{0}@count@{1}", actor.InstanceId, tick),
                    1, false, queue);
                if (spec.ResetCountOnReached)
                    actor.OwnUseCount -= spec.UseCountThreshold;
            }
        }

        private static void EnqueueTriggerForSide(
            SideState side,
            IItemDefinitionProvider provider,
            BuqiTrigger trigger,
            List<DeclaredEffect> queue,
            string chainId,
            int depth,
            bool rewrite)
        {
            foreach (ItemState item in side.Items)
            {
                if (!item.IsEnabled)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == trigger)
                        EnqueueDeclarations(item, spec, chainId, depth, rewrite, queue);
                }
            }
        }

        private static void EnqueuePendingConditions(
            SideState side,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> queue,
            int tick)
        {
            if (!side.BufferLostPending)
                return;
            side.BufferLostPending = false;
            foreach (ItemState item in side.Items)
            {
                if (!item.IsEnabled || item.FirstConditionUsed)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                bool hasBufferLostTrigger = false;
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == BuqiTrigger.OnFirstConditionMet &&
                        spec.ConditionKind == BuqiConditionKind.BufferLost)
                    {
                        hasBufferLostTrigger = true;
                        EnqueueDeclarations(
                            item, spec, BuqiText.Format("{0}@condition@{1}", item.InstanceId, tick),
                            0, false, queue);
                    }
                }
                if (hasBufferLostTrigger)
                    item.FirstConditionUsed = true;
            }
        }

        private static void EnqueueFirstInterfered(
            SideState side,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> queue,
            int tick)
        {
            foreach (ItemState item in side.Items)
            {
                if (!item.IsEnabled || item.FirstInterferedUsed)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                bool enqueued = false;
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == BuqiTrigger.OnFirstInterfered)
                    {
                        EnqueueDeclarations(
                            item, spec, BuqiText.Format("{0}@interfered@{1}", item.InstanceId, tick),
                            0, false, queue);
                        enqueued = true;
                    }
                }
                if (enqueued)
                    item.FirstInterferedUsed = true;
            }
        }

        private static DeclaredEffect NewDeclaration(
            ItemState actor,
            BuqiEffectSpec spec,
            string chainId,
            int depth,
            bool rewrite)
        {
            return new DeclaredEffect
            {
                Actor = actor,
                Spec = spec,
                ChainId = chainId,
                ChainDepth = depth,
                IsRewrite = rewrite,
            };
        }

        private static void EnqueueDeclarations(
            ItemState actor,
            BuqiEffectSpec spec,
            string chainId,
            int depth,
            bool rewrite,
            List<DeclaredEffect> queue,
            List<DeclaredEffect> capture = null)
        {
            int repeatCount = Math.Min(MaxEventsPerTick + 1, Math.Max(1, spec.RepeatCount));
            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                DeclaredEffect declaration = NewDeclaration(actor, spec, chainId, depth, rewrite);
                declaration.RepeatIndex = repeatIndex;
                declaration.DeclarationOrder = queue.Count;
                queue.Add(declaration);
                capture?.Add(declaration);
            }
        }

        /// <summary>
        /// 按稳定队列处理声明，并同时执行全场 64 事件与单实例 4 事件截断。
        /// 截断只阻止后续声明，不回滚已经进入本 tick 结算桶的效果。
        /// </summary>
        private static void ProcessQueue(
            List<DeclaredEffect> queue,
            SideState left,
            SideState right,
            Dictionary<SideState, TickAccumulator> accumulators,
            IItemDefinitionProvider provider,
            HashSet<SideState> interferedSides,
            Dictionary<string, int> perItemCount,
            ref int processedEvents,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            ulong battleSeed,
            ref bool loopCapLogged)
        {
            for (int queueIndex = 0; queueIndex < queue.Count; queueIndex++)
            {
                int nextIndex = FindNextDeclarationIndex(queue, queueIndex);
                if (nextIndex != queueIndex)
                {
                    DeclaredEffect swap = queue[queueIndex];
                    queue[queueIndex] = queue[nextIndex];
                    queue[nextIndex] = swap;
                }
                DeclaredEffect declaration = queue[queueIndex];
                if (processedEvents >= MaxEventsPerTick)
                {
                    LogLoopCap(ref nextSequence, log, tick, declaration.ChainId, "LoopCapReached", ref loopCapLogged);
                    break;
                }

                int actorCount = perItemCount.TryGetValue(declaration.Actor.InstanceId, out int currentCount)
                    ? currentCount
                    : 0;
                if (actorCount >= MaxEventsPerItemPerTick)
                {
                    LogLoopCap(ref nextSequence, log, tick, declaration.ChainId, "PerItemLoopCapReached", ref loopCapLogged);
                    continue;
                }

                perItemCount[declaration.Actor.InstanceId] = actorCount + 1;
                processedEvents++;
                ResolveEffect(
                    declaration, left, right, accumulators, provider, interferedSides,
                    ref nextSequence, log, tick, battleSeed, queue);
            }
        }

        private static void ResolveEffect(
            DeclaredEffect declaration,
            SideState left,
            SideState right,
            Dictionary<SideState, TickAccumulator> accumulators,
            IItemDefinitionProvider provider,
            HashSet<SideState> interferedSides,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            ulong battleSeed,
            List<DeclaredEffect> activeQueue)
        {
            ItemState actor = declaration.Actor;
            SideState own = left.Items.Contains(actor) ? left : right;
            SideState enemy = own == left ? right : left;
            BuqiEffectSpec spec = declaration.Spec;
            if (declaration.ConsumesAmmo)
            {
                actor.AmmoRemaining = Math.Max(0, actor.AmmoRemaining - 1);
                actor.IsEnabled = actor.AmmoRemaining > 0;
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, actor.InstanceId,
                    BuqiEventType.Effect, 1, spec.GetEffectId(), "AmmoConsumed");
            }
            ResolvedTargets targets = ResolveTargets(spec.Target, own, enemy, actor, provider);
            if (targets.Side == null && targets.Items.Count == 0)
            {
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, string.Empty,
                    BuqiEventType.NoTarget, 0, spec.GetEffectId(), "NoValidTarget");
                return;
            }

            int amount = CalculateEffectAmount(
                actor,
                own,
                spec,
                declaration.IsRewrite);
            string targetId = targets.Items.Count > 0 ? targets.Items[0].InstanceId : string.Empty;
            if (TryApplyCritical(
                    amount, spec, declaration, battleSeed, tick,
                    out int criticalAmount, ref nextSequence, log, targetId))
            {
                amount = criticalAmount;
            }
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                declaration.ChainId, actor.InstanceId, actor.InstanceId, targetId,
                BuqiEventType.Declare, amount, spec.GetEffectId(), spec.ReasonCode);

            switch (spec.Effect)
            {
                case BuqiEffect.Damage:
                    AddPending(accumulators[targets.Side], amount, actor, declaration, spec, accumulators[targets.Side].NormalDamage);
                    break;
                case BuqiEffect.Buffer:
                    AddPending(accumulators[targets.Side], amount, actor, declaration, spec, accumulators[targets.Side].Buffer);
                    break;
                case BuqiEffect.Heal:
                    AddPending(accumulators[targets.Side], amount, actor, declaration, spec, accumulators[targets.Side].Heal, "Heal");
                    break;
                case BuqiEffect.Regen:
                case BuqiEffect.Poison:
                case BuqiEffect.Burn:
                    AddPendingStatus(accumulators[targets.Side], amount, actor, declaration, spec);
                    break;
                case BuqiEffect.Freeze:
                    QueueFreeze(
                        spec, actor, targets, amount, declaration,
                        accumulators[targets.Side], ref nextSequence, log, tick);
                    break;
                case BuqiEffect.Noise:
                    amount = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId)
                        .AdjustNoiseAmount(amount);
                    AddPending(accumulators[targets.Side], amount, actor, declaration, spec, accumulators[targets.Side].Noise);
                    break;
                case BuqiEffect.Charge:
                    ApplyCharge(
                        targets, actor, amount, declaration, spec, own, enemy, provider,
                        activeQueue, ref nextSequence, log, tick);
                    break;
                case BuqiEffect.Ammo:
                    ApplyAmmo(targets, actor, amount, declaration, spec, ref nextSequence, log, tick);
                    break;
                case BuqiEffect.Flight:
                    QueueFlight(own, actor, amount, declaration, spec, accumulators[own]);
                    break;
                case BuqiEffect.Rage:
                    QueueRage(actor, amount, declaration, spec, accumulators[own]);
                    break;
                case BuqiEffect.Haste:
                case BuqiEffect.Delay:
                    QueueModifier(
                        spec, actor, own, targets, amount, declaration,
                        accumulators[targets.Side], interferedSides,
                        ref nextSequence, log, tick);
                    break;
            }

            IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId);
            if (spec.Trigger == BuqiTrigger.OnUse &&
                !declaration.IsRewrite &&
                refinement.OnUseNoise > 0)
            {
                AddPending(
                    accumulators[own], refinement.OnUseNoise, actor, declaration, spec,
                    accumulators[own].Noise, "A01UseNoise");
            }
        }

        private static void ApplyCharge(
            ResolvedTargets targets,
            ItemState actor,
            int amount,
            DeclaredEffect declaration,
            BuqiEffectSpec spec,
            SideState own,
            SideState enemy,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> activeQueue,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            if (amount <= 0)
                return;
            List<ItemState> targetItems = targets.Items.Count > 0
                ? targets.Items
                : new List<ItemState> { actor };
            foreach (ItemState target in targetItems)
            {
                if (!target.IsEnabled)
                    continue;
                if (target.FrozenTicks > 0)
                {
                    AppendEvent(
                        ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                        declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                        BuqiEventType.Immune, amount, spec.GetEffectId(), "ChargeBlockedFrozen");
                    continue;
                }

                long advance = (long)amount * CooldownUnit;
                target.CooldownProgress = SaturateToInt((long)target.CooldownProgress - advance);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                    BuqiEventType.Effect, amount, spec.GetEffectId(), "ChargeAdvanced");

                SideState targetSide = own.Items.Contains(target) ? own : enemy;
                while (target.CooldownProgress <= 0 &&
                       target.ActiveUsesThisTick < MaxEventsPerItemPerTick)
                {
                    if (target.AmmoCapacity > 0 &&
                        target.AmmoRemaining <= target.AmmoReservationsThisTick)
                    {
                        break;
                    }
                    target.CooldownProgress = SaturateToInt(
                        (long)target.CooldownProgress +
                        (long)target.EffectiveBaseCooldownTicks * CooldownUnit);
                    EnqueueActiveUse(
                        targetSide, target, provider, activeQueue, tick,
                        declaration.ChainId, declaration.ChainDepth + 1);
                }
            }
        }

        private static void ApplyAmmo(
            ResolvedTargets targets,
            ItemState actor,
            int amount,
            DeclaredEffect declaration,
            BuqiEffectSpec spec,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            List<ItemState> targetItems = targets.Items.Count > 0
                ? targets.Items
                : new List<ItemState> { actor };
            foreach (ItemState target in targetItems)
            {
                if (target.AmmoCapacity <= 0)
                {
                    AppendEvent(
                        ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                        declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                        BuqiEventType.Immune, 0, spec.GetEffectId(), "AmmoUnlimited");
                    continue;
                }

                int before = target.AmmoRemaining;
                target.AmmoRemaining = Clamp(
                    SaturateToInt((long)target.AmmoRemaining + amount),
                    0,
                    target.AmmoCapacity);
                target.IsEnabled = target.AmmoRemaining > 0;
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                    BuqiEventType.Effect, target.AmmoRemaining - before,
                    spec.GetEffectId(), "AmmoRefilled");
            }
        }

        private static ResolvedTargets ResolveTargets(
            BuqiTarget target,
            SideState own,
            SideState enemy,
            ItemState source,
            IItemDefinitionProvider provider)
        {
            if (target != BuqiTarget.LeftAdjacentItem &&
                target != BuqiTarget.RightAdjacentItem &&
                target != BuqiTarget.AllAdjacentItems)
            {
                return BuqiTargeting.Resolve(target, own, enemy, source);
            }

            var result = new ResolvedTargets();
            IReadOnlyList<ItemState> adjacent = GetLinearAdjacent(own, source, provider);
            if (target == BuqiTarget.AllAdjacentItems)
            {
                result.Items.AddRange(adjacent);
            }
            else
            {
                bool counterClockwise = target == BuqiTarget.LeftAdjacentItem;
                BuqiLinkDirection direction = counterClockwise
                    ? BuqiLinkDirection.CounterClockwise
                    : BuqiLinkDirection.Clockwise;
                BuqiLinkBoard board = BuqiLinkBoard.FromSide(own, provider);
                BuqiLinkItem linkSource = FindLinkItem(board, source.InstanceId);
                BuqiLinkItem linkTarget = BuqiLinkTopology.GetAdjacent(board, linkSource, direction);
                ItemState targetState = FindItemState(own, linkTarget?.InstanceId);
                if (targetState != null)
                    result.Items.Add(targetState);
            }
            if (result.Items.Count > 0)
                result.Side = own;
            return result;
        }

        private static IReadOnlyList<ItemState> GetLinearAdjacent(
            SideState side,
            ItemState source,
            IItemDefinitionProvider provider)
        {
            BuqiLinkBoard board = BuqiLinkBoard.FromSide(side, provider);
            BuqiLinkItem linkSource = FindLinkItem(board, source.InstanceId);
            IReadOnlyList<BuqiLinkItem> adjacent = BuqiLinkTopology.GetAllAdjacent(board, linkSource);
            var result = new List<ItemState>(adjacent.Count);
            foreach (BuqiLinkItem item in adjacent)
            {
                ItemState state = FindItemState(side, item.InstanceId);
                if (state != null)
                    result.Add(state);
            }
            return result;
        }

        private static BuqiLinkItem FindLinkItem(BuqiLinkBoard board, string instanceId)
        {
            foreach (BuqiLinkItem item in board.Items)
            {
                if (item.InstanceId == instanceId)
                    return item;
            }
            return null;
        }

        private static ItemState FindItemState(SideState side, string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            foreach (ItemState item in side.Items)
            {
                if (item.InstanceId == instanceId)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 将 Haste/Delay 先放入 Aggregate 修正桶；只有实际生效的敌方 Delay 才标记受扰阵营。
        /// A-04 免疫的 Delay 既不落地，也不触发 OnFirstInterfered。
        /// </summary>
        private static void QueueModifier(
            BuqiEffectSpec spec,
            ItemState actor,
            SideState own,
            ResolvedTargets targets,
            int amount,
            DeclaredEffect declaration,
            TickAccumulator accumulator,
            HashSet<SideState> interferedSides,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            bool fromEnemy = targets.Side != own;
            int durationTicks = Math.Max(1, spec.DurationTicks);
            int flightMitigation = 0;
            if (spec.Effect == BuqiEffect.Delay && fromEnemy && targets.Side.IsFlying)
            {
                int reducedDuration = HalfRoundedUp(durationTicks);
                flightMitigation = durationTicks - reducedDuration;
                durationTicks = reducedDuration;
            }
            var effectiveTargets = new List<ItemState>();
            List<ItemState> candidates = targets.Items.Count > 0 ? targets.Items : targets.Side.Items;
            foreach (ItemState target in candidates)
            {
                IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(target.AnnotationId);
                bool immune = !refinement.AllowsModifier(spec.Effect, fromEnemy);
                if (immune)
                {
                    AppendEvent(
                        ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                        declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                        BuqiEventType.Immune, amount, spec.GetEffectId(), "A04Immune");
                }
                else
                {
                    effectiveTargets.Add(target);
                }
            }

            if (effectiveTargets.Count == 0)
                return;

            if (flightMitigation > 0)
            {
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId,
                    effectiveTargets[0].InstanceId, BuqiEventType.Effect, flightMitigation,
                    spec.GetEffectId(), "FlightDelayMitigation");
            }

            accumulator.Modifiers.Add(new PendingModifier
            {
                Effect = spec.Effect,
                Bps = FixedModifierBps(spec.Effect),
                DurationTicks = durationTicks,
                SourceInstanceId = actor.InstanceId,
                FromEnemy = fromEnemy,
                TargetItems = targets.Items.Count > 0 ? effectiveTargets : new List<ItemState>(),
            });
            if (spec.Effect == BuqiEffect.Delay && fromEnemy)
                interferedSides.Add(targets.Side);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, declaration.ChainDepth,
                declaration.ChainId, actor.InstanceId, actor.InstanceId,
                effectiveTargets[0].InstanceId, BuqiEventType.Effect,
                FixedModifierBps(spec.Effect),
                spec.GetEffectId(), spec.ReasonCode);
        }

        /// <summary>
        /// 按契约固定顺序聚合一个阵营：新增护体、普通伤害吸收、治疗、失衡事故和状态写入。
        /// 修正从当前 tick 的 Aggregate 之后生效，避免凭空少一 tick；PostTick 由调用方统一检查胜负。
        /// </summary>
        private static void AddPendingStatus(
            TickAccumulator accumulator,
            int amount,
            ItemState actor,
            DeclaredEffect declaration,
            BuqiEffectSpec spec)
        {
            if (amount <= 0)
                return;

            accumulator.NewStatuses.Add(new TimedStatus
            {
                Effect = spec.Effect,
                Amount = amount,
                RemainingTicks = Math.Max(1, spec.DurationTicks),
                TickIntervalTicks = StatusTickIntervalTicks,
                SourceAnchorSlot = actor.AnchorSlot,
                SourceInstanceId = actor.InstanceId,
                ChainId = declaration.ChainId,
                EffectId = spec.GetEffectId(),
            });
        }

        private static void QueueFreeze(
            BuqiEffectSpec spec,
            ItemState actor,
            ResolvedTargets targets,
            int amount,
            DeclaredEffect declaration,
            TickAccumulator accumulator,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            if (amount <= 0)
                return;

            int durationTicks = amount;
            if (targets.Side.IsFlying)
            {
                int reducedDuration = HalfRoundedUp(durationTicks);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId,
                    targets.Items.Count > 0 ? targets.Items[0].InstanceId : string.Empty,
                    BuqiEventType.Effect, durationTicks - reducedDuration,
                    spec.GetEffectId(), "FlightFreezeMitigation");
                durationTicks = reducedDuration;
            }

            var targetItems = new List<ItemState>(targets.Items.Count > 0 ? targets.Items : targets.Side.Items);
            if (targetItems.Count == 0)
                return;

            accumulator.Freezes.Add(new PendingFreeze
            {
                DurationTicks = durationTicks,
                SourceAnchorSlot = actor.AnchorSlot,
                SourceInstanceId = actor.InstanceId,
                ChainId = declaration.ChainId,
                EffectId = spec.GetEffectId(),
                TargetItems = targetItems,
            });
        }

        private static void QueueFlight(
            SideState own,
            ItemState actor,
            int action,
            DeclaredEffect declaration,
            BuqiEffectSpec spec,
            TickAccumulator accumulator)
        {
            if (action > 0)
            {
                accumulator.Flights.Add(new PendingFlight
                {
                    Enter = true,
                    DurationTicks = Math.Max(1, spec.DurationTicks),
                    DamageBonusBps = Clamp(spec.FlightDamageBonusBps, 0, 100000),
                    EndDamage = Math.Max(0, spec.FlightEndDamage),
                    SourceAnchorSlot = actor.AnchorSlot,
                    SourceInstanceId = actor.InstanceId,
                    ChainId = declaration.ChainId,
                    EffectId = spec.GetEffectId(),
                });
                return;
            }

            if (action >= 0 || !own.IsFlying)
                return;

            foreach (PendingFlight pending in accumulator.Flights)
            {
                if (!pending.Enter)
                    return;
            }

            EnqueueFlightEndDamage(own, accumulator);
            accumulator.Flights.Add(new PendingFlight
            {
                Enter = false,
                SourceAnchorSlot = own.FlightSourceAnchorSlot,
                SourceInstanceId = own.FlightSourceInstanceId,
                ChainId = own.FlightChainId,
                EffectId = own.FlightEffectId,
            });
        }

        private static void QueueRage(
            ItemState actor,
            int amount,
            DeclaredEffect declaration,
            BuqiEffectSpec spec,
            TickAccumulator accumulator)
        {
            if (amount <= 0)
                return;
            accumulator.Rage.Add(new PendingRage
            {
                Amount = amount,
                Threshold = Math.Max(1, spec.RageThreshold),
                DurationTicks = Math.Max(1, spec.RageDurationTicks),
                CooldownReductionBps = Clamp(spec.RageCooldownReductionBps, 0, 9999),
                SourceAnchorSlot = actor.AnchorSlot,
                SourceInstanceId = actor.InstanceId,
                ChainId = declaration.ChainId,
                EffectId = spec.GetEffectId(),
            });
        }

        private static void ApplyAggregate(
            SideState side,
            TickAccumulator accumulator,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            SortPending(accumulator.Buffer);
            SortPending(accumulator.NormalDamage);
            SortPending(accumulator.Heal);
            SortPending(accumulator.Noise);

            foreach (PendingAmount pending in accumulator.Buffer)
            {
                int before = side.Buffer;
                side.Buffer = Math.Min(
                    BufferCap,
                    SaturateToInt((long)side.Buffer + pending.Amount));
                int actual = side.Buffer - before;
                AppendPendingEvent(ref nextSequence, log, tick, pending, actual, "BufferGain");
                if (actual < pending.Amount)
                    AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount - actual, "BufferOverflow");
            }

            bool hadBuffer = side.Buffer > 0;
            foreach (PendingAmount pending in accumulator.NormalDamage)
            {
                bool flightEnd = pending.ReasonCode == "FlightEndDamage";
                ApplyShieldedDamage(
                    side, pending, ref nextSequence, log, tick,
                    flightEnd ? "FlightEndBufferAbsorb" : "BufferAbsorb",
                    flightEnd ? "FlightEndDamage" : "Damage");
            }
            if (hadBuffer && side.Buffer == 0)
                side.BufferLostPending = true;

            foreach (PendingAmount pending in accumulator.Heal)
            {
                int before = side.Execution;
                side.Execution = Math.Min(
                    side.MaxExecution,
                    SaturateToInt((long)side.Execution + pending.Amount));
                int actual = side.Execution - before;
                AppendPendingEvent(ref nextSequence, log, tick, pending, actual, pending.ReasonCode);
                if (actual < pending.Amount)
                    AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount - actual, "HealOverflow");
                if (pending.ReasonCode == "Heal")
                    CleanseDamageStatuses(side, pending, ref nextSequence, log, tick);
            }

            foreach (PendingAmount pending in accumulator.Noise)
            {
                AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount, "NoiseChange");
                long total = Math.Max(0L, (long)side.Noise + pending.Amount);
                long incidentCount = total / NoiseThreshold;
                side.Noise = (int)(total % NoiseThreshold);
                if (incidentCount > 0)
                {
                    int damage = SaturateToInt(incidentCount * NoiseAccidentDamage);
                    ApplyExecutionDamage(side, damage);
                    AppendPendingEvent(
                        ref nextSequence, log, tick, pending,
                        damage, "NoiseAccident");
                    AppendPendingEvent(
                        ref nextSequence, log, tick, pending,
                        side.Noise, "NoiseRemainder");
                }
            }

            foreach (PendingModifier modifier in accumulator.Modifiers)
                ApplyModifier(side, modifier);

            foreach (TimedStatus status in accumulator.NewStatuses)
                ApplyStatus(side, status, ref nextSequence, log, tick);

            foreach (PendingFreeze pending in accumulator.Freezes)
                ApplyFreeze(pending, ref nextSequence, log, tick);

            foreach (PendingFlight pending in accumulator.Flights)
                ApplyFlight(side, pending, ref nextSequence, log, tick);

            accumulator.Rage.Sort((left, right) =>
            {
                int comparison = left.SourceAnchorSlot.CompareTo(right.SourceAnchorSlot);
                if (comparison != 0) return comparison;
                comparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
                return comparison != 0 ? comparison : string.CompareOrdinal(left.EffectId, right.EffectId);
            });
            foreach (PendingRage pending in accumulator.Rage)
                ApplyRage(side, pending, ref nextSequence, log, tick);
        }

        private static void CleanseDamageStatuses(
            SideState side,
            PendingAmount healer,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            SortStatuses(side.Statuses);
            CleanseDamageStatus(side, BuqiEffect.Poison, healer, ref nextSequence, log, tick);
            CleanseDamageStatus(side, BuqiEffect.Burn, healer, ref nextSequence, log, tick);
        }

        private static void CleanseDamageStatus(
            SideState side,
            BuqiEffect effect,
            PendingAmount healer,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            long total = 0;
            foreach (TimedStatus status in side.Statuses)
            {
                if (status.Effect == effect)
                    total += status.Amount;
            }
            if (total <= 0)
                return;

            long remainingRemoval = Math.Min(total, (total + 9) / 10);
            int removedTotal = SaturateToInt(remainingRemoval);
            for (int index = 0; index < side.Statuses.Count && remainingRemoval > 0; index++)
            {
                TimedStatus status = side.Statuses[index];
                if (status.Effect != effect)
                    continue;
                int removed = (int)Math.Min(status.Amount, remainingRemoval);
                status.Amount -= removed;
                remainingRemoval -= removed;
            }
            for (int index = side.Statuses.Count - 1; index >= 0; index--)
            {
                if (side.Statuses[index].Amount <= 0)
                    side.Statuses.RemoveAt(index);
            }
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                healer.ChainId, healer.SourceInstanceId, healer.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, removedTotal,
                healer.EffectId, "StatusCleansed");
        }

        private static void ApplyRage(
            SideState side,
            PendingRage pending,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            long total = Math.Max(0L, (long)side.Rage + pending.Amount);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, pending.Amount,
                pending.EffectId, "RageGained");

            long activationCount = total / pending.Threshold;
            side.Rage = (int)(total % pending.Threshold);
            if (activationCount > 0)
            {
                foreach (ItemState item in side.Items)
                {
                    item.FrozenTicks = 0;
                    RemoveModifier(item.Modifiers, BuqiEffect.Delay);
                }
                RemoveModifier(side.SideModifiers, BuqiEffect.Delay);
                side.EnragedTicks = Math.Max(side.EnragedTicks, pending.DurationTicks);
                side.RageCooldownReductionBps = Math.Max(
                    side.RageCooldownReductionBps,
                    pending.CooldownReductionBps);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    string.Empty, BuqiEventType.Effect,
                    SaturateToInt(activationCount * pending.Threshold),
                    pending.EffectId, "RageConsumed");
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    string.Empty, BuqiEventType.Effect, side.Rage,
                    pending.EffectId, "RageRemainder");
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    string.Empty, BuqiEventType.Effect, side.EnragedTicks,
                    pending.EffectId, "EnrageStarted");
            }
        }

        private static void RemoveModifier(List<TimedModifier> modifiers, BuqiEffect effect)
        {
            for (int index = modifiers.Count - 1; index >= 0; index--)
            {
                if (modifiers[index].Effect == effect)
                    modifiers.RemoveAt(index);
            }
        }

        private static void ApplyFlight(
            SideState side,
            PendingFlight pending,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            if (pending.Enter)
            {
                bool wasFlying = side.IsFlying;
                bool extendsFlight = !wasFlying || pending.DurationTicks > side.FlyingTicks;
                bool increasesEndDamage = !wasFlying || pending.EndDamage > side.FlightEndDamage;
                side.IsFlying = true;
                if (extendsFlight)
                {
                    side.FlightSourceAnchorSlot = pending.SourceAnchorSlot;
                    side.FlightSourceInstanceId = pending.SourceInstanceId;
                    side.FlightChainId = pending.ChainId;
                    side.FlightEffectId = pending.EffectId;
                }
                if (increasesEndDamage)
                {
                    side.FlightEndDamageSourceAnchorSlot = pending.SourceAnchorSlot;
                    side.FlightEndDamageSourceInstanceId = pending.SourceInstanceId;
                    side.FlightEndDamageChainId = pending.ChainId;
                    side.FlightEndDamageEffectId = pending.EffectId;
                }
                side.FlyingTicks = Math.Max(side.FlyingTicks, pending.DurationTicks);
                side.FlightDamageBonusBps = Math.Max(side.FlightDamageBonusBps, pending.DamageBonusBps);
                side.FlightEndDamage = Math.Max(side.FlightEndDamage, pending.EndDamage);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    string.Empty, BuqiEventType.Effect, side.FlyingTicks,
                    pending.EffectId, wasFlying ? "FlightRefreshed" : "FlightStarted");
                return;
            }

            if (!side.IsFlying)
                return;
            ClearFlight(side);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, 0, pending.EffectId, "FlightEnded");
        }

        private static void ApplyShieldedDamage(
            SideState side,
            PendingAmount pending,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            string shieldReason,
            string damageReason)
        {
            int absorbed = Math.Min(side.Buffer, pending.Amount);
            if (absorbed > 0)
            {
                side.Buffer -= absorbed;
                AppendPendingEvent(ref nextSequence, log, tick, pending, absorbed, shieldReason);
            }
            int actualDamage = pending.Amount - absorbed;
            if (actualDamage > 0)
            {
                ApplyExecutionDamage(side, actualDamage);
                AppendPendingEvent(ref nextSequence, log, tick, pending, actualDamage, damageReason);
            }
        }

        private static void ApplyExecutionDamage(SideState side, int damage)
        {
            side.Execution = SaturateToInt((long)side.Execution - damage);
        }

        private static void ApplyStatus(
            SideState side,
            TimedStatus pending,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            AddOrRefreshStatus(side.Statuses, pending);
            AppendStatusEvent(ref nextSequence, log, tick, pending, pending.Amount, "StatusApplied");
        }

        private static void ApplyFreeze(
            PendingFreeze pending,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            pending.TargetItems.Sort((left, right) =>
            {
                int anchorComparison = left.AnchorSlot.CompareTo(right.AnchorSlot);
                if (anchorComparison != 0) return anchorComparison;
                return string.CompareOrdinal(left.InstanceId, right.InstanceId);
            });

            foreach (ItemState target in pending.TargetItems)
            {
                target.FrozenTicks = Math.Max(target.FrozenTicks, pending.DurationTicks);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    target.InstanceId, BuqiEventType.Effect,
                    target.FrozenTicks, pending.EffectId, "FreezeApplied");
            }
        }

        private static void ApplyModifier(SideState side, PendingModifier pending)
        {
            if (pending.TargetItems.Count == 0)
            {
                foreach (ItemState item in side.Items)
                    RemoveOppositeModifier(item.Modifiers, pending.Effect);
                AddOrRefreshModifier(side.SideModifiers, pending);
                return;
            }

            foreach (ItemState target in pending.TargetItems)
            {
                AddOrRefreshModifier(target.Modifiers, pending);
            }
        }

        private static void AddOrRefreshModifier(List<TimedModifier> modifiers, PendingModifier pending)
        {
            RemoveOppositeModifier(modifiers, pending.Effect);
            foreach (TimedModifier modifier in modifiers)
            {
                if (modifier.Effect != pending.Effect ||
                    modifier.SourceInstanceId != pending.SourceInstanceId ||
                    modifier.FromEnemy != pending.FromEnemy)
                {
                    continue;
                }
                modifier.RemainingTicks = Math.Max(modifier.RemainingTicks, pending.DurationTicks);
                modifier.Bps = Math.Max(modifier.Bps, pending.Bps);
                return;
            }

            modifiers.Add(new TimedModifier
            {
                Effect = pending.Effect,
                Bps = pending.Bps,
                RemainingTicks = pending.DurationTicks,
                SourceInstanceId = pending.SourceInstanceId,
                FromEnemy = pending.FromEnemy,
            });
        }

        private static void AddOrRefreshStatus(List<TimedStatus> statuses, TimedStatus pending)
        {
            foreach (TimedStatus status in statuses)
            {
                if (status.Effect != pending.Effect ||
                    status.SourceInstanceId != pending.SourceInstanceId ||
                    status.EffectId != pending.EffectId ||
                    status.RemainingTicks != pending.RemainingTicks ||
                    status.TickProgressTicks != pending.TickProgressTicks)
                {
                    continue;
                }

                status.Amount = SaturateToInt((long)status.Amount + pending.Amount);
                status.TickIntervalTicks = Math.Max(1, pending.TickIntervalTicks);
                status.SourceAnchorSlot = pending.SourceAnchorSlot;
                status.ChainId = pending.ChainId;
                status.EffectId = pending.EffectId;
                return;
            }

            statuses.Add(new TimedStatus
            {
                Effect = pending.Effect,
                Amount = pending.Amount,
                RemainingTicks = pending.RemainingTicks,
                TickIntervalTicks = Math.Max(1, pending.TickIntervalTicks),
                TickProgressTicks = pending.TickProgressTicks,
                SourceAnchorSlot = pending.SourceAnchorSlot,
                SourceInstanceId = pending.SourceInstanceId,
                ChainId = pending.ChainId,
                EffectId = pending.EffectId,
            });
        }

        private static void ExpireModifiers(SideState side)
        {
            ExpireModifierList(side.SideModifiers);
            foreach (ItemState item in side.Items)
                ExpireModifierList(item.Modifiers);
        }

        private static void AdvanceFlight(
            SideState side,
            TickAccumulator accumulator,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            if (!side.IsFlying)
                return;

            side.FlyingTicks--;
            if (side.FlyingTicks > 0)
                return;

            string chainId = side.FlightChainId;
            string sourceInstanceId = side.FlightSourceInstanceId;
            string effectId = side.FlightEffectId;
            EnqueueFlightEndDamage(side, accumulator);
            ClearFlight(side);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.PreTick, 0,
                chainId, sourceInstanceId, sourceInstanceId, string.Empty,
                BuqiEventType.Effect, 0, effectId, "FlightEnded");
        }

        private static void EnqueueFlightEndDamage(SideState side, TickAccumulator accumulator)
        {
            if (side.FlightEndDamage <= 0)
                return;
            accumulator.NormalDamage.Add(new PendingAmount
            {
                Amount = side.FlightEndDamage,
                SourceAnchorSlot = side.FlightEndDamageSourceAnchorSlot,
                SourceInstanceId = side.FlightEndDamageSourceInstanceId,
                ChainId = side.FlightEndDamageChainId,
                EffectId = side.FlightEndDamageEffectId,
                ReasonCode = "FlightEndDamage",
            });
        }

        private static void SortStatuses(List<TimedStatus> statuses)
        {
            statuses.Sort((left, right) =>
            {
                int comparison = left.SourceAnchorSlot.CompareTo(right.SourceAnchorSlot);
                if (comparison != 0) return comparison;
                comparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
                if (comparison != 0) return comparison;
                comparison = string.CompareOrdinal(left.EffectId, right.EffectId);
                if (comparison != 0) return comparison;
                comparison = left.RemainingTicks.CompareTo(right.RemainingTicks);
                return comparison != 0
                    ? comparison
                    : left.TickProgressTicks.CompareTo(right.TickProgressTicks);
            });
        }

        private static void AddInitialModifier(List<TimedModifier> modifiers, TimedModifier pending)
        {
            RemoveOppositeModifier(modifiers, pending.Effect);
            modifiers.Add(pending);
        }

        private static void RemoveOppositeModifier(List<TimedModifier> modifiers, BuqiEffect effect)
        {
            BuqiEffect opposite = effect == BuqiEffect.Haste
                ? BuqiEffect.Delay
                : effect == BuqiEffect.Delay ? BuqiEffect.Haste : effect;
            if (opposite == effect)
                return;
            for (int index = modifiers.Count - 1; index >= 0; index--)
            {
                if (modifiers[index].Effect == opposite)
                    modifiers.RemoveAt(index);
            }
        }

        private static int FixedModifierBps(BuqiEffect effect)
        {
            if (effect == BuqiEffect.Haste)
                return 20000;
            if (effect == BuqiEffect.Delay)
                return 5000;
            return CooldownUnit;
        }

        private static void ClearFlight(SideState side)
        {
            side.IsFlying = false;
            side.FlyingTicks = 0;
            side.FlightDamageBonusBps = 0;
            side.FlightEndDamage = 0;
            side.FlightSourceAnchorSlot = 0;
            side.FlightSourceInstanceId = string.Empty;
            side.FlightChainId = string.Empty;
            side.FlightEffectId = string.Empty;
            side.FlightEndDamageSourceAnchorSlot = 0;
            side.FlightEndDamageSourceInstanceId = string.Empty;
            side.FlightEndDamageChainId = string.Empty;
            side.FlightEndDamageEffectId = string.Empty;
        }

        private static void ExpireModifierList(List<TimedModifier> modifiers)
        {
            for (int index = modifiers.Count - 1; index >= 0; index--)
            {
                modifiers[index].RemainingTicks--;
                if (modifiers[index].RemainingTicks <= 0)
                    modifiers.RemoveAt(index);
            }
        }

        private static int CalculateEffectAmount(
            ItemState actor,
            SideState own,
            BuqiEffectSpec spec,
            bool rewrite)
        {
            int qualityBps = actor.Quality == (int)BuqiQuality.Improved
                ? 16000
                : actor.Quality == (int)BuqiQuality.Fixed ? 24000 : 10000;
            IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId);
            int annotationBps = refinement.GetEffectMultiplierBps(
                spec.Effect,
                spec.Trigger == BuqiTrigger.OnBattleStart);

            int rewriteBps = rewrite ? 5000 : 10000;
            decimal numerator = (decimal)spec.Amount * qualityBps * annotationBps * rewriteBps;
            const decimal Denominator = 10000m * 10000m * 10000m;
            int result = RoundDivide(numerator, Denominator);

            if (spec.Effect == BuqiEffect.Damage)
            {
                if (own.IsFlying && own.FlightDamageBonusBps > 0)
                    result = RoundBps(result, CooldownUnit + own.FlightDamageBonusBps);
            }
            return result;
        }

        private static bool TryApplyCritical(
            int amount,
            BuqiEffectSpec spec,
            DeclaredEffect declaration,
            ulong battleSeed,
            int tick,
            out int criticalAmount,
            ref int nextSequence,
            List<BattleEvent> log,
            string targetId)
        {
            criticalAmount = amount;
            if (amount <= 0 || !CanCrit(spec.Effect) || spec.CriticalChanceBps <= 0)
                return false;
            string rollKey = BuqiText.Format(
                "crit:{0}:{1}:{2}:{3}:{4}:{5}:{6}",
                tick,
                declaration.ChainId,
                declaration.Actor.InstanceId,
                spec.GetEffectId(),
                declaration.RepeatIndex,
                declaration.IsRewrite ? 1 : 0,
                declaration.DeclarationOrder);
            int roll = BuqiCrypto.DeterministicRollBps(battleSeed, rollKey);
            if (roll >= Clamp(spec.CriticalChanceBps, 0, CooldownUnit))
                return false;
            criticalAmount = SaturateToInt((long)amount * 2);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                declaration.ChainId, declaration.Actor.InstanceId, declaration.Actor.InstanceId,
                targetId, BuqiEventType.Effect, 20000, spec.GetEffectId(), "CriticalApplied");
            return true;
        }

        private static bool CanCrit(BuqiEffect effect)
        {
            return effect == BuqiEffect.Damage ||
                   effect == BuqiEffect.Heal ||
                   effect == BuqiEffect.Buffer ||
                   effect == BuqiEffect.Regen ||
                   effect == BuqiEffect.Burn ||
                   effect == BuqiEffect.Poison;
        }

        private static int RoundDivide(decimal numerator, decimal denominator)
        {
            decimal rounded = decimal.Truncate(
                numerator >= 0
                    ? (numerator + denominator / 2) / denominator
                    : (numerator - denominator / 2) / denominator);
            if (rounded >= int.MaxValue)
                return int.MaxValue;
            if (rounded <= int.MinValue)
                return int.MinValue;
            return (int)rounded;
        }

        private static void EnqueueOpeningNoise(SideState side, TickAccumulator accumulator)
        {
            foreach (ItemState item in side.Items)
            {
                IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(item.AnnotationId);
                if (refinement.OpeningNoise <= 0)
                    continue;
                accumulator.Noise.Add(new PendingAmount
                {
                    Amount = refinement.OpeningNoise,
                    SourceAnchorSlot = item.AnchorSlot,
                    SourceInstanceId = item.InstanceId,
                    ChainId = BuqiText.Format("{0}@opening-noise", item.InstanceId),
                    ReasonCode = "A06OpeningNoise",
                });
            }
        }

        private static void AddPending(
            TickAccumulator accumulator,
            int amount,
            ItemState actor,
            DeclaredEffect declaration,
            BuqiEffectSpec spec,
            List<PendingAmount> destination,
            string reasonOverride = null)
        {
            if (amount == 0)
                return;
            destination.Add(new PendingAmount
            {
                Amount = amount,
                SourceAnchorSlot = actor.AnchorSlot,
                SourceInstanceId = actor.InstanceId,
                ChainId = declaration.ChainId,
                EffectId = spec.GetEffectId(),
                ReasonCode = reasonOverride ?? spec.ReasonCode,
            });
        }

        private static void ResetAccumulator(TickAccumulator accumulator)
        {
            accumulator.Buffer.Clear();
            accumulator.NormalDamage.Clear();
            accumulator.Heal.Clear();
            accumulator.Noise.Clear();
            accumulator.NewStatuses.Clear();
            accumulator.Freezes.Clear();
            accumulator.Modifiers.Clear();
            accumulator.Flights.Clear();
            accumulator.Rage.Clear();
        }

        private static void SortQueue(List<DeclaredEffect> queue)
        {
            for (int index = 0; index < queue.Count; index++)
                queue[index].DeclarationOrder = index;

            queue.Sort(CompareDeclarations);
        }

        private static int FindNextDeclarationIndex(List<DeclaredEffect> queue, int startIndex)
        {
            int bestIndex = startIndex;
            for (int index = startIndex + 1; index < queue.Count; index++)
            {
                if (CompareDeclarations(queue[index], queue[bestIndex]) < 0)
                    bestIndex = index;
            }
            return bestIndex;
        }

        private static int CompareDeclarations(DeclaredEffect left, DeclaredEffect right)
        {
            int comparison = left.ChainDepth.CompareTo(right.ChainDepth);
            if (comparison != 0) return comparison;
            comparison = left.Actor.AnchorSlot.CompareTo(right.Actor.AnchorSlot);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(left.Actor.InstanceId, right.Actor.InstanceId);
            return comparison != 0
                ? comparison
                : left.DeclarationOrder.CompareTo(right.DeclarationOrder);
        }

        private static void SortPending(List<PendingAmount> pending)
        {
            pending.Sort((left, right) =>
            {
                int anchorComparison = left.SourceAnchorSlot.CompareTo(right.SourceAnchorSlot);
                if (anchorComparison != 0) return anchorComparison;
                int sourceComparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
                if (sourceComparison != 0) return sourceComparison;
                return string.CompareOrdinal(left.EffectId, right.EffectId);
            });
        }

        /// <summary>
        /// 按 tick、阶段和原始结算序重排，并重新连续编号。
        /// 阶段内必须保留模拟器的真实应用顺序，否则 Aggregate 子阶段会在回放中被来源排序倒置。
        /// </summary>
        private static void SortAndResequenceLog(List<BattleEvent> log)
        {
            log.Sort((left, right) =>
            {
                int comparison = left.Tick.CompareTo(right.Tick);
                if (comparison != 0) return comparison;
                comparison = left.Phase.CompareTo(right.Phase);
                if (comparison != 0) return comparison;
                return left.Sequence.CompareTo(right.Sequence);
            });
            for (int index = 0; index < log.Count; index++)
                log[index].Sequence = index;
        }

        private static int CompareItemInstances(ItemInstance left, ItemInstance right)
        {
            int anchorComparison = left.AnchorSlot.CompareTo(right.AnchorSlot);
            return anchorComparison != 0
                ? anchorComparison
                : string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }

        private static BattleOutcome DetermineOutcome(SideState left, SideState right)
        {
            if (left.Execution <= 0 && right.Execution <= 0)
                return BattleOutcome.Draw;
            return right.Execution <= 0 ? BattleOutcome.LeftWin : BattleOutcome.RightWin;
        }

        private static void AppendPendingEvent(
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            PendingAmount pending,
            int amount,
            string reason)
        {
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, amount, pending.EffectId, reason);
        }

        private static void AppendStatusEvent(
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            TimedStatus status,
            int amount,
            string reason)
        {
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                status.ChainId, status.SourceInstanceId, status.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, amount, status.EffectId, reason);
        }

        private static void AppendStatusPreTickEvent(
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            TimedStatus status,
            int amount,
            string reason)
        {
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.PreTick, 0,
                status.ChainId, status.SourceInstanceId, status.SourceInstanceId,
                string.Empty, BuqiEventType.Effect, amount, status.EffectId, reason);
        }

        private static void AppendEvent(
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            BuqiEventPhase phase,
            int chainDepth,
            string chainId,
            string actorId,
            string sourceId,
            string targetId,
            BuqiEventType type,
            int amount,
            string effectId,
            string reasonCode)
        {
            log.Add(new BattleEvent
            {
                Sequence = nextSequence++,
                Tick = tick,
                Phase = phase,
                ChainDepth = chainDepth,
                ChainId = chainId ?? string.Empty,
                ActorInstanceId = actorId ?? string.Empty,
                SourceInstanceId = sourceId ?? string.Empty,
                TargetInstanceId = targetId ?? string.Empty,
                Type = type,
                Amount = amount,
                EffectId = effectId ?? string.Empty,
                ReasonCode = reasonCode ?? string.Empty,
            });
        }

        /// <summary>同一 tick 只记录一次截断事件，避免错误日志本身无限膨胀。</summary>
        private static void LogLoopCap(
            ref int nextSequence,
            List<BattleEvent> log,
            int tick,
            string chainId,
            string reason,
            ref bool loopCapLogged)
        {
            if (loopCapLogged)
                return;
            loopCapLogged = true;
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Chain, 0,
                chainId, string.Empty, string.Empty, string.Empty,
                BuqiEventType.Truncate, 0, string.Empty, reason);
        }

        private static int RoundBps(int value, int bps)
        {
            return SaturateToInt(((long)value * bps + 5000) / 10000);
        }

        private static int HalfRoundedUp(int value)
        {
            return Math.Max(1, value / 2 + value % 2);
        }

        private static int SaturateToInt(long value)
        {
            if (value >= int.MaxValue)
                return int.MaxValue;
            if (value <= int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
