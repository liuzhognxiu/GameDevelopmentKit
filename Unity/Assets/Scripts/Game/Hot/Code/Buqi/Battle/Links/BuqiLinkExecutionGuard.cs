using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public sealed class BuqiLinkExecutionGuard
    {
        private readonly BuqiLinkExecutionLimits m_Limits;
        private readonly Dictionary<int, int> m_TickCounts = new Dictionary<int, int>();
        private readonly Dictionary<string, int> m_ActiveUseCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_AbilityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_SignatureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_RuleTickCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_RuleActiveUseCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public BuqiLinkExecutionGuard(BuqiLinkExecutionLimits limits)
        {
            m_Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        }

        public bool TryEnter(BuqiLinkTriggerAttempt attempt, out string reasonCode)
        {
            if (attempt == null)
                throw new ArgumentNullException(nameof(attempt));
            if (attempt.ChainDepth > m_Limits.MaxChainDepth)
                return Reject("ChainDepthCapReached", out reasonCode);

            int tickCount = Get(m_TickCounts, attempt.Tick);
            if (tickCount >= m_Limits.MaxTriggersPerTick)
                return Reject("TickCapReached", out reasonCode);

            string activeUseKey = BuqiText.Format(
                "{0}|{1}",
                attempt.RootEventId,
                attempt.ActiveUseId);
            int activeUseCount = Get(m_ActiveUseCounts, activeUseKey);
            if (activeUseCount >= m_Limits.MaxTriggersPerActiveUse)
                return Reject("ActiveUseCapReached", out reasonCode);

            string abilityKey = BuqiText.Format(
                "{0}|{1}|{2}",
                attempt.RootEventId,
                attempt.SourceInstanceId,
                attempt.RuleId);
            int abilityCount = Get(m_AbilityCounts, abilityKey);
            if (abilityCount >= m_Limits.MaxAbilityFiresPerRoot)
                return Reject("AbilityRootCapReached", out reasonCode);

            string signature = BuqiText.Format(
                "{0}|{1}|{2}",
                abilityKey,
                attempt.TargetInstanceId,
                attempt.StateHash);
            int signatureCount = Get(m_SignatureCounts, signature);
            if (signatureCount >= m_Limits.MaxSignatureRepeats)
                return Reject("CycleSignatureCapReached", out reasonCode);

            string ruleTickKey = BuqiText.Format(
                "{0}|{1}",
                attempt.Tick,
                attempt.RuleId);
            int ruleTickCount = Get(m_RuleTickCounts, ruleTickKey);
            if (attempt.RuleMaxTriggersPerTick > 0 && ruleTickCount >= attempt.RuleMaxTriggersPerTick)
                return Reject("RuleTickCapReached", out reasonCode);

            string ruleUseKey = BuqiText.Format(
                "{0}|{1}",
                activeUseKey,
                attempt.RuleId);
            int ruleUseCount = Get(m_RuleActiveUseCounts, ruleUseKey);
            if (attempt.RuleMaxTriggersPerActiveUse > 0 && ruleUseCount >= attempt.RuleMaxTriggersPerActiveUse)
                return Reject("RuleActiveUseCapReached", out reasonCode);

            m_TickCounts[attempt.Tick] = tickCount + 1;
            m_ActiveUseCounts[activeUseKey] = activeUseCount + 1;
            m_AbilityCounts[abilityKey] = abilityCount + 1;
            m_SignatureCounts[signature] = signatureCount + 1;
            m_RuleTickCounts[ruleTickKey] = ruleTickCount + 1;
            m_RuleActiveUseCounts[ruleUseKey] = ruleUseCount + 1;
            reasonCode = string.Empty;
            return true;
        }

        private static int Get<TKey>(Dictionary<TKey, int> values, TKey key)
        {
            return values.TryGetValue(key, out int value) ? value : 0;
        }

        private static bool Reject(string reason, out string reasonCode)
        {
            reasonCode = reason;
            return false;
        }
    }
}
