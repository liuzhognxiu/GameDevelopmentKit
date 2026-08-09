using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Config
{
    /// <summary>
    /// Validates the authored Demo content contract that sits above battle-rule validation.
    /// </summary>
    public static class BuqiContentConfigValidator
    {
        private static readonly string[] s_MainBuilds = { "fast", "buffer", "heal" };
        private static readonly string[] s_MainRoles =
        {
            "starter", "core", "amplifier", "finisher",
            "bridge", "pivot", "counter", "economy",
        };

        private static readonly string[] s_MerchantSlotKinds =
        {
            "Archetype", "Size", "Quality", "Stage", "Bridge", "Counter", "Economy",
        };

        public static void Validate(
            BuqiConfigCatalog catalog,
            Dictionary<string, BuqiItemConfigRow> items,
            List<string> errors)
        {
            ValidateItems(catalog.Items, items, errors);
            ValidateMerchants(catalog.Merchants, items, errors);
            ValidateTraining(catalog.Trainers, catalog.TrainingProjects, errors);
            ValidateEvents(catalog.Events, catalog.EventOptions, errors);
        }

        private static void ValidateItems(
            List<BuqiItemConfigRow> rows,
            Dictionary<string, BuqiItemConfigRow> items,
            List<string> errors)
        {
            var rolesByBuild = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string build in s_MainBuilds)
                rolesByBuild.Add(build, new HashSet<string>(StringComparer.Ordinal));

            var unlockCounts = new Dictionary<int, int> { { 1, 0 }, { 4, 0 }, { 7, 0 } };
            foreach (BuqiItemConfigRow row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.DefinitionId))
                    continue;

                string where = GameFramework.Utility.Text.Format("Item {0}", row.DefinitionId);
                if (string.IsNullOrEmpty(row.DisplayName) || string.IsNullOrEmpty(row.LocalizationKey))
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define display and localization names", where));
                if (string.IsNullOrEmpty(row.Role))
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define a build role", where));
                if (string.IsNullOrEmpty(row.PositionHint))
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define a position hint", where));
                if (string.IsNullOrEmpty(row.UpgradeSummary) || string.IsNullOrEmpty(row.UpgradeLocalizationKey))
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define quality upgrade changes", where));
                if (!unlockCounts.ContainsKey(row.UnlockDay))
                    errors.Add(GameFramework.Utility.Text.Format("{0} unlock day must be 1, 4, or 7", where));
                else
                    unlockCounts[row.UnlockDay]++;

                int expectedBase = row.Size == Battle.BuqiSize.L ? 6 : row.Size == Battle.BuqiSize.M ? 4 : 2;
                int expectedFixed = row.Size == Battle.BuqiSize.L ? 9 : row.Size == Battle.BuqiSize.M ? 6 : 3;
                if (row.BasePrice != expectedBase ||
                    row.ImprovedUpgradeCost != expectedBase ||
                    row.FixedUpgradeCost != expectedFixed ||
                    row.RefinementCost != expectedBase)
                {
                    errors.Add(GameFramework.Utility.Text.Format("{0} violates the S/M/L price and upgrade budget", where));
                }

                if (row.LinkIds == null || row.LinkIds.Count < 2)
                    errors.Add(GameFramework.Utility.Text.Format("{0} must link to at least two items", where));
                else
                {
                    var uniqueLinks = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string linkId in row.LinkIds)
                    {
                        if (!uniqueLinks.Add(linkId))
                            errors.Add(GameFramework.Utility.Text.Format("{0} contains duplicate link {1}", where, linkId));
                        if (linkId == row.DefinitionId)
                            errors.Add(GameFramework.Utility.Text.Format("{0} cannot link to itself", where));
                        if (!items.ContainsKey(linkId))
                            errors.Add(GameFramework.Utility.Text.Format("{0} links to unknown item {1}", where, linkId));
                    }
                }

                if (row.Role == "economy")
                {
                    if (row.RunEffects == null || row.RunEffects.Count != 1)
                        errors.Add(GameFramework.Utility.Text.Format("{0} economy role must define exactly one run effect", where));
                }
                if (row.RunEffects != null)
                {
                    foreach (BuqiRunEffectConfigRow runEffect in row.RunEffects)
                    {
                        if (runEffect == null || runEffect.Amount <= 0 || runEffect.MaxPerDay != 1 ||
                            string.IsNullOrEmpty(runEffect.Trigger) || string.IsNullOrEmpty(runEffect.Effect) ||
                            string.IsNullOrEmpty(runEffect.ReasonCode))
                        {
                            errors.Add(GameFramework.Utility.Text.Format("{0} has an invalid capped run effect", where));
                        }
                    }
                }

                if (rolesByBuild.TryGetValue(row.ArchetypeId, out HashSet<string> roles))
                    roles.Add(row.Role);
            }

            if (unlockCounts[1] != 17 || unlockCounts[4] != 15 || unlockCounts[7] != 10)
                errors.Add("Item unlock bands must contain 17/15/10 items for days 1/4/7");

            foreach (string build in s_MainBuilds)
            {
                HashSet<string> roles = rolesByBuild[build];
                foreach (string role in s_MainRoles)
                {
                    if (!roles.Contains(role))
                        errors.Add(GameFramework.Utility.Text.Format("Main build {0} is missing role {1}", build, role));
                }
            }
        }

        private static void ValidateMerchants(
            List<BuqiMerchantConfigRow> rows,
            Dictionary<string, BuqiItemConfigRow> items,
            List<string> errors)
        {
            if (rows == null || rows.Count != 8)
            {
                errors.Add("Merchant table must contain exactly 8 merchants");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var coveredSlotKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiMerchantConfigRow row in rows)
            {
                if (row == null || !ids.Add(row.MerchantId))
                {
                    errors.Add("Merchant ids must be non-empty and unique");
                    continue;
                }
                string where = GameFramework.Utility.Text.Format("Merchant {0}", row.MerchantId);
                ValidateDayRange(row.MinDay, row.MaxDay, where, errors);
                if (row.Weight <= 0 || string.IsNullOrEmpty(row.LocalizationKey))
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define positive weight and localization", where));
                if (row.PoolItemIds == null || row.PoolItemIds.Count < 4 || row.PoolItemIds.Count >= items.Count)
                    errors.Add(GameFramework.Utility.Text.Format("{0} must use a constrained non-global item pool", where));
                else
                {
                    foreach (string itemId in row.PoolItemIds)
                    {
                        if (!items.ContainsKey(itemId))
                            errors.Add(GameFramework.Utility.Text.Format("{0} references unknown item {1}", where, itemId));
                    }
                }
                if (row.Slots == null || row.Slots.Count != 4)
                {
                    errors.Add(GameFramework.Utility.Text.Format("{0} must define exactly 4 constrained offer slots", where));
                    continue;
                }
                var slotIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (BuqiMerchantSlotConfigRow slot in row.Slots)
                {
                    if (slot == null || !slotIds.Add(slot.SlotId))
                    {
                        errors.Add(GameFramework.Utility.Text.Format("{0} slot ids must be non-empty and unique", where));
                        continue;
                    }
                    coveredSlotKinds.Add(slot.SlotKind);
                    if (slot.Weight <= 0 || slot.Count <= 0 ||
                        string.IsNullOrEmpty(slot.BuildFilter) || string.IsNullOrEmpty(slot.SizeFilter) ||
                        string.IsNullOrEmpty(slot.QualityFilter) || string.IsNullOrEmpty(slot.RequiredTag))
                    {
                        errors.Add(GameFramework.Utility.Text.Format("{0} slot {1} is not fully constrained", where, slot.SlotId));
                    }
                    ValidateDayRange(
                        slot.MinUnlockDay,
                        slot.MaxUnlockDay,
                        GameFramework.Utility.Text.Format("{0} slot {1}", where, slot.SlotId),
                        errors);
                }
            }

            foreach (string slotKind in s_MerchantSlotKinds)
            {
                if (!coveredSlotKinds.Contains(slotKind))
                    errors.Add(GameFramework.Utility.Text.Format("Merchant slots do not cover {0}", slotKind));
            }
        }

        private static void ValidateTraining(
            List<BuqiTrainerConfigRow> trainers,
            List<BuqiTrainingProjectConfigRow> projects,
            List<string> errors)
        {
            if (trainers == null || trainers.Count != 4)
                errors.Add("Trainer table must contain exactly 4 trainers");
            if (projects == null || projects.Count != 12)
                errors.Add("Training project table must contain exactly 12 projects");
            if (trainers == null || projects == null)
                return;

            var projectById = new Dictionary<string, BuqiTrainingProjectConfigRow>(StringComparer.Ordinal);
            foreach (BuqiTrainingProjectConfigRow project in projects)
            {
                if (project == null || string.IsNullOrEmpty(project.ProjectId) || projectById.ContainsKey(project.ProjectId))
                {
                    errors.Add("Training project ids must be non-empty and unique");
                    continue;
                }
                projectById.Add(project.ProjectId, project);
                ValidateDayRange(
                    project.MinDay,
                    project.MaxDay,
                    GameFramework.Utility.Text.Format("Training {0}", project.ProjectId),
                    errors);
                if (project.Cost <= 0 || project.MaxPerRun != 1 ||
                    string.IsNullOrEmpty(project.EffectKind) || string.IsNullOrEmpty(project.LocalizationKey) ||
                    string.IsNullOrEmpty(project.SummaryLocalizationKey))
                {
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Training {0} has an incomplete cost/effect contract",
                        project.ProjectId));
                }
            }

            var trainerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiTrainerConfigRow trainer in trainers)
            {
                if (trainer == null || !trainerIds.Add(trainer.TrainerId))
                {
                    errors.Add("Trainer ids must be non-empty and unique");
                    continue;
                }
                ValidateDayRange(
                    trainer.MinDay,
                    trainer.MaxDay,
                    GameFramework.Utility.Text.Format("Trainer {0}", trainer.TrainerId),
                    errors);
                if (trainer.ProjectIds == null || trainer.ProjectIds.Count != 3)
                {
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Trainer {0} must define exactly 3 projects",
                        trainer.TrainerId));
                    continue;
                }
                foreach (string projectId in trainer.ProjectIds)
                {
                    if (!projectById.TryGetValue(projectId, out BuqiTrainingProjectConfigRow project))
                        errors.Add(GameFramework.Utility.Text.Format(
                            "Trainer {0} references unknown project {1}",
                            trainer.TrainerId,
                            projectId));
                    else if (project.TrainerId != trainer.TrainerId)
                        errors.Add(GameFramework.Utility.Text.Format(
                            "Training {0} belongs to a different trainer",
                            projectId));
                }
            }
        }

        private static void ValidateEvents(
            List<BuqiEventConfigRow> events,
            List<BuqiEventOptionConfigRow> options,
            List<string> errors)
        {
            if (events == null || events.Count != 24)
                errors.Add("Event table must contain exactly 24 events");
            if (options == null || options.Count != 72)
                errors.Add("Event option table must contain exactly 72 options");
            if (events == null || options == null)
                return;

            var eventById = new Dictionary<string, BuqiEventConfigRow>(StringComparer.Ordinal);
            var optionsByEvent = new Dictionary<string, List<BuqiEventOptionConfigRow>>(StringComparer.Ordinal);
            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            int dayNineCount = 0;
            int revisitCount = 0;
            int earlyCount = 0;
            int middleCount = 0;
            int lateCount = 0;

            foreach (BuqiEventConfigRow row in events)
            {
                if (row == null || string.IsNullOrEmpty(row.EventId) || eventById.ContainsKey(row.EventId))
                {
                    errors.Add("Event ids must be non-empty and unique");
                    continue;
                }
                eventById.Add(row.EventId, row);
                ValidateDayRange(
                    row.MinDay,
                    row.MaxDay,
                    GameFramework.Utility.Text.Format("Event {0}", row.EventId),
                    errors);
                if (row.Weight <= 0 || string.IsNullOrEmpty(row.LocalizationKey))
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event {0} must define positive weight and localization",
                        row.EventId));
                if (row.OptionIds == null || row.OptionIds.Count != 3)
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event {0} must declare exactly 3 options",
                        row.EventId));
                if (row.DayNineResolution)
                    dayNineCount++;
                if (!string.IsNullOrEmpty(row.RevisitEventId))
                    revisitCount++;
                if (row.MinDay == 1)
                    earlyCount++;
                else if (row.MinDay == 4)
                    middleCount++;
                else
                    lateCount++;
            }

            foreach (BuqiEventOptionConfigRow option in options)
            {
                if (option == null || string.IsNullOrEmpty(option.OptionId) || !optionIds.Add(option.OptionId))
                {
                    errors.Add("Event option ids must be non-empty and unique");
                    continue;
                }
                if (!eventById.ContainsKey(option.EventId))
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event option {0} references unknown event {1}",
                        option.OptionId,
                        option.EventId));
                if (!optionsByEvent.TryGetValue(option.EventId, out List<BuqiEventOptionConfigRow> eventOptions))
                {
                    eventOptions = new List<BuqiEventOptionConfigRow>();
                    optionsByEvent.Add(option.EventId, eventOptions);
                }
                eventOptions.Add(option);
                if (option.Order < 1 || option.Order > 3 || string.IsNullOrEmpty(option.LocalizationKey) ||
                    string.IsNullOrEmpty(option.SummaryLocalizationKey) || string.IsNullOrEmpty(option.ConditionKind))
                {
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event option {0} has an incomplete visible contract",
                        option.OptionId));
                }
                if (option.Costs == null || option.Costs.Count == 0 || option.Outcomes == null || option.Outcomes.Count == 0)
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event option {0} must define explicit costs and outcomes",
                        option.OptionId));
                else
                {
                    foreach (BuqiEventOutcomeConfigRow eventOutcome in option.Outcomes)
                    {
                        if (eventOutcome == null || string.IsNullOrEmpty(eventOutcome.Kind) ||
                            string.IsNullOrEmpty(eventOutcome.ReasonCode))
                        {
                            errors.Add(GameFramework.Utility.Text.Format(
                                "Event option {0} has an invalid outcome",
                                option.OptionId));
                        }
                    }
                }
            }

            foreach (BuqiEventConfigRow row in events)
            {
                if (row == null || string.IsNullOrEmpty(row.EventId))
                    continue;
                if (!optionsByEvent.TryGetValue(row.EventId, out List<BuqiEventOptionConfigRow> eventOptions) ||
                    eventOptions.Count != 3)
                {
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event {0} must resolve to exactly 3 option rows",
                        row.EventId));
                    continue;
                }
                var orderSet = new HashSet<int>();
                foreach (BuqiEventOptionConfigRow option in eventOptions)
                    orderSet.Add(option.Order);
                if (orderSet.Count != 3)
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event {0} option order must be exactly 1/2/3",
                        row.EventId));
                foreach (string optionId in row.OptionIds)
                {
                    if (!optionIds.Contains(optionId))
                        errors.Add(GameFramework.Utility.Text.Format(
                            "Event {0} declares unknown option {1}",
                            row.EventId,
                            optionId));
                }
                if (!string.IsNullOrEmpty(row.RevisitEventId) && !eventById.ContainsKey(row.RevisitEventId))
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event {0} revisits unknown event {1}",
                        row.EventId,
                        row.RevisitEventId));
            }

            foreach (BuqiEventOptionConfigRow option in options)
            {
                if (option != null && !string.IsNullOrEmpty(option.FollowUpEventId) &&
                    !eventById.ContainsKey(option.FollowUpEventId))
                {
                    errors.Add(GameFramework.Utility.Text.Format(
                        "Event option {0} follows up to unknown event {1}",
                        option.OptionId,
                        option.FollowUpEventId));
                }
            }

            if (earlyCount != 8 || middleCount != 8 || lateCount != 8)
                errors.Add("Event stages must contain 8/8/8 events for days 1-3/4-6/7-9");
            if (revisitCount < 4)
                errors.Add("Event pool must contain at least four cross-day revisit chains");
            if (dayNineCount != 2)
                errors.Add("Event pool must contain exactly two Day Nine resolution events");
        }

        private static void ValidateDayRange(int minDay, int maxDay, string where, List<string> errors)
        {
            if (minDay < 1 || maxDay > 9 || minDay > maxDay)
                errors.Add(GameFramework.Utility.Text.Format("{0} has an invalid day range", where));
        }
    }
}
