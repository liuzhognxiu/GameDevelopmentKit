using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;

namespace Game.Hot.Buqi.Run.Settlement
{
    public static class BuqiRunBattleSummaryBuilder
    {
        public static BuqiRunBattleSummary Build(BattleResult result, IReadOnlyList<BattleEvent> log)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var summary = new BuqiRunBattleSummary
            {
                RawOutcome = result.Outcome,
                BattleLogHash = result.BattleLogHash ?? string.Empty,
            };

            if (log == null || log.Count == 0)
            {
                return summary;
            }

            var ordered = new List<BattleEvent>(log.Count);
            for (int index = 0; index < log.Count; index++)
            {
                BattleEvent battleEvent = log[index];
                if (battleEvent == null)
                {
                    throw new ArgumentException("Battle log contains a null event.", nameof(log));
                }

                ordered.Add(battleEvent);
            }

            ordered.Sort(CompareEvents);

            var contributions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BattleEvent battleEvent in ordered)
            {
                if (battleEvent.Type == BuqiEventType.Effect &&
                    battleEvent.Amount > 0 &&
                    !string.IsNullOrEmpty(battleEvent.SourceInstanceId))
                {
                    contributions.TryGetValue(battleEvent.SourceInstanceId, out int total);
                    contributions[battleEvent.SourceInstanceId] = total + battleEvent.Amount;
                }

                if (string.IsNullOrEmpty(summary.KeyInterruptionReason) &&
                    IsInterruptionEvent(battleEvent.Type) &&
                    !string.IsNullOrEmpty(battleEvent.ReasonCode))
                {
                    summary.KeyInterruptionReason = battleEvent.ReasonCode;
                }

                if (battleEvent.Amount > summary.OverloadLoss &&
                    IsRiskReason(battleEvent.ReasonCode))
                {
                    summary.OverloadLoss = battleEvent.Amount;
                }
            }

            foreach (KeyValuePair<string, int> pair in contributions)
            {
                if (pair.Value > summary.TopContribution)
                {
                    summary.TopSourceInstanceId = pair.Key;
                    summary.TopContribution = pair.Value;
                    continue;
                }

                if (pair.Value == summary.TopContribution &&
                    pair.Value > 0 &&
                    (string.IsNullOrEmpty(summary.TopSourceInstanceId) ||
                     string.CompareOrdinal(pair.Key, summary.TopSourceInstanceId) < 0))
                {
                    summary.TopSourceInstanceId = pair.Key;
                }
            }

            if (!string.IsNullOrEmpty(summary.TopSourceInstanceId))
            {
                summary.FactLines.Add(BuqiText.Format(
                    "主要贡献：{0} 累计 {1}",
                    summary.TopSourceInstanceId,
                    summary.TopContribution));
            }

            if (!string.IsNullOrEmpty(summary.KeyInterruptionReason))
            {
                summary.FactLines.Add(BuqiText.Format(
                    "关键中断：{0}",
                    summary.KeyInterruptionReason));
            }

            if (summary.OverloadLoss > 0)
            {
                summary.FactLines.Add(BuqiText.Format(
                    "风险损失：{0}",
                    summary.OverloadLoss));
            }

            return summary;
        }

        private static int CompareEvents(BattleEvent left, BattleEvent right)
        {
            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
            {
                return sequence;
            }

            int tick = left.Tick.CompareTo(right.Tick);
            if (tick != 0)
            {
                return tick;
            }

            int type = left.Type.CompareTo(right.Type);
            if (type != 0)
            {
                return type;
            }

            int source = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
            if (source != 0)
            {
                return source;
            }

            return string.CompareOrdinal(left.ReasonCode, right.ReasonCode);
        }

        private static bool IsInterruptionEvent(BuqiEventType eventType)
        {
            return eventType == BuqiEventType.Truncate ||
                   eventType == BuqiEventType.NoTarget ||
                   eventType == BuqiEventType.Immune ||
                   eventType == BuqiEventType.Invalid;
        }

        private static bool IsRiskReason(string reasonCode)
        {
            if (string.IsNullOrEmpty(reasonCode))
            {
                return false;
            }

            return string.Equals(reasonCode, "NoiseAccident", StringComparison.Ordinal) ||
                   string.Equals(reasonCode, "StormDamage", StringComparison.Ordinal) ||
                   string.Equals(reasonCode, "PoisonDamage", StringComparison.Ordinal) ||
                   string.Equals(reasonCode, "BurnDamage", StringComparison.Ordinal) ||
                   reasonCode.IndexOf("Overflow", StringComparison.Ordinal) >= 0 ||
                   reasonCode.IndexOf("Overload", StringComparison.Ordinal) >= 0;
        }
    }
}
