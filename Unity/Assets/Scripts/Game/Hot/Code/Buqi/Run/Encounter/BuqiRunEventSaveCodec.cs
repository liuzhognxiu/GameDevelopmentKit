using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.Run.Encounter
{
    [Serializable]
    public sealed class BuqiRunEventSaveData
    {
        public string SchemaVersion = BuqiRunEventSaveCodec.CurrentVersion;
        public long RunSeed;
        public string ContentVersion = string.Empty;
        public string RuleVersion = string.Empty;
        public int RngCursor;
        public int Revision;
        public int Day;
        public BuqiRunPeriod Period;
        public int Experience;
        public List<string> Flags = new List<string>();
        public List<BuqiRunEventCounter> Counters = new List<BuqiRunEventCounter>();
        public List<BuqiRunEventHistoryEntry> History = new List<BuqiRunEventHistoryEntry>();
        public List<BuqiRunScheduledReturn> ScheduledReturns = new List<BuqiRunScheduledReturn>();
        public List<BuqiRunTemporaryModifier> TemporaryModifiers = new List<BuqiRunTemporaryModifier>();
        public List<BuqiRunResolutionRecord> AppliedResolutions = new List<BuqiRunResolutionRecord>();
        public BuqiRunPendingEvent PendingEvent = new BuqiRunPendingEvent();
    }

    public sealed class BuqiRunEventSaveCodec
    {
        public const string CurrentVersion = "buqi-event-runtime-v2";
        public const string LegacyVersion = "buqi-event-runtime-v1";

        public BuqiRunEventSaveData Capture(BuqiRunEventRuntimeState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var save = new BuqiRunEventSaveData
            {
                SchemaVersion = CurrentVersion,
                RunSeed = source.Economy.Run.RunSeed,
                ContentVersion = source.Economy.Run.ContentVersion,
                RuleVersion = source.Economy.Run.RuleVersion,
                RngCursor = source.Economy.Run.RngCursor,
                Revision = source.Economy.Run.Revision,
                Day = source.Economy.Run.Day,
                Period = source.Economy.Run.Period,
                Experience = source.Experience,
                Flags = source.Flags == null ? new List<string>() : new List<string>(source.Flags),
                PendingEvent = source.PendingEvent?.Clone() ?? new BuqiRunPendingEvent(),
            };
            CloneList(source.Counters, save.Counters, value => value.Clone());
            CloneList(source.History, save.History, value => value.Clone());
            CloneList(source.ScheduledReturns, save.ScheduledReturns, value => value.Clone());
            CloneList(source.TemporaryModifiers, save.TemporaryModifiers, value => value.Clone());
            CloneList(source.AppliedResolutions, save.AppliedResolutions, value => value.Clone());
            Sort(save);
            return save;
        }

        public bool TryRestore(
            BuqiRunEconomySnapshot economy,
            BuqiRunEventSaveData save,
            out BuqiRunEventRuntimeState state,
            out string error)
        {
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));

            state = null;
            if (save == null)
            {
                error = "Event save data is required.";
                return false;
            }
            if (!string.Equals(save.SchemaVersion, CurrentVersion, StringComparison.Ordinal) &&
                !string.Equals(save.SchemaVersion, LegacyVersion, StringComparison.Ordinal))
            {
                error = "Event save schema version is unsupported.";
                return false;
            }
            if (save.Experience < 0)
            {
                error = "Saved experience must be non-negative.";
                return false;
            }

            bool legacy = string.Equals(save.SchemaVersion, LegacyVersion, StringComparison.Ordinal);
            if (!legacy && !MatchesRun(save, economy))
            {
                error = "Event save data does not match the supplied run snapshot.";
                return false;
            }
            if (!TryValidateCollections(save, legacy, economy, out error))
                return false;

            var restored = new BuqiRunEventRuntimeState
            {
                Economy = economy.Clone(),
                Experience = save.Experience,
                Flags = save.Flags == null ? new List<string>() : new List<string>(save.Flags),
                PendingEvent = ClonePending(save.PendingEvent),
            };
            CloneList(save.Counters, restored.Counters, value => value.Clone());
            CloneList(save.History, restored.History, value => value.Clone());
            CloneList(save.ScheduledReturns, restored.ScheduledReturns, value => value.Clone());
            CloneList(save.TemporaryModifiers, restored.TemporaryModifiers, value => value.Clone());
            CloneList(save.AppliedResolutions, restored.AppliedResolutions, value => value.Clone());

            BuqiRunEventSaveData canonical = Capture(restored);
            restored.Flags = canonical.Flags;
            restored.Counters = canonical.Counters;
            restored.History = canonical.History;
            restored.ScheduledReturns = canonical.ScheduledReturns;
            restored.TemporaryModifiers = canonical.TemporaryModifiers;
            restored.AppliedResolutions = canonical.AppliedResolutions;
            restored.PendingEvent = canonical.PendingEvent;
            state = restored;
            error = string.Empty;
            return true;
        }

        private static bool TryValidateCollections(
            BuqiRunEventSaveData save,
            bool legacy,
            BuqiRunEconomySnapshot economy,
            out string error)
        {
            if (!legacy && (save.Flags == null || save.Counters == null || save.History == null ||
                            save.ScheduledReturns == null || save.TemporaryModifiers == null ||
                            save.AppliedResolutions == null || save.PendingEvent == null))
            {
                error = "Current event save collections must not be null.";
                return false;
            }

            if (!TryValidateIds(save.Flags, value => value, "flag", out error) ||
                !TryValidateIds(save.Counters, value => value.CounterId, "counter", out error) ||
                !TryValidateIds(save.ScheduledReturns, value => value.ScheduleId, "scheduled return", out error) ||
                !TryValidateIds(save.TemporaryModifiers, value => value.ModifierId, "temporary modifier", out error) ||
                !TryValidateIds(save.AppliedResolutions, value => value.ResolutionId, "resolution", out error))
            {
                return false;
            }

            if (save.Counters != null)
            {
                for (int index = 0; index < save.Counters.Count; index++)
                {
                    if (save.Counters[index] == null)
                    {
                        error = "Saved counter must not be null.";
                        return false;
                    }
                }
            }
            if (save.History != null)
            {
                for (int index = 0; index < save.History.Count; index++)
                {
                    BuqiRunEventHistoryEntry entry = save.History[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.EventId) ||
                        entry.Day < 1 || string.IsNullOrWhiteSpace(entry.ResolutionId))
                    {
                        error = "Saved event history is invalid.";
                        return false;
                    }
                }
            }
            if (save.ScheduledReturns != null)
            {
                for (int index = 0; index < save.ScheduledReturns.Count; index++)
                {
                    BuqiRunScheduledReturn scheduled = save.ScheduledReturns[index];
                    if (scheduled == null || string.IsNullOrWhiteSpace(scheduled.EventId) ||
                        scheduled.EarliestDay < 1 || scheduled.LatestDay < scheduled.EarliestDay ||
                        scheduled.WeightBonus < 0)
                    {
                        error = "Saved scheduled return is invalid.";
                        return false;
                    }
                }
            }
            if (save.TemporaryModifiers != null)
            {
                for (int index = 0; index < save.TemporaryModifiers.Count; index++)
                {
                    BuqiRunTemporaryModifier modifier = save.TemporaryModifiers[index];
                    if (modifier == null || modifier.RemainingBattles < 1 || modifier.DurationTicks < 0 ||
                        !Enum.IsDefined(typeof(BuqiRunModifierKind), modifier.Kind))
                    {
                        error = "Saved temporary modifier is invalid.";
                        return false;
                    }
                }
            }
            if (save.AppliedResolutions != null)
            {
                for (int index = 0; index < save.AppliedResolutions.Count; index++)
                {
                    BuqiRunResolutionRecord resolution = save.AppliedResolutions[index];
                    if (resolution == null || string.IsNullOrWhiteSpace(resolution.SourceKind) ||
                        string.IsNullOrWhiteSpace(resolution.ContentId) ||
                        string.IsNullOrWhiteSpace(resolution.ChoiceId) ||
                        (!legacy && string.IsNullOrWhiteSpace(resolution.RequestFingerprint)))
                    {
                        error = "Saved resolution record is invalid.";
                        return false;
                    }
                }
            }

            return TryValidatePending(save.PendingEvent, economy, legacy, out error);
        }

        private static bool TryValidatePending(
            BuqiRunPendingEvent pending,
            BuqiRunEconomySnapshot economy,
            bool legacy,
            out string error)
        {
            error = string.Empty;
            if (pending == null)
                return legacy;
            if (pending.OptionIds == null || pending.RandomResults == null)
            {
                if (legacy && !pending.IsActive)
                    return true;
                error = "Saved pending event collections must not be null.";
                return false;
            }
            if (!pending.IsActive)
                return true;
            if (pending.Day != economy.Run.Day || pending.Period != economy.Run.Period ||
                pending.OptionIds.Count != 3 ||
                !TryValidateIds(pending.OptionIds, value => value, "pending option", out error) ||
                !TryValidateIds(pending.RandomResults, value => value.ActionId, "frozen result", out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = "Saved pending event is invalid.";
                return false;
            }

            for (int index = 0; index < pending.RandomResults.Count; index++)
            {
                if (pending.RandomResults[index] == null ||
                    string.IsNullOrWhiteSpace(pending.RandomResults[index].Value))
                {
                    error = "Saved frozen result is invalid.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool TryValidateIds<T>(
            IReadOnlyList<T> values,
            Func<T, string> idSelector,
            string label,
            out string error)
        {
            if (values == null)
            {
                error = string.Empty;
                return true;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                T value = values[index];
                if (value == null)
                {
                    error = GameFramework.Utility.Text.Format(
                        "Saved {0} must not be null.",
                        label);
                    return false;
                }
                string id = idSelector(value);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    error = GameFramework.Utility.Text.Format(
                        "Saved {0} ids must be non-empty and unique.",
                        label);
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static void Sort(BuqiRunEventSaveData save)
        {
            save.Flags.Sort(StringComparer.Ordinal);
            save.Counters.Sort((left, right) => string.CompareOrdinal(left.CounterId, right.CounterId));
            save.History.Sort((left, right) =>
            {
                int day = left.Day.CompareTo(right.Day);
                if (day != 0)
                    return day;
                int eventId = string.CompareOrdinal(left.EventId, right.EventId);
                if (eventId != 0)
                    return eventId;
                int resolution = string.CompareOrdinal(left.ResolutionId, right.ResolutionId);
                return resolution != 0
                    ? resolution
                    : string.CompareOrdinal(left.OptionId, right.OptionId);
            });
            save.ScheduledReturns.Sort((left, right) =>
            {
                int day = left.EarliestDay.CompareTo(right.EarliestDay);
                if (day != 0)
                    return day;
                return string.CompareOrdinal(left.ScheduleId, right.ScheduleId);
            });
            save.TemporaryModifiers.Sort((left, right) =>
                string.CompareOrdinal(left.ModifierId, right.ModifierId));
            save.AppliedResolutions.Sort((left, right) =>
                string.CompareOrdinal(left.ResolutionId, right.ResolutionId));
            save.PendingEvent.RandomResults.Sort((left, right) =>
                string.CompareOrdinal(left.ActionId, right.ActionId));
        }

        private static void CloneList<T>(IReadOnlyList<T> source, ICollection<T> target, Func<T, T> clone)
        {
            if (source == null)
                return;
            for (int index = 0; index < source.Count; index++)
            {
                target.Add(clone(source[index]));
            }
        }

        private static bool MatchesRun(BuqiRunEventSaveData save, BuqiRunEconomySnapshot economy)
        {
            return save.RunSeed == economy.Run.RunSeed &&
                   string.Equals(save.ContentVersion, economy.Run.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(save.RuleVersion, economy.Run.RuleVersion, StringComparison.Ordinal) &&
                   save.RngCursor == economy.Run.RngCursor &&
                   save.Revision == economy.Run.Revision &&
                   save.Day == economy.Run.Day &&
                   save.Period == economy.Run.Period;
        }

        private static BuqiRunPendingEvent ClonePending(BuqiRunPendingEvent source)
        {
            if (source == null)
                return new BuqiRunPendingEvent();

            var clone = new BuqiRunPendingEvent
            {
                EventId = source.EventId,
                Day = source.Day,
                Period = source.Period,
                TriggeredScheduleId = source.TriggeredScheduleId,
                OptionIds = source.OptionIds == null
                    ? new List<string>()
                    : new List<string>(source.OptionIds),
            };
            if (source.RandomResults != null)
            {
                for (int index = 0; index < source.RandomResults.Count; index++)
                {
                    clone.RandomResults.Add(source.RandomResults[index].Clone());
                }
            }
            return clone;
        }
    }
}
