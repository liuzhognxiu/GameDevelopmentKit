using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public sealed class BattleReplayController
    {
        private enum ReplaySide
        {
            Left,
            Right,
        }

        private const int BoardSlotCount = BuqiBoardValidator.BoardSlotCount;
        private const float TickSeconds = 0.1f;

        private readonly Dictionary<string, ReplaySide> m_InstanceSides =
            new Dictionary<string, ReplaySide>(StringComparer.Ordinal);
        private readonly Dictionary<string, BattleReplayItemFrame> m_Items =
            new Dictionary<string, BattleReplayItemFrame>(StringComparer.Ordinal);
        private readonly Dictionary<string, BattleReplayEffectInfo> m_Effects =
            new Dictionary<string, BattleReplayEffectInfo>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> m_DeclareTicks =
            new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly IReadOnlyList<BattleReplayFeedbackEvent> m_FeedbackEvents;

        private int m_EventCursor;
        private int m_Speed = 1;
        private float m_PresentationTick;
        private int m_SourceLessTick = -1;
        private int m_SourceLessIndex;
        private BattleReplayFilter m_Filter = new BattleReplayFilter();

        public BattleReplayController(BattleReplayData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            ValidateData(data);
            BuildEffectLookup(data);
            BuildDeclareLookup(data.Log);
            ResetProjection();
            m_FeedbackEvents = BuildFeedbackEvents(data.Log).AsReadOnly();
        }

        public BattleReplayData Data { get; }

        public BattleReplayFrame Frame { get; private set; }

        public int Speed => m_Speed;

        public float PresentationSeconds => m_PresentationTick * TickSeconds;

        public IReadOnlyList<BattleReplayFeedbackEvent> FeedbackEvents => m_FeedbackEvents;

        public void SetSpeed(int speed)
        {
            if (speed != 1 && speed != 2)
                throw new ArgumentOutOfRangeException(nameof(speed), "Replay speed must be 1 or 2.");
            m_Speed = speed;
        }

        public void Advance(float realSeconds)
        {
            if (realSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (Frame.IsFinished || !string.IsNullOrEmpty(Frame.Error))
                return;

            m_PresentationTick += realSeconds * m_Speed / TickSeconds;
            int targetTick = Math.Min((int)m_PresentationTick, Data.Result.DurationTicks);
            ProjectTo(targetTick);
        }

        public void SkipToResult()
        {
            ResetProjection();
            m_PresentationTick = Data.Result.DurationTicks;
            ProjectTo(Data.Result.DurationTicks);
        }

        public void SetFilter(BattleReplayFilter filter)
        {
            m_Filter = filter == null
                ? new BattleReplayFilter()
                : new BattleReplayFilter
                {
                    KeyOnly = filter.KeyOnly,
                    SourceInstanceId = filter.SourceInstanceId ?? string.Empty,
                    TargetInstanceId = filter.TargetInstanceId ?? string.Empty,
                    ChainId = filter.ChainId ?? string.Empty,
                    ReasonCode = filter.ReasonCode ?? string.Empty,
                };
        }

        public BattleReplayLogPage GetLogPage(int pageIndex)
        {
            var filtered = new List<BattleReplayLogRow>();
            foreach (BattleEvent battleEvent in Data.Log)
            {
                if (!MatchesFilter(battleEvent))
                    continue;
                filtered.Add(new BattleReplayLogRow
                {
                    Event = battleEvent,
                    Summary = FormatEvent(battleEvent),
                });
            }

            int pageCount = Math.Max(1, (filtered.Count + 11) / 12);
            int safePage = Math.Max(0, Math.Min(pageIndex, pageCount - 1));
            int start = safePage * 12;
            int count = Math.Min(12, filtered.Count - start);
            var rows = count > 0 ? filtered.GetRange(start, count) : new List<BattleReplayLogRow>();
            return new BattleReplayLogPage
            {
                PageIndex = safePage,
                PageCount = pageCount,
                Rows = rows,
            };
        }

        public IReadOnlyList<BattleReplayFact> GetFacts()
        {
            BattleEvent contribution = FindLargestEvent(IsContributionEvent);
            BattleEvent chain = FindChainEvent();
            BattleEvent risk = FindLargestEvent(IsRiskEvent);
            BattleEvent fallback = Data.Log.Count > 0 ? Data.Log[0] : null;
            return new List<BattleReplayFact>
            {
                CreateFact("Contribution", "最大有效贡献", contribution ?? fallback),
                CreateFact("Chain", "关键连锁或中断", chain ?? fallback),
                CreateFact("Risk", "最大风险账单", risk ?? contribution ?? fallback),
            };
        }

        private void ResetProjection()
        {
            m_EventCursor = 0;
            m_PresentationTick = 0f;
            m_SourceLessTick = -1;
            m_SourceLessIndex = 0;
            m_InstanceSides.Clear();
            m_Items.Clear();

            BattleReplaySideFrame left = CreateInitialSide(Data.LeftBuild, Data.Definitions, ReplaySide.Left);
            BattleReplaySideFrame right = CreateInitialSide(Data.RightBuild, Data.Definitions, ReplaySide.Right);
            Frame = new BattleReplayFrame
            {
                Tick = 0,
                Left = left,
                Right = right,
                CurrentEvent = null,
                IsFinished = false,
                Error = string.Empty,
            };
        }

        private void ProjectTo(int targetTick)
        {
            while (m_EventCursor < Data.Log.Count && Data.Log[m_EventCursor].Tick <= targetTick)
            {
                BattleEvent battleEvent = Data.Log[m_EventCursor++];
                ApplyEvent(battleEvent);
                Frame.CurrentEvent = battleEvent;
            }

            Frame.Tick = targetTick;
            UpdateCooldowns(targetTick);
            Frame.IsFinished = targetTick >= Data.Result.DurationTicks;
            if (Frame.IsFinished)
                ValidateFinalFrame();
        }

        private void ApplyEvent(BattleEvent battleEvent)
        {
            if (battleEvent.Type != BuqiEventType.Effect)
                return;

            if (TryGetEffect(battleEvent.EffectId, out BattleReplayEffectInfo effectInfo))
            {
                if (effectInfo.Effect == BuqiEffect.Charge)
                {
                    if (m_Items.TryGetValue(battleEvent.TargetInstanceId, out BattleReplayItemFrame chargeTarget))
                        chargeTarget.Charge += battleEvent.Amount;
                    return;
                }

                if (effectInfo.Effect == BuqiEffect.Freeze)
                {
                    if (m_Items.TryGetValue(battleEvent.TargetInstanceId, out BattleReplayItemFrame freezeTarget))
                        freezeTarget.FrozenTicks += battleEvent.Amount;
                    return;
                }
            }

            BattleReplaySideFrame side = GetSideFrame(ResolveTargetSide(battleEvent, effectInfo));
            switch (battleEvent.ReasonCode)
            {
                case "BufferGain":
                    side.Buffer += battleEvent.Amount;
                    return;
                case "BufferOverflow":
                case "HealOverflow":
                    return;
                case "BufferAbsorb":
                case "BurnShieldAbsorb":
                    side.Buffer -= battleEvent.Amount;
                    return;
                case "Damage":
                case "BurnDamage":
                case "PoisonDamage":
                case "OvertimeDamage":
                    side.Execution -= battleEvent.Amount;
                    return;
                case "NoiseChange":
                    side.Noise += battleEvent.Amount;
                    return;
                case "NoiseAccident":
                    side.Noise -= BuqiBattleSimulator.NoiseThreshold;
                    side.Execution -= battleEvent.Amount;
                    return;
            }

            if (effectInfo != null &&
                (effectInfo.Effect == BuqiEffect.Heal || effectInfo.Effect == BuqiEffect.Regen))
            {
                side.Execution = Math.Min(side.MaxExecution, side.Execution + battleEvent.Amount);
            }
        }

        private ReplaySide ResolveTargetSide(
            BattleEvent battleEvent,
            BattleReplayEffectInfo effectInfo)
        {
            if (!string.IsNullOrEmpty(battleEvent.TargetInstanceId) &&
                m_InstanceSides.TryGetValue(battleEvent.TargetInstanceId, out ReplaySide targetSide))
            {
                return targetSide;
            }

            if (!string.IsNullOrEmpty(battleEvent.SourceInstanceId) &&
                m_InstanceSides.TryGetValue(battleEvent.SourceInstanceId, out ReplaySide sourceSide))
            {
                return effectInfo != null && IsEnemyTarget(effectInfo.Target)
                    ? Opposite(sourceSide)
                    : sourceSide;
            }

            if (battleEvent.ReasonCode == "OvertimeDamage")
            {
                if (m_SourceLessTick != battleEvent.Tick)
                {
                    m_SourceLessTick = battleEvent.Tick;
                    m_SourceLessIndex = 0;
                }

                return m_SourceLessIndex++ % 2 == 0 ? ReplaySide.Left : ReplaySide.Right;
            }

            throw new InvalidOperationException(
                $"Replay event target side cannot be resolved at sequence {battleEvent.Sequence}.");
        }

        private BattleReplaySideFrame GetSideFrame(ReplaySide side)
        {
            return side == ReplaySide.Left ? Frame.Left : Frame.Right;
        }

        private static ReplaySide Opposite(ReplaySide side)
        {
            return side == ReplaySide.Left ? ReplaySide.Right : ReplaySide.Left;
        }

        private static bool IsEnemyTarget(BuqiTarget target)
        {
            return target == BuqiTarget.EnemyExecution ||
                   target == BuqiTarget.ShortestCooldownEnemyItem ||
                   target == BuqiTarget.LongestCooldownEnemyItem ||
                   target == BuqiTarget.LeftmostEnemyItem ||
                   target == BuqiTarget.RightmostEnemyItem;
        }

        private bool TryGetEffect(string effectId, out BattleReplayEffectInfo effectInfo)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                effectInfo = null;
                return false;
            }
            return m_Effects.TryGetValue(effectId, out effectInfo);
        }

        private BattleReplaySideFrame CreateInitialSide(
            BuildSnapshot build,
            IItemDefinitionProvider definitions,
            ReplaySide side)
        {
            var items = new List<BattleReplayItemFrame>();
            var slots = new string[BoardSlotCount];
            var instances = new List<ItemInstance>(build.Items);
            instances.Sort((left, right) =>
            {
                int anchor = left.AnchorSlot.CompareTo(right.AnchorSlot);
                return anchor != 0 ? anchor : string.CompareOrdinal(left.InstanceId, right.InstanceId);
            });

            foreach (ItemInstance instance in instances)
            {
                if (!definitions.TryGet(instance.DefinitionId, out BuqiItemDefinition definition))
                    throw new ArgumentException($"Replay item definition is missing: {instance.DefinitionId}");

                int size = (int)definition.Size;
                if (instance.AnchorSlot < 0 || instance.AnchorSlot + size > BoardSlotCount)
                    throw new ArgumentException($"Replay item is outside the board: {instance.InstanceId}");

                for (int slot = instance.AnchorSlot; slot < instance.AnchorSlot + size; slot++)
                {
                    if (!string.IsNullOrEmpty(slots[slot]))
                        throw new ArgumentException($"Replay item overlaps slot {slot}: {instance.InstanceId}");
                    slots[slot] = instance.InstanceId;
                }

                var item = new BattleReplayItemFrame
                {
                    InstanceId = instance.InstanceId,
                    DefinitionId = instance.DefinitionId,
                    AnchorSlot = instance.AnchorSlot,
                    Size = size,
                    Cooldown01 = 0f,
                };
                items.Add(item);
                m_Items.Add(item.InstanceId, item);
                m_InstanceSides.Add(item.InstanceId, side);
            }

            for (int slot = 0; slot < slots.Length; slot++)
                slots[slot] ??= string.Empty;

            return new BattleReplaySideFrame
            {
                Execution = build.InitialExecution,
                MaxExecution = Math.Max(BuqiBattleSimulator.DefaultMaxExecution, build.InitialExecution),
                Buffer = build.InitialBuffer,
                Noise = build.InitialNoiseDebt,
                Items = items,
                Slots = slots,
            };
        }

        private void BuildEffectLookup(BattleReplayData data)
        {
            foreach (KeyValuePair<string, BattleReplayEffectInfo> pair in data.Effects)
                m_Effects[pair.Key] = pair.Value;
            AddBuildEffects(data.LeftBuild, data.Definitions);
            AddBuildEffects(data.RightBuild, data.Definitions);
        }

        private void BuildDeclareLookup(IReadOnlyList<BattleEvent> log)
        {
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Type != BuqiEventType.Declare ||
                    string.IsNullOrEmpty(battleEvent.ActorInstanceId))
                {
                    continue;
                }

                if (!m_DeclareTicks.TryGetValue(battleEvent.ActorInstanceId, out List<int> ticks))
                {
                    ticks = new List<int>();
                    m_DeclareTicks.Add(battleEvent.ActorInstanceId, ticks);
                }

                if (ticks.Count == 0 || ticks[ticks.Count - 1] != battleEvent.Tick)
                    ticks.Add(battleEvent.Tick);
            }
        }

        private List<BattleReplayFeedbackEvent> BuildFeedbackEvents(IReadOnlyList<BattleEvent> log)
        {
            var feedback = new List<BattleReplayFeedbackEvent>();
            int previousTick = -1;
            int orderWithinTick = 0;
            int sourceLessIndex = 0;
            foreach (BattleEvent battleEvent in log)
            {
                if (battleEvent.Tick != previousTick)
                {
                    previousTick = battleEvent.Tick;
                    orderWithinTick = 0;
                    sourceLessIndex = 0;
                }

                if (TryCreateFeedback(
                        battleEvent,
                        orderWithinTick,
                        ref sourceLessIndex,
                        out BattleReplayFeedbackEvent item))
                {
                    feedback.Add(item);
                    orderWithinTick++;
                }
            }
            return feedback;
        }

        private bool TryCreateFeedback(
            BattleEvent battleEvent,
            int orderWithinTick,
            ref int sourceLessIndex,
            out BattleReplayFeedbackEvent feedback)
        {
            feedback = null;
            if (!TryResolveFeedbackKind(battleEvent, out BattleReplayFeedbackKind kind))
                return false;

            if (!TryResolveFeedbackAnchor(
                    battleEvent,
                    kind,
                    ref sourceLessIndex,
                    out ReplaySide side,
                    out BattleReplayItemFrame item))
                return false;

            feedback = new BattleReplayFeedbackEvent(
                battleEvent.Sequence,
                kind,
                side == ReplaySide.Left
                    ? BattleReplayFeedbackSide.Left
                    : BattleReplayFeedbackSide.Right,
                item.AnchorSlot,
                Math.Abs(battleEvent.Amount),
                battleEvent.Tick * TickSeconds + orderWithinTick * 0.05f,
                0.8f);
            return true;
        }

        private bool TryResolveFeedbackAnchor(
            BattleEvent battleEvent,
            BattleReplayFeedbackKind kind,
            ref int sourceLessIndex,
            out ReplaySide side,
            out BattleReplayItemFrame item)
        {
            string sourceId = FirstNonEmpty(
                battleEvent.ActorInstanceId,
                battleEvent.SourceInstanceId);
            if (kind == BattleReplayFeedbackKind.Attack)
                return TryGetFeedbackItem(sourceId, out side, out item);

            if (TryGetFeedbackItem(battleEvent.TargetInstanceId, out side, out item))
                return true;

            int preferredSlot = 0;
            if (TryGetFeedbackItem(sourceId, out ReplaySide sourceSide, out BattleReplayItemFrame sourceItem))
            {
                preferredSlot = sourceItem.AnchorSlot;
                side = sourceSide;
                if (TryGetEffect(battleEvent.EffectId, out BattleReplayEffectInfo effectInfo) &&
                    IsEnemyTarget(effectInfo.Target))
                {
                    side = Opposite(side);
                }
            }
            else if (battleEvent.ReasonCode == "OvertimeDamage")
            {
                side = sourceLessIndex++ % 2 == 0 ? ReplaySide.Left : ReplaySide.Right;
            }
            else
            {
                side = default;
                item = null;
                return false;
            }

            return TryGetNearestFeedbackItem(side, preferredSlot, out item);
        }

        private bool TryGetFeedbackItem(
            string instanceId,
            out ReplaySide side,
            out BattleReplayItemFrame item)
        {
            if (!string.IsNullOrEmpty(instanceId) &&
                m_Items.TryGetValue(instanceId, out item) &&
                m_InstanceSides.TryGetValue(instanceId, out side))
            {
                return true;
            }

            side = default;
            item = null;
            return false;
        }

        private bool TryGetNearestFeedbackItem(
            ReplaySide side,
            int preferredSlot,
            out BattleReplayItemFrame item)
        {
            IReadOnlyList<BattleReplayItemFrame> items = side == ReplaySide.Left
                ? Frame.Left.Items
                : Frame.Right.Items;
            item = null;
            int bestDistance = int.MaxValue;
            foreach (BattleReplayItemFrame candidate in items)
            {
                int distance = Math.Abs(candidate.AnchorSlot - preferredSlot);
                if (item == null || distance < bestDistance ||
                    (distance == bestDistance && candidate.AnchorSlot < item.AnchorSlot))
                {
                    item = candidate;
                    bestDistance = distance;
                }
            }
            return item != null;
        }

        private bool TryResolveFeedbackKind(
            BattleEvent battleEvent,
            out BattleReplayFeedbackKind kind)
        {
            if (battleEvent.Type == BuqiEventType.Declare)
            {
                if (!TryGetEffect(battleEvent.EffectId, out BattleReplayEffectInfo declaredEffect))
                {
                    kind = default;
                    return false;
                }

                switch (declaredEffect.Effect)
                {
                    case BuqiEffect.Damage:
                    case BuqiEffect.Burn:
                    case BuqiEffect.Poison:
                        kind = BattleReplayFeedbackKind.Attack;
                        return true;
                    case BuqiEffect.Buffer:
                        kind = BattleReplayFeedbackKind.Guard;
                        return true;
                    case BuqiEffect.Heal:
                    case BuqiEffect.Regen:
                        kind = BattleReplayFeedbackKind.Heal;
                        return true;
                    default:
                        kind = default;
                        return false;
                }
            }

            if (battleEvent.Type != BuqiEventType.Effect || battleEvent.Amount == 0)
            {
                kind = default;
                return false;
            }

            switch (battleEvent.ReasonCode)
            {
                case "Damage":
                case "BurnDamage":
                case "PoisonDamage":
                case "OvertimeDamage":
                case "NoiseAccident":
                case "BufferAbsorb":
                case "BurnShieldAbsorb":
                    kind = BattleReplayFeedbackKind.Damage;
                    return true;
                case "BufferGain":
                    kind = BattleReplayFeedbackKind.Guard;
                    return true;
                case "Heal":
                case "Regen":
                    kind = BattleReplayFeedbackKind.Heal;
                    return true;
            }

            if (TryGetEffect(battleEvent.EffectId, out BattleReplayEffectInfo info) &&
                (info.Effect == BuqiEffect.Heal || info.Effect == BuqiEffect.Regen))
            {
                kind = BattleReplayFeedbackKind.Heal;
                return true;
            }

            kind = default;
            return false;
        }

        private static string FirstNonEmpty(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }

        private void UpdateCooldowns(int tick)
        {
            foreach (KeyValuePair<string, BattleReplayItemFrame> pair in m_Items)
            {
                BattleReplayItemFrame item = pair.Value;
                if (!m_DeclareTicks.TryGetValue(pair.Key, out List<int> ticks) || ticks.Count == 0)
                {
                    item.Cooldown01 = 0f;
                    continue;
                }

                if (tick < ticks[0])
                {
                    item.Cooldown01 = ticks[0] <= 0 ? 0f : tick / (float)ticks[0];
                    continue;
                }

                bool foundInterval = false;
                for (int index = 0; index < ticks.Count - 1; index++)
                {
                    int start = ticks[index];
                    int end = ticks[index + 1];
                    if (tick < end)
                    {
                        item.Cooldown01 = end == start ? 1f : (tick - start) / (float)(end - start);
                        foundInterval = true;
                        break;
                    }
                }

                if (!foundInterval)
                    item.Cooldown01 = 1f;
            }
        }

        private bool MatchesFilter(BattleEvent battleEvent)
        {
            if (m_Filter.KeyOnly && !IsKeyEvent(battleEvent))
                return false;
            if (!Matches(m_Filter.SourceInstanceId, battleEvent.SourceInstanceId))
                return false;
            if (!Matches(m_Filter.TargetInstanceId, battleEvent.TargetInstanceId))
                return false;
            if (!Matches(m_Filter.ChainId, battleEvent.ChainId))
                return false;
            return Matches(m_Filter.ReasonCode, battleEvent.ReasonCode);
        }

        private static bool Matches(string filter, string value)
        {
            return string.IsNullOrEmpty(filter) ||
                   string.Equals(filter, value ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool IsKeyEvent(BattleEvent battleEvent)
        {
            return battleEvent.Type != BuqiEventType.Effect ||
                   battleEvent.ChainDepth > 0 ||
                   battleEvent.ReasonCode == "NoiseAccident" ||
                   battleEvent.ReasonCode == "OvertimeDamage";
        }

        private static string FormatEvent(BattleEvent battleEvent)
        {
            return BuqiText.Format(
                "第 {0} 时刻 | {1} {2}",
                battleEvent.Tick,
                BuqiBattleText.EventReason(battleEvent.ReasonCode),
                battleEvent.Amount);
        }

        private BattleEvent FindLargestEvent(Func<BattleEvent, bool> predicate)
        {
            BattleEvent selected = null;
            foreach (BattleEvent battleEvent in Data.Log)
            {
                if (!predicate(battleEvent))
                    continue;
                if (selected == null || Math.Abs(battleEvent.Amount) > Math.Abs(selected.Amount))
                    selected = battleEvent;
            }
            return selected;
        }

        private BattleEvent FindChainEvent()
        {
            BattleEvent selected = null;
            foreach (BattleEvent battleEvent in Data.Log)
            {
                if (battleEvent.Type == BuqiEventType.Truncate ||
                    battleEvent.Type == BuqiEventType.NoTarget ||
                    battleEvent.Type == BuqiEventType.Immune)
                {
                    return battleEvent;
                }
                if (selected == null || battleEvent.ChainDepth > selected.ChainDepth)
                    selected = battleEvent;
            }
            return selected;
        }

        private bool IsContributionEvent(BattleEvent battleEvent)
        {
            if (battleEvent.Type != BuqiEventType.Effect || battleEvent.Amount <= 0)
                return false;
            if (battleEvent.ReasonCode == "Damage" ||
                battleEvent.ReasonCode == "BurnDamage" ||
                battleEvent.ReasonCode == "PoisonDamage" ||
                battleEvent.ReasonCode == "BufferGain")
            {
                return true;
            }
            return TryGetEffect(battleEvent.EffectId, out BattleReplayEffectInfo info) &&
                   (info.Effect == BuqiEffect.Heal || info.Effect == BuqiEffect.Regen);
        }

        private static bool IsRiskEvent(BattleEvent battleEvent)
        {
            return battleEvent.ReasonCode == "NoiseAccident" ||
                   battleEvent.ReasonCode == "OvertimeDamage" ||
                   battleEvent.ReasonCode == "PoisonDamage" ||
                   battleEvent.ReasonCode == "BurnDamage";
        }

        private static BattleReplayFact CreateFact(string kind, string label, BattleEvent battleEvent)
        {
            if (battleEvent == null)
            {
                return new BattleReplayFact
                {
                    Kind = kind,
                    Summary = label,
                    EventSequences = Array.Empty<int>(),
                };
            }

            return new BattleReplayFact
            {
                Kind = kind,
                Summary = BuqiText.Format("{0}：第 {1} 时刻，{2} {3}",
                    label,
                    battleEvent.Tick,
                    BuqiBattleText.EventReason(battleEvent.ReasonCode),
                    battleEvent.Amount),
                EventSequences = new List<int> { battleEvent.Sequence },
            };
        }

        private void AddBuildEffects(BuildSnapshot build, IItemDefinitionProvider definitions)
        {
            foreach (ItemInstance instance in build.Items)
            {
                definitions.TryGet(instance.DefinitionId, out BuqiItemDefinition definition);
                foreach (BuqiEffectSpec spec in definition.Effects)
                {
                    string effectId = spec.GetEffectId();
                    m_Effects[effectId] = new BattleReplayEffectInfo
                    {
                        EffectId = effectId,
                        Effect = spec.Effect,
                        Target = spec.Target,
                    };
                }
            }
        }

        private static void ValidateData(BattleReplayData data)
        {
            if (data.LeftBuild == null || data.RightBuild == null)
                throw new ArgumentException("战斗回放缺少装备栏快照。"  );
            if (data.Result == null)
                throw new ArgumentException("战斗回放缺少战斗结果。"  );
            if (data.Log == null)
                throw new ArgumentException("战斗回放缺少战斗记录。"  );
            if (data.Definitions == null)
                throw new ArgumentException("战斗回放缺少装备定义。"  );

            int previousTick = -1;
            for (int index = 0; index < data.Log.Count; index++)
            {
                BattleEvent battleEvent = data.Log[index];
                if (battleEvent == null || battleEvent.Sequence != index || battleEvent.Tick < previousTick)
                    throw new ArgumentException("Replay battle log sequence is invalid.");
                previousTick = battleEvent.Tick;
            }

            string hash = BuqiCrypto.BattleLogHash(data.Result, new List<BattleEvent>(data.Log));
            if (!string.Equals(hash, data.Result.BattleLogHash, StringComparison.Ordinal))
                throw new ArgumentException("Replay battle log hash does not match the result.");
        }

        private void ValidateFinalFrame()
        {
            if (Frame.Left.Execution == Data.Result.LeftExecution &&
                Frame.Right.Execution == Data.Result.RightExecution &&
                Frame.Left.Buffer == Data.Result.LeftBuffer &&
                Frame.Right.Buffer == Data.Result.RightBuffer &&
                Frame.Left.Noise == Data.Result.LeftNoise &&
                Frame.Right.Noise == Data.Result.RightNoise)
            {
                return;
            }

            Frame.Error = "Replay projection does not match the recorded battle result.";
            Frame.IsFinished = false;
        }
    }

}
