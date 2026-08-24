using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.Run.Encounter
{
    [Serializable]
    public sealed class BuqiRunEventFrozenValue
    {
        public string ActionId = string.Empty;
        public string Value = string.Empty;

        public BuqiRunEventFrozenValue Clone()
        {
            return (BuqiRunEventFrozenValue)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunPendingEvent
    {
        public string EventId = string.Empty;
        public int Day;
        public BuqiRunPeriod Period;
        public string TriggeredScheduleId = string.Empty;
        public List<string> OptionIds = new List<string>();
        public List<BuqiRunEventFrozenValue> RandomResults = new List<BuqiRunEventFrozenValue>();

        public bool IsActive => !string.IsNullOrEmpty(EventId);

        public BuqiRunPendingEvent Clone()
        {
            var clone = new BuqiRunPendingEvent
            {
                EventId = EventId,
                Day = Day,
                Period = Period,
                TriggeredScheduleId = TriggeredScheduleId,
                OptionIds = new List<string>(OptionIds),
            };
            for (int index = 0; index < RandomResults.Count; index++)
            {
                clone.RandomResults.Add(RandomResults[index].Clone());
            }

            return clone;
        }
    }

    [Serializable]
    public sealed class BuqiRunEventCounter
    {
        public string CounterId = string.Empty;
        public int Value;

        public BuqiRunEventCounter Clone()
        {
            return (BuqiRunEventCounter)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunEventHistoryEntry
    {
        public string EventId = string.Empty;
        public int Day;
        public string OptionId = string.Empty;
        public string ResolutionId = string.Empty;

        public BuqiRunEventHistoryEntry Clone()
        {
            return (BuqiRunEventHistoryEntry)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunScheduledReturn
    {
        public string ScheduleId = string.Empty;
        public string EventId = string.Empty;
        public int EarliestDay;
        public int LatestDay;
        public int WeightBonus;

        public BuqiRunScheduledReturn Clone()
        {
            return (BuqiRunScheduledReturn)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunTemporaryModifier
    {
        public string ModifierId = string.Empty;
        public string SourceId = string.Empty;
        public string BuildTag = string.Empty;
        public BuqiRunModifierKind Kind;
        public int Value;
        public int RemainingBattles;
        public int DurationTicks;

        public BuqiRunTemporaryModifier Clone()
        {
            return (BuqiRunTemporaryModifier)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunResolutionRecord
    {
        public string ResolutionId = string.Empty;
        public string SourceKind = string.Empty;
        public string ContentId = string.Empty;
        public string ChoiceId = string.Empty;
        public string RequestFingerprint = string.Empty;

        public BuqiRunResolutionRecord Clone()
        {
            return (BuqiRunResolutionRecord)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunEventRuntimeState
    {
        public BuqiRunEconomySnapshot Economy = null!;
        public int Experience;
        public List<string> Flags = new List<string>();
        public List<BuqiRunEventCounter> Counters = new List<BuqiRunEventCounter>();
        public List<BuqiRunEventHistoryEntry> History = new List<BuqiRunEventHistoryEntry>();
        public List<BuqiRunScheduledReturn> ScheduledReturns = new List<BuqiRunScheduledReturn>();
        public List<BuqiRunTemporaryModifier> TemporaryModifiers = new List<BuqiRunTemporaryModifier>();
        public List<BuqiRunResolutionRecord> AppliedResolutions = new List<BuqiRunResolutionRecord>();
        public BuqiRunPendingEvent PendingEvent = new BuqiRunPendingEvent();

        public static BuqiRunEventRuntimeState CreateInitial(long runSeed, string contentVersion = "")
        {
            return new BuqiRunEventRuntimeState
            {
                Economy = BuqiRunEconomySnapshot.CreateInitial(runSeed, contentVersion),
            };
        }

        public bool HasFlag(string flagId)
        {
            return Flags.Exists(value => string.Equals(value, flagId, StringComparison.Ordinal));
        }

        public BuqiRunEventRuntimeState Clone()
        {
            var clone = new BuqiRunEventRuntimeState
            {
                Economy = Economy.Clone(),
                Experience = Experience,
                Flags = new List<string>(Flags),
                PendingEvent = PendingEvent?.Clone() ?? new BuqiRunPendingEvent(),
            };
            CloneList(Counters, clone.Counters, value => value.Clone());
            CloneList(History, clone.History, value => value.Clone());
            CloneList(ScheduledReturns, clone.ScheduledReturns, value => value.Clone());
            CloneList(TemporaryModifiers, clone.TemporaryModifiers, value => value.Clone());
            CloneList(AppliedResolutions, clone.AppliedResolutions, value => value.Clone());
            return clone;
        }

        private static void CloneList<T>(IReadOnlyList<T> source, ICollection<T> target, Func<T, T> clone)
        {
            for (int index = 0; index < source.Count; index++)
            {
                target.Add(clone(source[index]));
            }
        }
    }

    public sealed class BuqiRunEventSelectionResult
    {
        public bool Success;
        public bool Created;
        public string FailureReason = string.Empty;
        public BuqiRunEventRuntimeState State = null!;
        public BuqiRunPendingEvent Pending = null!;
    }

    public sealed class BuqiRunEventExecutionResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunEventRuntimeState State = null!;
    }
}
