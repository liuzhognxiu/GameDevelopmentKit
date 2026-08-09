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
        /// <summary>声明时已经确定并预占的蓄力读取量，Resolve 不得再次读取运行时状态。</summary>
        public int DeclaredCharge;
        /// <summary>A-03 复写引用的原始声明，用于复用蓄力快照而不重复消费。</summary>
        public DeclaredEffect RewriteSource;
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

    /// <summary>
    /// 单阵营单 tick 的结算桶。Resolve 只向桶内声明，Aggregate 再按固定顺序修改状态。
    /// </summary>
    internal sealed class TickAccumulator
    {
        public List<PendingAmount> Buffer = new List<PendingAmount>();
        public List<PendingAmount> NormalDamage = new List<PendingAmount>();
        public List<PendingAmount> BurnDamage = new List<PendingAmount>();
        public List<PendingAmount> Heal = new List<PendingAmount>();
        public List<PendingAmount> PoisonDamage = new List<PendingAmount>();
        public List<PendingAmount> Noise = new List<PendingAmount>();
        public List<PendingAmount> OvertimeDamage = new List<PendingAmount>();
        public List<TimedStatus> NewStatuses = new List<TimedStatus>();
        public List<PendingFreeze> Freezes = new List<PendingFreeze>();
        public List<PendingModifier> Modifiers = new List<PendingModifier>();
    }

    /// <summary>
    /// 《不器》战斗契约 v0.4 的确定性纯 C# 模拟器。
    /// 1 tick = 100 ms；不读取 Time、Unity Random、场景或网络，Unity 与无头端直接编译同一份源码。
    /// </summary>
    public static class BuqiBattleSimulator
    {
        /// <summary>正常战斗共 450 tick，tick 450 起进入劫火。</summary>
        public const int NormalTickCount = 450;
        /// <summary>tick 600 完成 Aggregate 后强制按执行值、护体、失衡比较。</summary>
        public const int HardCapTick = 600;
        public const int BufferCap = 60;
        public const int NoiseThreshold = 10;
        public const int NoiseAccidentDamage = 8;
        public const int ChargeCap = 9;
        public const int DefaultMaxExecution = 100;
        public const int StatusTickIntervalTicks = 10;

        /// <summary>防止全场触发链异常膨胀的单 tick 上限。</summary>
        public const int MaxEventsPerTick = 64;

        /// <summary>防止单个法门在同 tick 自循环的声明上限。</summary>
        public const int MaxEventsPerItemPerTick = 4;

        /// <summary>冷却与 basis points 共用的整数基准 10000。</summary>
        public const int CooldownUnit = 10000;
        public const string RuleVersion = "0.5.0";
        public const string SimulationVersion = "battle-core-0.5.0";

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
            var anchorByInstanceId = BuildAnchorLookup(left, right);
            int nextSequence = 0;
            BattleOutcome outcome = BattleOutcome.Draw;
            string terminationReason = string.Empty;
            int durationTicks = 0;

            for (int tick = 0; tick <= HardCapTick; tick++)
            {
                int processedEvents = 0;
                bool loopCapLogged = false;
                var perItemCount = new Dictionary<string, int>(StringComparer.Ordinal);
                var interferedSides = new HashSet<SideState>();
                var queue = new List<DeclaredEffect>();

                // tick 0 的开战声明必须存在于首次冷却推进之前：先声明双方 OnBattleStart，
                // 再推进冷却和声明主动使用，避免开战效果被首次 ready 使用抢先结算。
                if (tick == 0)
                {
                    EnqueueTriggerForSide(left, provider, BuqiTrigger.OnBattleStart, queue, "battle-start", 0, false);
                    EnqueueTriggerForSide(right, provider, BuqiTrigger.OnBattleStart, queue, "battle-start", 0, false);
                    EnqueueOpeningNoise(left, accumulators[left]);
                    EnqueueOpeningNoise(right, accumulators[right]);
                }

                AdvanceCooldowns(left);
                AdvanceCooldowns(right);
                ExpireModifiers(left);
                ExpireModifiers(right);
                EnqueueStatusTicks(left, accumulators[left]);
                EnqueueStatusTicks(right, accumulators[right]);
                EnqueueOvertimeDamage(tick, accumulators[left]);
                EnqueueOvertimeDamage(tick, accumulators[right]);
                EnqueuePendingConditions(left, provider, queue, tick);
                EnqueuePendingConditions(right, provider, queue, tick);
                EnqueueReadyUses(left, provider, queue, tick);
                EnqueueReadyUses(right, provider, queue, tick);

                SortQueue(queue);
                ProcessQueue(
                    queue, left, right, accumulators, provider, interferedSides,
                    perItemCount, ref processedEvents, ref nextSequence, log, tick, ref loopCapLogged);

                var responseQueue = new List<DeclaredEffect>();
                EnqueueChargeConditions(left, provider, responseQueue, tick);
                EnqueueChargeConditions(right, provider, responseQueue, tick);
                if (interferedSides.Contains(left))
                    EnqueueFirstInterfered(left, provider, responseQueue, tick);
                if (interferedSides.Contains(right))
                    EnqueueFirstInterfered(right, provider, responseQueue, tick);
                SortQueue(responseQueue);
                ProcessQueue(
                    responseQueue, left, right, accumulators, provider, interferedSides,
                    perItemCount, ref processedEvents, ref nextSequence, log, tick, ref loopCapLogged);

                // 双方的 Declare/Resolve/Chain 全部结束后才分别聚合；在此之前任何一方都不能因先执行而提前结束战斗。
                ApplyAggregate(left, accumulators[left], ref nextSequence, log, tick);
                ApplyAggregate(right, accumulators[right], ref nextSequence, log, tick);
                ResetAccumulator(accumulators[left]);
                ResetAccumulator(accumulators[right]);

                SortAndResequenceLog(log, anchorByInstanceId);
                nextSequence = log.Count;

                // PostTick 才统一检查胜负，因此同 tick 双方同时归零会得到平局，而不是由处理顺序决定胜者。
                if (left.Execution <= 0 || right.Execution <= 0)
                {
                    outcome = DetermineOutcome(left, right);
                    terminationReason = tick >= NormalTickCount
                        ? TerminationReason.Overtime.ToString()
                        : TerminationReason.Normal.ToString();
                    durationTicks = tick + 1;
                    break;
                }

                if (tick == HardCapTick)
                {
                    outcome = DecideHardCap(left, right);
                    terminationReason = TerminationReason.HardCap.ToString();
                    durationTicks = tick + 1;
                    break;
                }
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
                    10,
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
                    CooldownProgress = baseCooldownTicks * CooldownUnit,
                };
                if (instance.TemporaryModifiers != null)
                {
                    foreach (TemporaryModifier modifier in instance.TemporaryModifiers)
                    {
                        state.Modifiers.Add(new TimedModifier
                        {
                            Effect = modifier.Effect,
                            Bps = modifier.Bps,
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

        /// <summary>
        /// 在本 tick 的声明前推进冷却，并记录 ReadyThisTick。
        /// A-04 只免疫敌方 Delay 且忽略友方 Haste；A-01/A-02 在构建运行时状态时已修正基础冷却。
        /// </summary>
        private static void AdvanceCooldowns(SideState side)
        {
            foreach (ItemState item in side.Items)
            {
                if (item.FrozenTicks > 0)
                {
                    item.FrozenTicks--;
                    item.ReadyThisTick = false;
                    continue;
                }

                int hasteBps = 0;
                int delayBps = 0;
                AccumulateModifiers(side.SideModifiers, item, ref hasteBps, ref delayBps);
                AccumulateModifiers(item.Modifiers, item, ref hasteBps, ref delayBps);
                int advance = Clamp(CooldownUnit + hasteBps - delayBps, 5000, 15000);
                item.CooldownProgress -= advance;
                item.ReadyThisTick = item.CooldownProgress <= 0;
                if (item.ReadyThisTick)
                    item.CooldownProgress += item.EffectiveBaseCooldownTicks * CooldownUnit;
            }
        }

        private static void AccumulateModifiers(
            List<TimedModifier> modifiers,
            ItemState item,
            ref int hasteBps,
            ref int delayBps)
        {
            foreach (TimedModifier modifier in modifiers)
            {
                IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(item.AnnotationId);
                if (!refinement.AllowsModifier(modifier.Effect, modifier.FromEnemy))
                    continue;
                if (modifier.Effect == BuqiEffect.Haste)
                {
                    hasteBps += modifier.Bps;
                }
                else if (modifier.Effect == BuqiEffect.Delay)
                {
                    delayBps += modifier.Bps;
                }
            }
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
            int tick)
        {
            provider.TryGet(actor.DefinitionId, out BuqiItemDefinition definition);
            string chainId = BuqiText.Format("{0}@use@{1}", actor.InstanceId, tick);
            var directDeclarations = new List<DeclaredEffect>();
            foreach (BuqiEffectSpec spec in definition.Effects)
            {
                if (spec.Trigger == BuqiTrigger.OnUse)
                {
                    DeclaredEffect declaration = NewDeclaration(actor, spec, chainId, 0, false);
                    directDeclarations.Add(declaration);
                    queue.Add(declaration);
                }
            }

            foreach (ItemState adjacent in GetRingAdjacent(side, actor, provider))
            {
                provider.TryGet(adjacent.DefinitionId, out BuqiItemDefinition adjacentDefinition);
                foreach (BuqiEffectSpec spec in adjacentDefinition.Effects)
                {
                    if (spec.Trigger != BuqiTrigger.OnAdjacentUse)
                        continue;
                    adjacent.AdjacentUseCount++;
                    queue.Add(NewDeclaration(adjacent, spec, chainId, 1, false));
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
                    DeclaredEffect rewrite = NewDeclaration(actor, source.Spec, chainId, 1, true);
                    rewrite.RewriteSource = source;
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
                queue.Add(NewDeclaration(
                    actor, spec, BuqiText.Format("{0}@count@{1}", actor.InstanceId, tick), 1, false));
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
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == trigger)
                        queue.Add(NewDeclaration(item, spec, chainId, depth, rewrite));
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
                if (item.FirstConditionUsed)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                bool hasBufferLostTrigger = false;
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == BuqiTrigger.OnFirstConditionMet &&
                        spec.ConditionKind == BuqiConditionKind.BufferLost)
                    {
                        hasBufferLostTrigger = true;
                        queue.Add(NewDeclaration(
                            item, spec, BuqiText.Format("{0}@condition@{1}", item.InstanceId, tick), 0, false));
                    }
                }
                if (hasBufferLostTrigger)
                    item.FirstConditionUsed = true;
            }
        }

        private static void EnqueueChargeConditions(
            SideState side,
            IItemDefinitionProvider provider,
            List<DeclaredEffect> queue,
            int tick)
        {
            foreach (ItemState item in side.Items)
            {
                if (item.FirstConditionUsed)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                bool enqueued = false;
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == BuqiTrigger.OnFirstConditionMet &&
                        spec.ConditionKind == BuqiConditionKind.ChargeAtLeast &&
                        item.Charge >= spec.ConditionThreshold)
                    {
                        queue.Add(NewDeclaration(
                            item, spec, BuqiText.Format("{0}@condition@{1}", item.InstanceId, tick), 0, false));
                        enqueued = true;
                    }
                }
                if (enqueued)
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
                if (item.FirstInterferedUsed)
                    continue;
                provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition);
                bool enqueued = false;
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    if (spec.Trigger == BuqiTrigger.OnFirstInterfered)
                    {
                        queue.Add(NewDeclaration(
                            item, spec, BuqiText.Format("{0}@interfered@{1}", item.InstanceId, tick), 0, false));
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
            ref bool loopCapLogged)
        {
            foreach (DeclaredEffect declaration in queue)
            {
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
                    ref nextSequence, log, tick);
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
            int tick)
        {
            ItemState actor = declaration.Actor;
            SideState own = left.Items.Contains(actor) ? left : right;
            SideState enemy = own == left ? right : left;
            BuqiEffectSpec spec = declaration.Spec;
            ResolvedTargets targets = ResolveTargets(spec.Target, own, enemy, actor, provider);
            if (targets.Side == null && targets.Items.Count == 0)
            {
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Resolve, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, string.Empty,
                    BuqiEventType.NoTarget, 0, spec.GetEffectId(), "NoValidTarget");
                return;
            }

            DeclareChargeUsage(declaration, ref nextSequence, log, tick);
            int amount = CalculateEffectAmount(
                actor,
                spec,
                declaration.IsRewrite,
                declaration.DeclaredCharge);
            string targetId = targets.Items.Count > 0 ? targets.Items[0].InstanceId : string.Empty;
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
                        accumulators[targets.Side]);
                    break;
                case BuqiEffect.Noise:
                    amount = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId)
                        .AdjustNoiseAmount(amount);
                    AddPending(accumulators[targets.Side], amount, actor, declaration, spec, accumulators[targets.Side].Noise);
                    break;
                case BuqiEffect.Charge:
                    ApplyCharge(targets, actor, amount, declaration, spec, ref nextSequence, log, tick);
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

        /// <summary>
        /// 在效果已经确定存在合法目标后声明本次蓄力读取。消费发生在 Declare，后续 Resolve 只使用快照。
        /// A-03 复写复用原声明快照且不再次扣除；同 tick 后续声明只能看到扣除后的剩余蓄力。
        /// </summary>
        private static void DeclareChargeUsage(
            DeclaredEffect declaration,
            ref int nextSequence,
            List<BattleEvent> log,
            int tick)
        {
            BuqiEffectSpec spec = declaration.Spec;
            if (spec.ChargeReadLimit <= 0)
                return;

            if (declaration.IsRewrite && declaration.RewriteSource != null)
            {
                declaration.DeclaredCharge = declaration.RewriteSource.DeclaredCharge;
                return;
            }

            int read = Math.Min(declaration.Actor.Charge, spec.ChargeReadLimit);
            declaration.DeclaredCharge = read;
            if (!spec.ChargeConsume || read <= 0)
                return;

            declaration.Actor.Charge -= read;
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                declaration.ChainId, declaration.Actor.InstanceId, declaration.Actor.InstanceId,
                declaration.Actor.InstanceId, BuqiEventType.Effect, -read,
                spec.GetEffectId(), "ChargeConsumed");
        }

        private static void ApplyCharge(
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
                int before = target.Charge;
                target.Charge = Clamp(target.Charge + amount, 0, ChargeCap);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Declare, declaration.ChainDepth,
                    declaration.ChainId, actor.InstanceId, actor.InstanceId, target.InstanceId,
                    BuqiEventType.Effect, target.Charge - before, spec.GetEffectId(), spec.ReasonCode);
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
            IReadOnlyList<ItemState> adjacent = GetRingAdjacent(own, source, provider);
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

        private static IReadOnlyList<ItemState> GetRingAdjacent(
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

            accumulator.Modifiers.Add(new PendingModifier
            {
                Effect = spec.Effect,
                Bps = Math.Max(0, amount),
                DurationTicks = Math.Max(1, spec.DurationTicks),
                SourceInstanceId = actor.InstanceId,
                FromEnemy = fromEnemy,
                TargetItems = targets.Items.Count > 0 ? effectiveTargets : new List<ItemState>(),
            });
            if (spec.Effect == BuqiEffect.Delay && fromEnemy)
                interferedSides.Add(targets.Side);
            AppendEvent(
                ref nextSequence, log, tick, BuqiEventPhase.Aggregate, declaration.ChainDepth,
                declaration.ChainId, actor.InstanceId, actor.InstanceId,
                effectiveTargets[0].InstanceId, BuqiEventType.Effect, amount,
                spec.GetEffectId(), spec.ReasonCode);
        }

        /// <summary>
        /// 按契约固定顺序聚合一个阵营：新增护体、普通伤害吸收、失衡事故、劫火直接伤害，最后写入修正。
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
            TickAccumulator accumulator)
        {
            if (amount <= 0)
                return;

            var targetItems = new List<ItemState>(targets.Items.Count > 0 ? targets.Items : targets.Side.Items);
            if (targetItems.Count == 0)
                return;

            accumulator.Freezes.Add(new PendingFreeze
            {
                DurationTicks = amount,
                SourceAnchorSlot = actor.AnchorSlot,
                SourceInstanceId = actor.InstanceId,
                ChainId = declaration.ChainId,
                EffectId = spec.GetEffectId(),
                TargetItems = targetItems,
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
            SortPending(accumulator.BurnDamage);
            SortPending(accumulator.Heal);
            SortPending(accumulator.PoisonDamage);
            SortPending(accumulator.Noise);
            SortPending(accumulator.OvertimeDamage);

            foreach (PendingAmount pending in accumulator.Buffer)
            {
                int before = side.Buffer;
                side.Buffer = Math.Min(BufferCap, side.Buffer + pending.Amount);
                int actual = side.Buffer - before;
                AppendPendingEvent(ref nextSequence, log, tick, pending, actual, "BufferGain");
                if (actual < pending.Amount)
                    AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount - actual, "BufferOverflow");
            }

            bool hadBuffer = side.Buffer > 0;
            foreach (PendingAmount pending in accumulator.NormalDamage)
                ApplyShieldedDamage(side, pending, ref nextSequence, log, tick, "BufferAbsorb", "Damage");
            foreach (PendingAmount pending in accumulator.BurnDamage)
                ApplyShieldedDamage(side, pending, ref nextSequence, log, tick, "BurnShieldAbsorb", "BurnDamage");
            if (hadBuffer && side.Buffer == 0)
                side.BufferLostPending = true;

            foreach (PendingAmount pending in accumulator.Heal)
            {
                int before = side.Execution;
                side.Execution = Math.Min(side.MaxExecution, side.Execution + pending.Amount);
                int actual = side.Execution - before;
                AppendPendingEvent(ref nextSequence, log, tick, pending, actual, pending.ReasonCode);
                if (actual < pending.Amount)
                    AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount - actual, "HealOverflow");
            }

            foreach (PendingAmount pending in accumulator.PoisonDamage)
            {
                side.Execution -= pending.Amount;
                AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount, "PoisonDamage");
            }

            foreach (PendingAmount pending in accumulator.Noise)
            {
                side.Noise += pending.Amount;
                AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount, "NoiseChange");
                while (side.Noise >= NoiseThreshold)
                {
                    side.Noise -= NoiseThreshold;
                    side.Execution -= NoiseAccidentDamage;
                    AppendPendingEvent(
                        ref nextSequence, log, tick, pending,
                        NoiseAccidentDamage, "NoiseAccident");
                }
                side.Noise = Math.Max(side.Noise, 0);
            }

            foreach (PendingAmount pending in accumulator.OvertimeDamage)
            {
                side.Execution -= pending.Amount;
                AppendPendingEvent(ref nextSequence, log, tick, pending, pending.Amount, "OvertimeDamage");
            }

            foreach (PendingModifier modifier in accumulator.Modifiers)
                ApplyModifier(side, modifier);

            foreach (TimedStatus status in accumulator.NewStatuses)
                ApplyStatus(side, status, ref nextSequence, log, tick);

            foreach (PendingFreeze pending in accumulator.Freezes)
                ApplyFreeze(pending, ref nextSequence, log, tick);
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
                side.Execution -= actualDamage;
                AppendPendingEvent(ref nextSequence, log, tick, pending, actualDamage, damageReason);
            }
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
                int before = target.FrozenTicks;
                target.FrozenTicks = Math.Max(target.FrozenTicks, pending.DurationTicks);
                AppendEvent(
                    ref nextSequence, log, tick, BuqiEventPhase.Aggregate, 0,
                    pending.ChainId, pending.SourceInstanceId, pending.SourceInstanceId,
                    target.InstanceId, BuqiEventType.Effect,
                    target.FrozenTicks - before, pending.EffectId, "FreezeApplied");
            }
        }

        private static void ApplyModifier(SideState side, PendingModifier pending)
        {
            if (pending.TargetItems.Count == 0)
            {
                AddOrRefreshModifier(side.SideModifiers, pending);
                return;
            }

            foreach (ItemState target in pending.TargetItems)
                AddOrRefreshModifier(target.Modifiers, pending);
        }

        private static void AddOrRefreshModifier(List<TimedModifier> modifiers, PendingModifier pending)
        {
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
                    status.SourceInstanceId != pending.SourceInstanceId)
                {
                    continue;
                }

                status.Amount = Math.Max(status.Amount, pending.Amount);
                status.RemainingTicks = Math.Max(status.RemainingTicks, pending.RemainingTicks);
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
            BuqiEffectSpec spec,
            bool rewrite,
            int declaredCharge)
        {
            int qualityBps = actor.Quality == (int)BuqiQuality.Improved
                ? 16000
                : actor.Quality == (int)BuqiQuality.Fixed ? 24000 : 10000;
            IBuqiRefinementRule refinement = BuqiRefinementRuleCatalog.GetOrDefault(actor.AnnotationId);
            int annotationBps = refinement.GetEffectMultiplierBps(
                spec.Effect,
                spec.Trigger == BuqiTrigger.OnBattleStart);

            int rewriteBps = rewrite ? 5000 : 10000;
            long chargeAmount = (long)spec.Amount + (long)declaredCharge * spec.AmountPerCharge;
            long numerator = chargeAmount * qualityBps * annotationBps * rewriteBps;
            const long Denominator = 10000L * 10000L * 10000L;
            if (numerator >= 0)
                return (int)((numerator + Denominator / 2) / Denominator);
            return (int)((numerator - Denominator / 2) / Denominator);
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

        /// <summary>
        /// 劫火每秒向双方增加一次直接伤害；先写入 accumulator，待 Aggregate 与其它效果同时结算。
        /// 这样 tick 450 的劫火不会绕过护体顺序，也不会因左右处理顺序产生偏差。
        /// </summary>
        private static void EnqueueStatusTicks(SideState side, TickAccumulator accumulator)
        {
            for (int index = side.Statuses.Count - 1; index >= 0; index--)
            {
                TimedStatus status = side.Statuses[index];
                status.RemainingTicks--;
                status.TickProgressTicks++;

                if (status.TickProgressTicks >= Math.Max(1, status.TickIntervalTicks))
                {
                    status.TickProgressTicks = 0;
                    PendingAmount pending = CreateStatusPending(status);
                    if (pending.Amount > 0)
                    {
                        if (status.Effect == BuqiEffect.Regen)
                            accumulator.Heal.Add(pending);
                        else if (status.Effect == BuqiEffect.Poison)
                            accumulator.PoisonDamage.Add(pending);
                        else if (status.Effect == BuqiEffect.Burn)
                            accumulator.BurnDamage.Add(pending);
                    }
                }

                if (status.RemainingTicks <= 0)
                    side.Statuses.RemoveAt(index);
            }
        }

        private static PendingAmount CreateStatusPending(TimedStatus status)
        {
            string reason = status.Effect == BuqiEffect.Regen
                ? "Regen"
                : status.Effect == BuqiEffect.Poison ? "PoisonDamage" : "BurnDamage";
            return new PendingAmount
            {
                Amount = status.Amount,
                SourceAnchorSlot = status.SourceAnchorSlot,
                SourceInstanceId = status.SourceInstanceId,
                ChainId = status.ChainId,
                EffectId = status.EffectId,
                ReasonCode = reason,
            };
        }

        private static void EnqueueOvertimeDamage(int tick, TickAccumulator accumulator)
        {
            if (tick < NormalTickCount || (tick - NormalTickCount) % 10 != 0)
                return;
            int completedOvertimeSeconds = (tick - NormalTickCount) / 10;
            accumulator.OvertimeDamage.Add(new PendingAmount
            {
                Amount = 2 + completedOvertimeSeconds / 5,
                SourceAnchorSlot = int.MaxValue,
                SourceInstanceId = string.Empty,
                ChainId = BuqiText.Format("overtime@{0}", tick),
                ReasonCode = "Overtime",
            });
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
            accumulator.BurnDamage.Clear();
            accumulator.Heal.Clear();
            accumulator.PoisonDamage.Clear();
            accumulator.Noise.Clear();
            accumulator.OvertimeDamage.Clear();
            accumulator.NewStatuses.Clear();
            accumulator.Freezes.Clear();
            accumulator.Modifiers.Clear();
        }

        private static Dictionary<string, int> BuildAnchorLookup(SideState left, SideState right)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ItemState item in left.Items)
                result[item.InstanceId] = item.AnchorSlot;
            foreach (ItemState item in right.Items)
            {
                if (!result.ContainsKey(item.InstanceId))
                    result[item.InstanceId] = item.AnchorSlot;
            }
            return result;
        }

        private static void SortQueue(List<DeclaredEffect> queue)
        {
            for (int index = 0; index < queue.Count; index++)
                queue[index].DeclarationOrder = index;

            queue.Sort((left, right) =>
            {
                int depthComparison = left.ChainDepth.CompareTo(right.ChainDepth);
                if (depthComparison != 0) return depthComparison;
                int anchorComparison = left.Actor.AnchorSlot.CompareTo(right.Actor.AnchorSlot);
                if (anchorComparison != 0) return anchorComparison;
                int idComparison = string.CompareOrdinal(left.Actor.InstanceId, right.Actor.InstanceId);
                if (idComparison != 0) return idComparison;
                return left.DeclarationOrder.CompareTo(right.DeclarationOrder);
            });
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
        /// 按 tick、阶段、链深、来源锚点、来源实例、事件类型和原始序号重排，并重新连续编号。
        /// 这是回放、日志哈希和跨端比较的共同稳定排序键。
        /// </summary>
        private static void SortAndResequenceLog(
            List<BattleEvent> log,
            Dictionary<string, int> anchorByInstanceId)
        {
            log.Sort((left, right) =>
            {
                int comparison = left.Tick.CompareTo(right.Tick);
                if (comparison != 0) return comparison;
                comparison = left.Phase.CompareTo(right.Phase);
                if (comparison != 0) return comparison;
                comparison = left.ChainDepth.CompareTo(right.ChainDepth);
                if (comparison != 0) return comparison;
                int leftAnchor = GetAnchor(left.SourceInstanceId, anchorByInstanceId);
                int rightAnchor = GetAnchor(right.SourceInstanceId, anchorByInstanceId);
                comparison = leftAnchor.CompareTo(rightAnchor);
                if (comparison != 0) return comparison;
                comparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
                if (comparison != 0) return comparison;
                comparison = left.Type.CompareTo(right.Type);
                if (comparison != 0) return comparison;
                return left.Sequence.CompareTo(right.Sequence);
            });
            for (int index = 0; index < log.Count; index++)
                log[index].Sequence = index;
        }

        private static int GetAnchor(string instanceId, Dictionary<string, int> anchorByInstanceId)
        {
            return instanceId != null && anchorByInstanceId.TryGetValue(instanceId, out int anchor)
                ? anchor
                : int.MaxValue;
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

        /// <summary>硬上限比较顺序固定为执行值、护体、失衡；三项都相同才平局。</summary>
        private static BattleOutcome DecideHardCap(SideState left, SideState right)
        {
            if (left.Execution != right.Execution)
                return left.Execution > right.Execution ? BattleOutcome.LeftWin : BattleOutcome.RightWin;
            if (left.Buffer != right.Buffer)
                return left.Buffer > right.Buffer ? BattleOutcome.LeftWin : BattleOutcome.RightWin;
            if (left.Noise != right.Noise)
                return left.Noise < right.Noise ? BattleOutcome.LeftWin : BattleOutcome.RightWin;
            return BattleOutcome.Draw;
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
            return (int)(((long)value * bps + 5000) / 10000);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
