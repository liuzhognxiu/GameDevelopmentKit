using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public static class BuqiLinkEngine
    {
        public static BuqiLinkEvaluation Evaluate(
            BuqiLinkBoard board,
            IReadOnlyList<BuqiLinkRule> linkRules,
            IReadOnlyList<BuqiFormationRule> formationRules,
            BuqiEchoBlueprint echo)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            ValidateRules(linkRules);
            ValidateFormations(formationRules);
            List<BuqiLinkFact> links = EvaluateLinks(board, linkRules ?? Array.Empty<BuqiLinkRule>());
            return new BuqiLinkEvaluation
            {
                Links = links,
                Formations = EvaluateFormations(board, links, formationRules ?? Array.Empty<BuqiFormationRule>()),
                Echo = EvaluateEcho(board, echo),
            };
        }

        public static IReadOnlyList<BuqiLinkTriggerFact> ResolveTriggers(
            IReadOnlyList<BuqiLinkFact> links,
            IReadOnlyList<BuqiLinkRule> rules,
            BuqiLinkTriggerContext context,
            BuqiLinkExecutionGuard guard)
        {
            if (links == null)
                throw new ArgumentNullException(nameof(links));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (guard == null)
                throw new ArgumentNullException(nameof(guard));
            ValidateRules(rules);

            var rulesById = new Dictionary<string, BuqiLinkRule>(StringComparer.Ordinal);
            if (rules != null)
            {
                foreach (BuqiLinkRule rule in rules)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.RuleId))
                        rulesById[rule.RuleId] = rule;
                }
            }

            var facts = new List<BuqiLinkTriggerFact>();
            foreach (BuqiLinkFact link in links)
            {
                if (!link.IsConnected || !rulesById.TryGetValue(link.RuleId, out BuqiLinkRule rule))
                    continue;
                var fact = new BuqiLinkTriggerFact
                {
                    RuleId = rule.RuleId,
                    SourceInstanceId = link.SourceInstanceId,
                    TargetInstanceId = link.TargetInstanceId,
                    TriggeredByInstanceId = context.TriggeredByInstanceId,
                    RootEventId = context.RootEventId,
                    Priority = rule.Priority,
                    Amount = rule.Amount,
                    Rule = rule,
                    Link = link,
                };
                if (!MatchesTriggerSource(link, rule, context))
                {
                    fact.ReasonCode = "TriggerSourceMismatch";
                    facts.Add(fact);
                    continue;
                }
                fact.IsTriggered = true;
                facts.Add(fact);
            }

            facts.Sort(CompareTriggerFacts);
            ApplyExclusiveGroups(facts);
            ApplyStacking(facts);
            foreach (BuqiLinkTriggerFact fact in facts)
            {
                if (!fact.IsTriggered)
                    continue;
                var attempt = new BuqiLinkTriggerAttempt
                {
                    Tick = context.Tick,
                    ChainDepth = context.ChainDepth,
                    RootEventId = context.RootEventId,
                    ActiveUseId = context.ActiveUseId,
                    RuleId = fact.RuleId,
                    SourceInstanceId = fact.SourceInstanceId,
                    TargetInstanceId = fact.TargetInstanceId,
                    StateHash = context.StateHash,
                    RuleMaxTriggersPerTick = fact.Rule.MaxTriggersPerTick,
                    RuleMaxTriggersPerActiveUse = fact.Rule.MaxTriggersPerActiveUse,
                };
                if (!guard.TryEnter(attempt, out string reason))
                {
                    fact.IsTriggered = false;
                    fact.ReasonCode = reason;
                }
            }
            return facts;
        }

        private static List<BuqiLinkFact> EvaluateLinks(BuqiLinkBoard board, IReadOnlyList<BuqiLinkRule> rules)
        {
            var sortedRules = new List<BuqiLinkRule>();
            foreach (BuqiLinkRule rule in rules)
            {
                if (rule != null)
                    sortedRules.Add(rule);
            }
            sortedRules.Sort(CompareRules);

            var result = new List<BuqiLinkFact>();
            foreach (BuqiLinkRule rule in sortedRules)
            {
                bool foundSource = false;
                foreach (BuqiLinkItem source in board.Items)
                {
                    if (!BuqiLinkConditionMatcher.Matches(source, rule.SourceCondition))
                        continue;
                    foundSource = true;
                    if (rule.Direction == BuqiLinkDirection.AnyAdjacent)
                    {
                        AddLinkFact(result, board, source, rule, BuqiLinkDirection.Clockwise);
                        AddLinkFact(result, board, source, rule, BuqiLinkDirection.CounterClockwise);
                    }
                    else
                    {
                        AddLinkFact(result, board, source, rule, rule.Direction);
                    }
                }
                if (!foundSource)
                {
                    result.Add(new BuqiLinkFact
                    {
                        RuleId = rule.RuleId,
                        Direction = rule.Direction,
                        ReasonCode = "SourceConditionNotMet",
                    });
                }
            }
            result.Sort(CompareLinkFacts);
            return result;
        }

        private static void AddLinkFact(
            List<BuqiLinkFact> result,
            BuqiLinkBoard board,
            BuqiLinkItem source,
            BuqiLinkRule rule,
            BuqiLinkDirection direction)
        {
            BuqiLinkItem target = BuqiLinkTopology.GetAdjacent(board, source, direction);
            var fact = new BuqiLinkFact
            {
                RuleId = rule.RuleId,
                SourceInstanceId = source.InstanceId,
                SourceAnchorSlot = source.AnchorSlot,
                TargetInstanceId = target?.InstanceId ?? string.Empty,
                TargetAnchorSlot = target?.AnchorSlot ?? int.MaxValue,
                Direction = direction,
                Source = source,
                Target = target,
            };
            if (target == null)
                fact.ReasonCode = "NoAdjacentItem";
            else if (!BuqiLinkConditionMatcher.Matches(target, rule.TargetCondition))
                fact.ReasonCode = "TargetConditionNotMet";
            else
                fact.IsConnected = true;
            result.Add(fact);
        }

        private static List<BuqiFormationFact> EvaluateFormations(
            BuqiLinkBoard board,
            IReadOnlyList<BuqiLinkFact> links,
            IReadOnlyList<BuqiFormationRule> rules)
        {
            var result = new List<BuqiFormationFact>();
            foreach (BuqiFormationRule rule in rules)
            {
                if (rule == null)
                    continue;
                var fact = new BuqiFormationFact
                {
                    FormationId = rule.FormationId,
                    Priority = rule.Priority,
                };
                foreach (BuqiFormationRequirement requirement in rule.Requirements)
                {
                    int count = requirement.Kind == BuqiFormationRequirementKind.MatchingItems
                        ? CountItems(board, requirement.ItemCondition)
                        : CountLinks(links, requirement.SourceCondition, requirement.TargetCondition);
                    if (count < requirement.MinimumCount)
                    {
                        fact.MissingRequirements.Add(
                            GameFramework.Utility.Text.Format(
                                "{0}:{1}/{2}",
                                requirement.RequirementId,
                                count,
                                requirement.MinimumCount));
                    }
                }
                fact.MissingRequirements.Sort(StringComparer.Ordinal);
                fact.IsFormed = fact.MissingRequirements.Count == 0;
                result.Add(fact);
            }
            result.Sort((left, right) =>
            {
                int comparison = right.Priority.CompareTo(left.Priority);
                return comparison != 0 ? comparison : string.CompareOrdinal(left.FormationId, right.FormationId);
            });
            return result;
        }

        private static BuqiEchoMatchFact EvaluateEcho(BuqiLinkBoard board, BuqiEchoBlueprint echo)
        {
            var fact = new BuqiEchoMatchFact();
            if (echo == null)
                return fact;
            fact.IsEvaluated = true;
            fact.EchoId = echo.EchoId;
            var matchedInstances = new HashSet<string>(StringComparer.Ordinal);
            var expectedAnchors = new HashSet<int>();
            foreach (BuqiEchoSlot expected in echo.Items)
            {
                if (!expectedAnchors.Add(expected.AnchorSlot))
                {
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "slot-{0}:DuplicateBlueprint",
                        expected.AnchorSlot));
                    continue;
                }
                if (expected.AnchorSlot < 0 || expected.AnchorSlot >= BuqiLinkBoard.SlotCount)
                {
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "slot-{0}:OutOfRange",
                        expected.AnchorSlot));
                    continue;
                }
                BuqiLinkItem actual = FindAtAnchor(board, expected.AnchorSlot);
                if (actual == null)
                {
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "slot-{0}:Missing",
                        expected.AnchorSlot));
                    continue;
                }
                matchedInstances.Add(actual.InstanceId);
                if (actual.DefinitionId != expected.DefinitionId)
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "{0}:DefinitionId",
                        actual.InstanceId));
                if (actual.Quality != expected.Quality)
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "{0}:Quality",
                        actual.InstanceId));
                if (actual.AnnotationId != expected.AnnotationId)
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "{0}:AnnotationId",
                        actual.InstanceId));
            }
            foreach (BuqiLinkItem actual in board.Items)
            {
                if (!matchedInstances.Contains(actual.InstanceId))
                    fact.Mismatches.Add(GameFramework.Utility.Text.Format(
                        "{0}:Unexpected",
                        actual.InstanceId));
            }
            fact.Mismatches.Sort(StringComparer.Ordinal);
            fact.IsExactMatch = fact.Mismatches.Count == 0;
            return fact;
        }

        private static int CountItems(BuqiLinkBoard board, BuqiLinkCondition condition)
        {
            int count = 0;
            foreach (BuqiLinkItem item in board.Items)
            {
                if (BuqiLinkConditionMatcher.Matches(item, condition))
                    count++;
            }
            return count;
        }

        private static int CountLinks(
            IReadOnlyList<BuqiLinkFact> links,
            BuqiLinkCondition source,
            BuqiLinkCondition target)
        {
            int count = 0;
            foreach (BuqiLinkFact link in links)
            {
                if (link.IsConnected &&
                    BuqiLinkConditionMatcher.Matches(link.Source, source) &&
                    BuqiLinkConditionMatcher.Matches(link.Target, target))
                {
                    count++;
                }
            }
            return count;
        }

        private static BuqiLinkItem FindAtAnchor(BuqiLinkBoard board, int anchor)
        {
            foreach (BuqiLinkItem item in board.Items)
            {
                if (item.AnchorSlot == anchor)
                    return item;
            }
            return null;
        }

        private static bool MatchesTriggerSource(
            BuqiLinkFact link,
            BuqiLinkRule rule,
            BuqiLinkTriggerContext context)
        {
            if (rule.TriggerSource != context.TriggerSource)
                return false;
            if (context.TriggerSource == BuqiLinkTriggerSource.BattleStart)
                return true;
            if (context.TriggerSource == BuqiLinkTriggerSource.AdjacentUse)
                return context.TriggeredByInstanceId == link.TargetInstanceId;
            return context.TriggeredByInstanceId == link.SourceInstanceId;
        }

        private static void ApplyExclusiveGroups(List<BuqiLinkTriggerFact> facts)
        {
            var winners = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiLinkTriggerFact fact in facts)
            {
                if (!fact.IsTriggered || string.IsNullOrEmpty(fact.Rule.ExclusiveGroup))
                    continue;
                string key = GameFramework.Utility.Text.Format(
                    "{0}|{1}",
                    fact.TargetInstanceId,
                    fact.Rule.ExclusiveGroup);
                if (winners.Add(key))
                    continue;
                fact.IsTriggered = false;
                fact.ReasonCode = "ExclusiveSuppressed";
            }
        }

        private static void ApplyStacking(List<BuqiLinkTriggerFact> facts)
        {
            var groups = new Dictionary<string, List<BuqiLinkTriggerFact>>(StringComparer.Ordinal);
            foreach (BuqiLinkTriggerFact fact in facts)
            {
                if (!fact.IsTriggered || string.IsNullOrEmpty(fact.Rule.StackGroup))
                    continue;
                string key = GameFramework.Utility.Text.Format(
                    "{0}|{1}",
                    fact.TargetInstanceId,
                    fact.Rule.StackGroup);
                if (!groups.TryGetValue(key, out List<BuqiLinkTriggerFact> group))
                {
                    group = new List<BuqiLinkTriggerFact>();
                    groups[key] = group;
                }
                group.Add(fact);
            }

            foreach (List<BuqiLinkTriggerFact> group in groups.Values)
            {
                BuqiLinkStackMode mode = group[0].Rule.StackMode;
                if (mode == BuqiLinkStackMode.Add)
                {
                    int limit = Math.Max(1, group[0].Rule.StackLimit);
                    for (int index = limit; index < group.Count; index++)
                        Suppress(group[index], "StackLimitReached");
                }
                else if (mode == BuqiLinkStackMode.Max)
                {
                    BuqiLinkTriggerFact winner = group[0];
                    foreach (BuqiLinkTriggerFact candidate in group)
                    {
                        if (candidate.Amount > winner.Amount ||
                            (candidate.Amount == winner.Amount && CompareTriggerFacts(candidate, winner) < 0))
                        {
                            winner = candidate;
                        }
                    }
                    foreach (BuqiLinkTriggerFact candidate in group)
                    {
                        if (candidate != winner)
                            Suppress(candidate, "MaxSuppressed");
                    }
                }
                else if (mode == BuqiLinkStackMode.ReplaceHigherPriority)
                {
                    for (int index = 1; index < group.Count; index++)
                        Suppress(group[index], "PrioritySuppressed");
                }
                else
                {
                    var sources = new HashSet<string>(StringComparer.Ordinal);
                    foreach (BuqiLinkTriggerFact candidate in group)
                    {
                        if (!sources.Add(candidate.SourceInstanceId))
                            Suppress(candidate, "DuplicateSourceSuppressed");
                    }
                }
            }
        }

        private static void Suppress(BuqiLinkTriggerFact fact, string reason)
        {
            fact.IsTriggered = false;
            fact.ReasonCode = reason;
        }

        private static int CompareRules(BuqiLinkRule left, BuqiLinkRule right)
        {
            int comparison = right.Priority.CompareTo(left.Priority);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.RuleId, right.RuleId);
        }

        private static void ValidateRules(IReadOnlyList<BuqiLinkRule> rules)
        {
            if (rules == null)
                return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var stackModes = new Dictionary<string, BuqiLinkStackMode>(StringComparer.Ordinal);
            foreach (BuqiLinkRule rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId) || !ids.Add(rule.RuleId))
                    throw new ArgumentException("Link rule ids must be non-empty and unique.", nameof(rules));
                if (rule.StackLimit < 1)
                    throw new ArgumentException("Link rule stack limits must be positive.", nameof(rules));
                if (!string.IsNullOrEmpty(rule.StackGroup))
                {
                    if (stackModes.TryGetValue(rule.StackGroup, out BuqiLinkStackMode mode) && mode != rule.StackMode)
                        throw new ArgumentException("Rules sharing a stack group must use one stack mode.", nameof(rules));
                    stackModes[rule.StackGroup] = rule.StackMode;
                }
            }
        }

        private static void ValidateFormations(IReadOnlyList<BuqiFormationRule> rules)
        {
            if (rules == null)
                return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiFormationRule rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.FormationId) || !ids.Add(rule.FormationId))
                    throw new ArgumentException("Formation ids must be non-empty and unique.", nameof(rules));
                var requirementIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (BuqiFormationRequirement requirement in rule.Requirements)
                {
                    if (requirement == null ||
                        string.IsNullOrWhiteSpace(requirement.RequirementId) ||
                        !requirementIds.Add(requirement.RequirementId) ||
                        requirement.MinimumCount < 1)
                    {
                        throw new ArgumentException(
                            "Formation requirements must have unique positive ids and counts.",
                            nameof(rules));
                    }
                }
            }
        }

        private static int CompareLinkFacts(BuqiLinkFact left, BuqiLinkFact right)
        {
            int comparison = left.SourceAnchorSlot.CompareTo(right.SourceAnchorSlot);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
            if (comparison != 0) return comparison;
            comparison = left.Direction.CompareTo(right.Direction);
            if (comparison != 0) return comparison;
            comparison = left.TargetAnchorSlot.CompareTo(right.TargetAnchorSlot);
            if (comparison != 0) return comparison;
            return string.CompareOrdinal(left.RuleId, right.RuleId);
        }

        private static int CompareTriggerFacts(BuqiLinkTriggerFact left, BuqiLinkTriggerFact right)
        {
            int comparison = right.Priority.CompareTo(left.Priority);
            if (comparison != 0) return comparison;
            comparison = left.Link.SourceAnchorSlot.CompareTo(right.Link.SourceAnchorSlot);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
            if (comparison != 0) return comparison;
            comparison = left.Link.TargetAnchorSlot.CompareTo(right.Link.TargetAnchorSlot);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(left.TargetInstanceId, right.TargetInstanceId);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(left.RuleId, right.RuleId);
            return comparison != 0 ? comparison : left.Link.Direction.CompareTo(right.Link.Direction);
        }
    }
}
