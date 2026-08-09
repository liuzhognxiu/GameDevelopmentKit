namespace Game.Hot.Buqi.Battle
{
    public static class BuqiLinkConditionMatcher
    {
        public static bool Matches(BuqiLinkItem item, BuqiLinkCondition condition)
        {
            if (item == null)
                return false;
            if (condition == null)
                return true;
            if (!string.IsNullOrEmpty(condition.DefinitionId) && item.DefinitionId != condition.DefinitionId)
                return false;
            if (!string.IsNullOrEmpty(condition.AnnotationId) && item.AnnotationId != condition.AnnotationId)
                return false;
            if (!string.IsNullOrEmpty(condition.RequiredTag) && !item.Tags.Contains(condition.RequiredTag))
                return false;
            if (condition.AnyTags.Count > 0 && !Overlaps(item.Tags, condition.AnyTags))
                return false;
            if (condition.RequiredEffect.HasValue && !item.Effects.Contains(condition.RequiredEffect.Value))
                return false;
            if (condition.AnyEffects.Count > 0 && !Overlaps(item.Effects, condition.AnyEffects))
                return false;
            if (condition.RequiredTrigger.HasValue && !item.Triggers.Contains(condition.RequiredTrigger.Value))
                return false;
            if (condition.RequiredCondition.HasValue && !item.Conditions.Contains(condition.RequiredCondition.Value))
                return false;
            if (condition.MinimumQuality > 0 && item.Quality < condition.MinimumQuality)
                return false;
            if (condition.MaximumQuality > 0 && item.Quality > condition.MaximumQuality)
                return false;
            if (condition.MinimumSize > 0 && item.Size < condition.MinimumSize)
                return false;
            if (condition.MaximumSize > 0 && item.Size > condition.MaximumSize)
                return false;
            return true;
        }

        private static bool Overlaps<T>(System.Collections.Generic.HashSet<T> left, System.Collections.Generic.HashSet<T> right)
        {
            foreach (T value in right)
            {
                if (left.Contains(value))
                    return true;
            }
            return false;
        }
    }
}
