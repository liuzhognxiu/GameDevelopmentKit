using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Training;

namespace Game.Hot.Buqi.Run.Integration
{
    public sealed class BuqiRunAuthoredOperationCatalog :
        IBuqiRunEventDefinitionCatalog,
        IBuqiRunEventItemCatalog,
        IBuqiRunTrainingDefinitionCatalog
    {
        private readonly Dictionary<string, BuqiRunItemDefinition> m_Items =
            new Dictionary<string, BuqiRunItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> m_ItemTags =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuqiRunEventDefinition> m_Events =
            new Dictionary<string, BuqiRunEventDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuqiRunTrainingDefinition> m_Training =
            new Dictionary<string, BuqiRunTrainingDefinition>(StringComparer.Ordinal);

        public BuqiRunAuthoredOperationCatalog(BuqiConfigCatalog source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            BuildItems(source.Items);
            BuildEvents(source.Events, source.EventOptions);
            BuildTraining(source.TrainingProjects);
        }

        public IReadOnlyList<BuqiRunEventDefinition> Definitions => m_Events.Values.OrderBy(value => value.EventId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> DefinitionIds => m_Items.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<BuqiRunTrainingDefinition> TrainingDefinitions => m_Training.Values.OrderBy(value => value.TrainingId, StringComparer.Ordinal).ToArray();

        public bool TryGet(string eventId, out BuqiRunEventDefinition definition)
        {
            return m_Events.TryGetValue(eventId, out definition);
        }

        public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
        {
            if (m_Items.TryGetValue(definitionId, out BuqiRunItemDefinition found))
            {
                definition = new BuqiRunItemDefinition
                {
                    DefinitionId = found.DefinitionId,
                    Size = found.Size,
                    BuyPrice = found.BuyPrice,
                    SellPrice = found.SellPrice,
                    UpgradePrice = found.UpgradePrice,
                    RefinementPrice = found.RefinementPrice,
                };
                return true;
            }

            definition = null;
            return false;
        }

        public bool TryGet(string trainingId, out BuqiRunTrainingDefinition definition)
        {
            return m_Training.TryGetValue(trainingId, out definition);
        }

        public bool HasBuildTag(string definitionId, string buildTag)
        {
            return !string.IsNullOrWhiteSpace(definitionId) &&
                   !string.IsNullOrWhiteSpace(buildTag) &&
                   m_ItemTags.TryGetValue(definitionId, out HashSet<string> tags) &&
                   tags.Contains(buildTag);
        }

        private void BuildItems(IReadOnlyList<BuqiItemConfigRow> rows)
        {
            if (rows == null)
                return;
            for (int index = 0; index < rows.Count; index++)
            {
                BuqiItemConfigRow row = rows[index];
                if (row == null || string.IsNullOrWhiteSpace(row.DefinitionId) || m_Items.ContainsKey(row.DefinitionId))
                    continue;

                int price = Math.Max(0, row.BasePrice);
                m_Items.Add(row.DefinitionId, new BuqiRunItemDefinition
                {
                    DefinitionId = row.DefinitionId,
                    Size = (int)row.Size,
                    BuyPrice = price,
                    SellPrice = Math.Max(1, price / 2),
                    UpgradePrice = Math.Max(1, row.ImprovedUpgradeCost),
                    RefinementPrice = Math.Max(1, row.RefinementCost),
                });
                var tags = new HashSet<string>(StringComparer.Ordinal);
                AddTag(tags, row.ArchetypeId);
                AddTag(tags, row.Role);
                if (row.Tags != null)
                {
                    for (int tagIndex = 0; tagIndex < row.Tags.Count; tagIndex++)
                        AddTag(tags, row.Tags[tagIndex]);
                }
                m_ItemTags.Add(row.DefinitionId, tags);
            }
        }

        private void BuildEvents(
            IReadOnlyList<BuqiEventConfigRow> events,
            IReadOnlyList<BuqiEventOptionConfigRow> options)
        {
            if (events == null)
                return;
            for (int index = 0; index < events.Count; index++)
            {
                BuqiEventConfigRow row = events[index];
                if (row == null || string.IsNullOrWhiteSpace(row.EventId) || m_Events.ContainsKey(row.EventId))
                    continue;
                var definition = new BuqiRunEventDefinition
                {
                    EventId = row.EventId,
                    BaseWeight = Math.Max(1, row.Weight),
                    UniquePerRun = row.DayNineResolution,
                    CooldownDays = Math.Max(0, row.RevisitDelayDays),
                    Eligibility = Eligibility(row.MinDay, row.MaxDay, row.RequiredFlags, row.ForbiddenFlags),
                };

                List<BuqiEventOptionConfigRow> authoredOptions = (options ?? Array.Empty<BuqiEventOptionConfigRow>())
                    .Where(value => value != null && string.Equals(value.EventId, row.EventId, StringComparison.Ordinal))
                    .OrderBy(value => value.Order)
                    .ThenBy(value => value.OptionId, StringComparer.Ordinal)
                    .ToList();
                for (int optionIndex = 0; optionIndex < authoredOptions.Count; optionIndex++)
                {
                    BuqiRunEventOptionDefinition converted = ConvertOption(authoredOptions[optionIndex]);
                    if (converted == null)
                    {
                        definition.Options.Clear();
                        break;
                    }
                    definition.Options.Add(converted);
                }
                if (definition.Options.Count == 3)
                    m_Events.Add(definition.EventId, definition);
            }
        }

        private void BuildTraining(IReadOnlyList<BuqiTrainingProjectConfigRow> rows)
        {
            if (rows == null)
                return;
            for (int index = 0; index < rows.Count; index++)
            {
                BuqiTrainingProjectConfigRow row = rows[index];
                if (row == null || string.IsNullOrWhiteSpace(row.ProjectId) || m_Training.ContainsKey(row.ProjectId))
                    continue;
                BuqiRunTrainingDefinition definition = ConvertTraining(row);
                if (definition != null)
                    m_Training.Add(definition.TrainingId, definition);
            }
        }

        private BuqiRunEventOptionDefinition ConvertOption(BuqiEventOptionConfigRow row)
        {
            var option = new BuqiRunEventOptionDefinition
            {
                OptionId = row.OptionId,
                Eligibility = OptionEligibility(row),
            };
            if (row.Costs != null)
            {
                for (int index = 0; index < row.Costs.Count; index++)
                {
                    BuqiEventCostConfigRow cost = row.Costs[index];
                    if (cost != null && IsKind(cost.Kind, BuqiEventCostKind.Coins))
                        option.CoinCost = Math.Max(option.CoinCost, Math.Max(0, cost.Amount));
                }
            }
            if (row.Outcomes != null)
            {
                for (int index = 0; index < row.Outcomes.Count; index++)
                {
                    BuqiEventOutcomeConfigRow outcome = row.Outcomes[index];
                    BuqiRunEventActionDefinition action = ConvertOutcome(row.OptionId, outcome, row.ConditionValue);
                    if (action == null)
                        return null;
                    option.Actions.Add(action);
                }
            }
            AddFlagActions(option, row.SetFlags, BuqiRunEventActionKind.SetFlag);
            AddFlagActions(option, row.ClearFlags, BuqiRunEventActionKind.ClearFlag);
            if (!string.IsNullOrWhiteSpace(row.FollowUpEventId))
            {
                option.Actions.Add(new BuqiRunEventActionDefinition
                {
                    ActionId = row.OptionId + ".follow-up",
                    Kind = BuqiRunEventActionKind.ScheduleReturn,
                    ReturnEventId = row.FollowUpEventId,
                    ScheduleId = row.OptionId + ".schedule",
                    MinDayOffset = Math.Max(1, row.FollowUpDelayDays),
                    MaxDayOffset = Math.Max(1, row.FollowUpDelayDays),
                });
            }
            return option;
        }

        private BuqiRunEventActionDefinition ConvertOutcome(
            string optionId,
            BuqiEventOutcomeConfigRow row,
            string defaultBuildTag)
        {
            if (row == null)
                return null;
            string actionId = optionId + "." + row.ReasonCode;
            if (IsKind(row.Kind, BuqiEventOutcomeKind.Coins))
                return Action(actionId, BuqiRunEventActionKind.GrantCoins, row.Amount);
            if (IsKind(row.Kind, BuqiEventOutcomeKind.Life))
                return Action(actionId, BuqiRunEventActionKind.RestoreLife, row.Amount);
            if (IsKind(row.Kind, BuqiEventOutcomeKind.GrantItemTag))
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = BuqiRunEventActionKind.GrantRandomItem,
                    BuildTag = row.Value,
                };
            if (IsKind(row.Kind, BuqiEventOutcomeKind.GrantRefinement))
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = BuqiRunEventActionKind.ApplyRefinement,
                    BuildTag = defaultBuildTag,
                    RefinementId = row.Value,
                };
            if (IsKind(row.Kind, BuqiEventOutcomeKind.UpgradeItemTag))
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = BuqiRunEventActionKind.UpgradeItem,
                    BuildTag = row.Value,
                    QualitySteps = Math.Max(1, row.Amount),
                };
            if (IsKind(row.Kind, BuqiEventOutcomeKind.TemporaryHaste) ||
                IsKind(row.Kind, BuqiEventOutcomeKind.TemporaryBuffer) ||
                IsKind(row.Kind, BuqiEventOutcomeKind.TemporaryHealing))
            {
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = BuqiRunEventActionKind.AddTemporaryModifier,
                    BuildTag = defaultBuildTag,
                    ModifierId = row.ReasonCode,
                    ModifierKind = IsKind(row.Kind, BuqiEventOutcomeKind.TemporaryBuffer)
                        ? BuqiRunModifierKind.StartingShield
                        : IsKind(row.Kind, BuqiEventOutcomeKind.TemporaryHealing)
                            ? BuqiRunModifierKind.RecoveryPercent
                            : BuqiRunModifierKind.CooldownPercent,
                    Amount = row.Amount,
                    DurationBattles = Math.Max(1, row.DurationDays),
                };
            }
            if (IsKind(row.Kind, BuqiEventOutcomeKind.SetFlag) || IsKind(row.Kind, BuqiEventOutcomeKind.ClearFlag))
            {
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = IsKind(row.Kind, BuqiEventOutcomeKind.SetFlag)
                        ? BuqiRunEventActionKind.SetFlag
                        : BuqiRunEventActionKind.ClearFlag,
                    FlagId = row.Value,
                };
            }
            if (IsKind(row.Kind, BuqiEventOutcomeKind.RemoveItemTag))
            {
                return new BuqiRunEventActionDefinition
                {
                    ActionId = actionId,
                    Kind = BuqiRunEventActionKind.SacrificeItem,
                    BuildTag = row.Value,
                };
            }
            return null;
        }

        private static BuqiRunTrainingDefinition ConvertTraining(BuqiTrainingProjectConfigRow row)
        {
            if (!TryGetSupportedTrainingModifier(row.EffectKind, out BuqiRunModifierKind modifier))
                return null;
            return new BuqiRunTrainingDefinition
            {
                TrainingId = row.ProjectId,
                Kind = BuqiRunTrainingKind.DirectedStrengthening,
                Eligibility = Eligibility(row.MinDay, row.MaxDay, Array.Empty<string>(), Array.Empty<string>(), row.RequiredTag),
                CoinCost = Math.Max(0, row.Cost),
                RequiredBuildTag = row.RequiredTag,
                ModifierId = row.ProjectId,
                ModifierKind = modifier,
                ModifierValue = NormalizeModifierValue(modifier, row.Amount),
                ModifierDurationBattles = 1,
                ModifierDurationTicks = Math.Max(0, row.Duration),
                MaxPerRun = Math.Max(0, row.MaxPerRun),
            };
        }

        private static BuqiRunEventActionDefinition Action(string id, BuqiRunEventActionKind kind, int amount)
        {
            return new BuqiRunEventActionDefinition { ActionId = id, Kind = kind, Amount = amount };
        }

        private static void AddFlagActions(BuqiRunEventOptionDefinition option, IReadOnlyList<string> values, BuqiRunEventActionKind kind)
        {
            if (values == null)
                return;
            for (int index = 0; index < values.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                    option.Actions.Add(new BuqiRunEventActionDefinition
                    {
                        ActionId = option.OptionId + "." + kind + "." + index,
                        Kind = kind,
                        FlagId = values[index],
                    });
            }
        }

        private static BuqiRunEventEligibility OptionEligibility(BuqiEventOptionConfigRow row)
        {
            var eligibility = new BuqiRunEventEligibility
            {
                RequiredFlags = row.RequiredFlags == null ? new List<string>() : new List<string>(row.RequiredFlags),
                ForbiddenFlags = row.ForbiddenFlags == null ? new List<string>() : new List<string>(row.ForbiddenFlags),
            };
            if (IsKind(row.ConditionKind, BuqiEventConditionKind.HasItemTag) ||
                IsKind(row.ConditionKind, BuqiEventConditionKind.HasBuild))
            {
                AddTag(eligibility.RequiredBuildTags, row.ConditionValue);
            }
            else if (IsKind(row.ConditionKind, BuqiEventConditionKind.HasFlag))
            {
                AddTag(eligibility.RequiredFlags, row.ConditionValue);
            }
            else if (IsKind(row.ConditionKind, BuqiEventConditionKind.MissingFlag))
            {
                AddTag(eligibility.ForbiddenFlags, row.ConditionValue);
            }
            return eligibility;
        }

        private static BuqiRunEventEligibility Eligibility(
            int minDay,
            int maxDay,
            IReadOnlyList<string> requiredFlags,
            IReadOnlyList<string> forbiddenFlags,
            string requiredBuildTag = "")
        {
            var result = new BuqiRunEventEligibility
            {
                MinDay = Math.Max(1, minDay),
                MaxDay = Math.Max(Math.Max(1, minDay), maxDay),
                RequiredFlags = requiredFlags == null ? new List<string>() : new List<string>(requiredFlags),
                ForbiddenFlags = forbiddenFlags == null ? new List<string>() : new List<string>(forbiddenFlags),
            };
            AddTag(result.RequiredBuildTags, requiredBuildTag);
            return result;
        }

        private static bool TryGetSupportedTrainingModifier(string value, out BuqiRunModifierKind modifier)
        {
            if (IsKind(value, BuqiTrainingEffectKind.OpeningBuffer))
            {
                modifier = BuqiRunModifierKind.StartingShield;
                return true;
            }
            if (IsKind(value, BuqiTrainingEffectKind.OpeningHeal))
            {
                modifier = BuqiRunModifierKind.RecoveryPercent;
                return true;
            }
            if (IsKind(value, BuqiTrainingEffectKind.CooldownPercent))
            {
                modifier = BuqiRunModifierKind.CooldownPercent;
                return true;
            }
            if (IsKind(value, BuqiTrainingEffectKind.OpeningHaste))
            {
                modifier = BuqiRunModifierKind.CooldownPercent;
                return true;
            }
            modifier = default;
            return false;
        }

        private static int NormalizeModifierValue(BuqiRunModifierKind kind, int value)
        {
            int normalized = Math.Abs(value);
            if (kind == BuqiRunModifierKind.CooldownPercent && normalized > 0 && normalized <= 100)
                normalized *= 100;
            return normalized;
        }

        private static bool IsKind<T>(string value, T expected) where T : struct, Enum
        {
            if (int.TryParse(value, out int numeric))
                return numeric == Convert.ToInt32(expected);
            return Enum.TryParse(value, true, out T parsed) && EqualityComparer<T>.Default.Equals(parsed, expected);
        }

        private static void AddTag(ICollection<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
                values.Add(value);
        }
    }
}
