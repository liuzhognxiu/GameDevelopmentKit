using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public interface IBuqiRefinementRule
    {
        string RefinementId { get; }
        bool RewritesFirstActiveUse { get; }
        int OpeningNoise { get; }
        int OnUseNoise { get; }
        int AdjustBaseCooldownTicks(int baseCooldownTicks);
        int GetEffectMultiplierBps(BuqiEffect effect, bool isOpening);
        int AdjustNoiseAmount(int amount);
        bool AllowsModifier(BuqiEffect effect, bool fromEnemy);
    }

    public static class BuqiRefinementRuleCatalog
    {
        private static readonly IBuqiRefinementRule s_None = new BuiltInRefinementRule(string.Empty);
        private static readonly Dictionary<string, IBuqiRefinementRule> s_Rules =
            new Dictionary<string, IBuqiRefinementRule>(StringComparer.Ordinal)
            {
                ["A-01"] = new BuiltInRefinementRule("A-01", cooldownBps: 8500, onUseNoise: 1),
                ["A-02"] = new BuiltInRefinementRule("A-02", cooldownBps: 12000, nonOpeningEffectBps: 13000),
                ["A-03"] = new BuiltInRefinementRule("A-03", rewritesFirstActiveUse: true),
                ["A-04"] = new BuiltInRefinementRule("A-04", rejectsFriendlyHaste: true, rejectsEnemyDelay: true),
                ["A-05"] = new BuiltInRefinementRule("A-05", damageBufferBps: 8500, noiseAdjustment: -1),
                ["A-06"] = new BuiltInRefinementRule("A-06", damageBufferBps: 13500, openingNoise: 3),
            };

        public static bool TryGet(string refinementId, out IBuqiRefinementRule rule)
        {
            return s_Rules.TryGetValue(refinementId ?? string.Empty, out rule);
        }

        public static IBuqiRefinementRule GetOrDefault(string refinementId)
        {
            return TryGet(refinementId, out IBuqiRefinementRule rule) ? rule : s_None;
        }
    }

    internal sealed class BuiltInRefinementRule : IBuqiRefinementRule
    {
        private readonly int m_CooldownBps;
        private readonly int m_NonOpeningEffectBps;
        private readonly int m_DamageBufferBps;
        private readonly int m_NoiseAdjustment;
        private readonly bool m_RejectsFriendlyHaste;
        private readonly bool m_RejectsEnemyDelay;

        public BuiltInRefinementRule(
            string refinementId,
            int cooldownBps = 10000,
            int nonOpeningEffectBps = 10000,
            int damageBufferBps = 10000,
            int noiseAdjustment = 0,
            bool rewritesFirstActiveUse = false,
            bool rejectsFriendlyHaste = false,
            bool rejectsEnemyDelay = false,
            int openingNoise = 0,
            int onUseNoise = 0)
        {
            RefinementId = refinementId;
            m_CooldownBps = cooldownBps;
            m_NonOpeningEffectBps = nonOpeningEffectBps;
            m_DamageBufferBps = damageBufferBps;
            m_NoiseAdjustment = noiseAdjustment;
            RewritesFirstActiveUse = rewritesFirstActiveUse;
            m_RejectsFriendlyHaste = rejectsFriendlyHaste;
            m_RejectsEnemyDelay = rejectsEnemyDelay;
            OpeningNoise = openingNoise;
            OnUseNoise = onUseNoise;
        }

        public string RefinementId { get; }
        public bool RewritesFirstActiveUse { get; }
        public int OpeningNoise { get; }
        public int OnUseNoise { get; }

        public int AdjustBaseCooldownTicks(int baseCooldownTicks)
        {
            return RoundBps(baseCooldownTicks, m_CooldownBps);
        }

        public int GetEffectMultiplierBps(BuqiEffect effect, bool isOpening)
        {
            int result = 10000;
            if (!isOpening)
                result = RoundBps(result, m_NonOpeningEffectBps);
            if (effect == BuqiEffect.Damage || effect == BuqiEffect.Buffer)
                result = RoundBps(result, m_DamageBufferBps);
            return result;
        }

        public int AdjustNoiseAmount(int amount)
        {
            long adjusted = (long)amount + m_NoiseAdjustment;
            return adjusted <= 0
                ? 0
                : adjusted >= int.MaxValue ? int.MaxValue : (int)adjusted;
        }

        public bool AllowsModifier(BuqiEffect effect, bool fromEnemy)
        {
            if (effect == BuqiEffect.Haste && !fromEnemy && m_RejectsFriendlyHaste)
                return false;
            if (effect == BuqiEffect.Delay && fromEnemy && m_RejectsEnemyDelay)
                return false;
            return true;
        }

        private static int RoundBps(int value, int bps)
        {
            long numerator = (long)value * bps;
            long rounded = numerator >= 0
                ? (numerator + 5000) / 10000
                : (numerator - 5000) / 10000;
            if (rounded >= int.MaxValue)
                return int.MaxValue;
            if (rounded <= int.MinValue)
                return int.MinValue;
            return (int)rounded;
        }
    }
}
