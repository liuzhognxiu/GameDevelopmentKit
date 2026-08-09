using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public static class BuqiFormationCatalog
    {
        public static IReadOnlyList<BuqiFormationRule> CreateDefault()
        {
            return new[]
            {
                Formation("core.attack.tempo", 300,
                    Items("damage", 1, Effect(BuqiEffect.Damage)),
                    Links("tempo-to-damage", 1, Effects(BuqiEffect.Haste, BuqiEffect.Charge), Effect(BuqiEffect.Damage))),
                Formation("core.attack.chain", 290,
                    Items("damage-outlets", 2, Effect(BuqiEffect.Damage)),
                    Items("adjacent-listener", 1, Trigger(BuqiTrigger.OnAdjacentUse))),

                Formation("core.shield.counter", 280,
                    Items("buffer-source", 1, Effect(BuqiEffect.Buffer)),
                    Items("buffer-loss-counter", 1, Condition(BuqiConditionKind.BufferLost))),
                Formation("core.shield.sustain", 270,
                    Items("buffer-sources", 2, Effect(BuqiEffect.Buffer)),
                    Items("charge-source", 1, Effect(BuqiEffect.Charge))),

                Formation("core.recovery.regen", 260,
                    Items("direct-heal", 1, Effect(BuqiEffect.Heal)),
                    Items("regeneration", 1, Effect(BuqiEffect.Regen))),
                Formation("core.recovery.tempo", 250,
                    Items("recovery-sources", 2, Effects(BuqiEffect.Heal, BuqiEffect.Regen)),
                    Links("haste-to-recovery", 1, Effect(BuqiEffect.Haste), Effects(BuqiEffect.Heal, BuqiEffect.Regen))),

                Formation("bridge.attack.overload", 200,
                    Items("damage", 1, Effect(BuqiEffect.Damage)),
                    Items("noise", 1, Effect(BuqiEffect.Noise)),
                    Items("charge", 1, Effect(BuqiEffect.Charge))),
                Formation("bridge.shield.recovery", 190,
                    Items("buffer", 1, Effect(BuqiEffect.Buffer)),
                    Items("recovery", 1, Effects(BuqiEffect.Heal, BuqiEffect.Regen))),
                Formation("bridge.poison.freeze", 180,
                    Items("poison", 1, Effect(BuqiEffect.Poison)),
                    Items("freeze", 1, Effect(BuqiEffect.Freeze))),
            };
        }

        private static BuqiFormationRule Formation(
            string id,
            int priority,
            params BuqiFormationRequirement[] requirements)
        {
            return new BuqiFormationRule
            {
                FormationId = id,
                Priority = priority,
                Requirements = new List<BuqiFormationRequirement>(requirements),
            };
        }

        private static BuqiFormationRequirement Items(string id, int count, BuqiLinkCondition condition)
        {
            return BuqiFormationRequirement.Items(id, count, condition);
        }

        private static BuqiFormationRequirement Links(
            string id,
            int count,
            BuqiLinkCondition source,
            BuqiLinkCondition target)
        {
            return BuqiFormationRequirement.Links(id, count, source, target);
        }

        private static BuqiLinkCondition Effect(BuqiEffect effect)
        {
            return new BuqiLinkCondition { RequiredEffect = effect };
        }

        private static BuqiLinkCondition Effects(params BuqiEffect[] effects)
        {
            return new BuqiLinkCondition { AnyEffects = new HashSet<BuqiEffect>(effects) };
        }

        private static BuqiLinkCondition Trigger(BuqiTrigger trigger)
        {
            return new BuqiLinkCondition { RequiredTrigger = trigger };
        }

        private static BuqiLinkCondition Condition(BuqiConditionKind condition)
        {
            return new BuqiLinkCondition { RequiredCondition = condition };
        }
    }
}
