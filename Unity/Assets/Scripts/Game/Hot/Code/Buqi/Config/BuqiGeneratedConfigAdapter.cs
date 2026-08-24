using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using BattleConditionKind = Game.Hot.Buqi.Battle.BuqiConditionKind;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;
using GeneratedBuild = Game.Hot.BuqiBuild;

namespace Game.Hot.Buqi.Config
{
    public static class BuqiGeneratedConfigAdapter
    {
        public static bool HasGeneratedTables(object tables)
        {
            return tables != null && TryGetMember(tables, "DTBuqiGlobal", out _);
        }

        public static bool TryReadFromTables(
            object tables,
            out BuqiConfigCatalog catalog,
            out List<string> errors)
        {
            catalog = new BuqiConfigCatalog();
            errors = new List<string>();
            if (tables == null)
            {
                errors.Add("配置表组件不能为空");
                return false;
            }

            if (!TryReadGlobal(tables, catalog, errors))
                return false;
            ReadItems(tables, catalog, errors);
            ReadRefinements(tables, catalog, errors);
            ReadEchoes(tables, catalog, errors);
            ReadMerchants(tables, catalog, errors);
            ReadTrainers(tables, catalog, errors);
            ReadTrainingProjects(tables, catalog, errors);
            ReadEvents(tables, catalog, errors);
            ReadEventOptions(tables, catalog, errors);
            return errors.Count == 0;
        }

        private static bool TryReadGlobal(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            object table = RequireMember(tables, "DTBuqiGlobal", errors);
            object row = table == null ? null : RequireMember(table, "Data", errors);
            if (row == null)
                return false;

            catalog.Global = new BuqiGlobalConfigRow
            {
                ContentVersion = ReadString(row, "ContentVersion", errors),
                InitialExecution = ReadInt(row, "InitialExecution", errors),
                BufferCap = ReadInt(row, "BufferCap", errors),
                NoiseThreshold = ReadInt(row, "NoiseThreshold", errors),
                NoiseIncidentDamage = ReadInt(row, "NoiseIncidentDamage", errors),
                BoardSlotCount = ReadInt(row, "BoardSlotCount", errors),
                StormStartTicks = ReadInt(row, "StormStartTicks", errors),
                StormBaseDamage = ReadInt(row, "StormBaseDamage", errors),
                StormRampDamage = ReadInt(row, "StormRampDamage", errors),
                MaxTickEvents = ReadInt(row, "MaxTickEvents", errors),
                MaxItemEventsPerTick = ReadInt(row, "MaxItemEventsPerTick", errors),
                MaxChainDepth = ReadInt(row, "MaxChainDepth", errors),
                MaxRepeatedReasonPerTick = ReadInt(row, "MaxRepeatedReasonPerTick", errors),
                DailyEconomyProcCap = ReadInt(row, "DailyEconomyProcCap", errors),
            };
            return true;
        }

        private static void ReadItems(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiItem", errors))
            {
                var item = new BuqiItemConfigRow
                {
                    DefinitionId = ReadString(row, "DefinitionId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    DesignNote = ReadString(row, "DesignNote", errors),
                    EffectDescription = ReadString(row, "EffectDescription", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    Size = ReadEnum(row, "Size", BattleSize.S, errors),
                    BasePrice = ReadInt(row, "BasePrice", errors),
                    ImprovedUpgradeCost = ReadInt(row, "ImprovedUpgradeCost", errors),
                    FixedUpgradeCost = ReadInt(row, "FixedUpgradeCost", errors),
                    RefinementCost = ReadInt(row, "RefinementCost", errors),
                    BaseCooldownTicks = ReadInt(row, "BaseCooldownTicks", errors),
                    AmmoCapacity = ReadOptionalInt(row, "AmmoCapacity", 0, errors),
                    ArchetypeId = ReadEnum(row, "ArchetypeId", GeneratedBuild.fast, errors).ToString(),
                    Role = ReadString(row, "Role", errors),
                    UnlockDay = ReadInt(row, "UnlockDay", errors),
                    PositionHint = ReadString(row, "PositionHint", errors),
                    Tags = ReadStringList(row, "Tags"),
                    UpgradeSummary = ReadString(row, "UpgradeSummary", errors),
                    UpgradeLocalizationKey = ReadString(row, "UpgradeLocalizationKey", errors),
                    LinkIds = ReadStringList(row, "LinkIds"),
                };
                foreach (object effectRow in ReadObjectList(row, "Effects"))
                    item.Effects.Add(ReadEffect(effectRow, errors));
                foreach (object effectRow in ReadObjectList(row, "RunEffects"))
                    item.RunEffects.Add(ReadRunEffect(effectRow, errors));
                catalog.Items.Add(item);
            }
        }

        private static BuqiRunEffectConfigRow ReadRunEffect(object row, List<string> errors)
        {
            return new BuqiRunEffectConfigRow
            {
                Trigger = ReadString(row, "Trigger", errors),
                Effect = ReadString(row, "Effect", errors),
                Amount = ReadInt(row, "Amount", errors),
                Threshold = ReadInt(row, "Threshold", errors),
                MaxPerDay = ReadInt(row, "MaxPerDay", errors),
                ReasonCode = ReadString(row, "ReasonCode", errors),
            };
        }

        private static BuqiEffectConfigRow ReadEffect(object row, List<string> errors)
        {
            return new BuqiEffectConfigRow
            {
                Trigger = ReadEnum(row, "Trigger", BattleTrigger.OnUse, errors),
                Effect = ReadEnum(row, "Effect", BattleEffect.Damage, errors),
                Target = ReadEnum(row, "Target", BattleTarget.EnemyExecution, errors),
                Amount = ReadInt(row, "Amount", errors),
                DurationTicks = ReadInt(row, "DurationTicks", errors),
                ReasonCode = ReadString(row, "ReasonCode", errors),
                ConditionKind = ReadEnum(row, "ConditionKind", BattleConditionKind.None, errors),
                ConditionThreshold = ReadInt(row, "ConditionThreshold", errors),
                UseCountThreshold = ReadInt(row, "UseCountThreshold", errors),
                ResetCountOnReached = ReadBool(row, "ResetCountOnReached", errors),
                CriticalChanceBps = ReadInt(row, "CriticalChanceBps", errors),
                RepeatCount = ReadInt(row, "RepeatCount", errors),
                RageThreshold = ReadInt(row, "RageThreshold", errors),
                RageDurationTicks = ReadInt(row, "RageDurationTicks", errors),
                RageCooldownReductionBps = ReadInt(row, "RageCooldownReductionBps", errors),
                FlightDamageBonusBps = ReadInt(row, "FlightDamageBonusBps", errors),
                FlightEndDamage = ReadInt(row, "FlightEndDamage", errors),
            };
        }

        private static void ReadRefinements(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiRefinement", errors))
            {
                catalog.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = ReadString(row, "RefinementId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    Summary = ReadString(row, "Summary", errors),
                });
            }
        }

        private static void ReadEchoes(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiEcho", errors))
            {
                object snapshotRow = RequireMember(row, "Snapshot", errors);
                catalog.Echoes.Add(new BuqiEchoConfigRow
                {
                    EchoId = ReadString(row, "EchoId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    Tier = ReadString(row, "Tier", errors),
                    Build = ReadEnum(row, "Build", GeneratedBuild.fast, errors).ToString(),
                    Snapshot = ReadSnapshot(snapshotRow, errors),
                });
            }
        }

        private static BuqiBuildSnapshotConfigRow ReadSnapshot(object row, List<string> errors)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = ReadString(row, "SnapshotId", errors),
                ArchetypeId = ReadEnum(row, "ArchetypeId", GeneratedBuild.fast, errors).ToString(),
                InitialExecution = ReadInt(row, "InitialExecution", errors),
                InitialBuffer = ReadInt(row, "InitialBuffer", errors),
                InitialNoiseDebt = ReadInt(row, "InitialNoiseDebt", errors),
            };
            foreach (object itemRow in ReadObjectList(row, "Items"))
            {
                snapshot.Items.Add(new BuqiItemInstanceConfigRow
                {
                    InstanceId = ReadString(itemRow, "InstanceId", errors),
                    DefinitionId = ReadString(itemRow, "DefinitionId", errors),
                    Quality = ReadEnum(itemRow, "Quality", BattleQuality.Normal, errors),
                    AnchorSlot = ReadInt(itemRow, "AnchorSlot", errors),
                    RefinementId = NormalizeOptionalId(ReadString(itemRow, "RefinementId", errors)),
                });
            }
            return snapshot;
        }

        private static void ReadMerchants(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiMerchant", errors))
            {
                var merchant = new BuqiMerchantConfigRow
                {
                    MerchantId = ReadString(row, "MerchantId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    MinDay = ReadInt(row, "MinDay", errors),
                    MaxDay = ReadInt(row, "MaxDay", errors),
                    Weight = ReadInt(row, "Weight", errors),
                    PoolItemIds = ReadStringList(row, "PoolItemIds"),
                };
                foreach (object slotRow in ReadObjectList(row, "Slots"))
                {
                    merchant.Slots.Add(new BuqiMerchantSlotConfigRow
                    {
                        SlotId = ReadString(slotRow, "SlotId", errors),
                        SlotKind = ReadString(slotRow, "SlotKind", errors),
                        BuildFilter = ReadString(slotRow, "BuildFilter", errors),
                        SizeFilter = ReadString(slotRow, "SizeFilter", errors),
                        QualityFilter = ReadString(slotRow, "QualityFilter", errors),
                        RequiredTag = ReadString(slotRow, "RequiredTag", errors),
                        MinUnlockDay = ReadInt(slotRow, "MinUnlockDay", errors),
                        MaxUnlockDay = ReadInt(slotRow, "MaxUnlockDay", errors),
                        Weight = ReadInt(slotRow, "Weight", errors),
                        Count = ReadInt(slotRow, "Count", errors),
                    });
                }
                catalog.Merchants.Add(merchant);
            }
        }

        private static void ReadTrainers(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiTrainer", errors))
            {
                catalog.Trainers.Add(new BuqiTrainerConfigRow
                {
                    TrainerId = ReadString(row, "TrainerId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    MinDay = ReadInt(row, "MinDay", errors),
                    MaxDay = ReadInt(row, "MaxDay", errors),
                    Weight = ReadInt(row, "Weight", errors),
                    ProjectIds = ReadStringList(row, "ProjectIds"),
                });
            }
        }

        private static void ReadTrainingProjects(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiTrainingProject", errors))
            {
                catalog.TrainingProjects.Add(new BuqiTrainingProjectConfigRow
                {
                    ProjectId = ReadString(row, "ProjectId", errors),
                    TrainerId = ReadString(row, "TrainerId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    MinDay = ReadInt(row, "MinDay", errors),
                    MaxDay = ReadInt(row, "MaxDay", errors),
                    Cost = ReadInt(row, "Cost", errors),
                    RequiredTag = ReadString(row, "RequiredTag", errors),
                    ExcludedTag = ReadString(row, "ExcludedTag", errors),
                    EffectKind = ReadString(row, "EffectKind", errors),
                    Amount = ReadInt(row, "Amount", errors),
                    Duration = ReadInt(row, "Duration", errors),
                    MaxPerRun = ReadInt(row, "MaxPerRun", errors),
                    Summary = ReadString(row, "Summary", errors),
                    SummaryLocalizationKey = ReadString(row, "SummaryLocalizationKey", errors),
                });
            }
        }

        private static void ReadEvents(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiEvent", errors))
            {
                catalog.Events.Add(new BuqiEventConfigRow
                {
                    EventId = ReadString(row, "EventId", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    MinDay = ReadInt(row, "MinDay", errors),
                    MaxDay = ReadInt(row, "MaxDay", errors),
                    Weight = ReadInt(row, "Weight", errors),
                    RequiredFlags = ReadStringList(row, "RequiredFlags"),
                    ForbiddenFlags = ReadStringList(row, "ForbiddenFlags"),
                    RevisitEventId = NormalizeOptionalId(ReadString(row, "RevisitEventId", errors)),
                    RevisitDelayDays = ReadInt(row, "RevisitDelayDays", errors),
                    DayNineResolution = ReadBool(row, "DayNineResolution", errors),
                    OptionIds = ReadStringList(row, "OptionIds"),
                });
            }
        }

        private static void ReadEventOptions(
            object tables,
            BuqiConfigCatalog catalog,
            List<string> errors)
        {
            foreach (object row in ReadTableRows(tables, "DTBuqiEventOption", errors))
            {
                var option = new BuqiEventOptionConfigRow
                {
                    OptionId = ReadString(row, "OptionId", errors),
                    EventId = ReadString(row, "EventId", errors),
                    Order = ReadInt(row, "Order", errors),
                    DisplayName = ReadString(row, "DisplayName", errors),
                    LocalizationKey = ReadString(row, "LocalizationKey", errors),
                    ConditionKind = ReadString(row, "ConditionKind", errors),
                    ConditionValue = ReadString(row, "ConditionValue", errors),
                    RequiredFlags = ReadStringList(row, "RequiredFlags"),
                    ForbiddenFlags = ReadStringList(row, "ForbiddenFlags"),
                    SetFlags = ReadStringList(row, "SetFlags"),
                    ClearFlags = ReadStringList(row, "ClearFlags"),
                    FollowUpEventId = NormalizeOptionalId(ReadString(row, "FollowUpEventId", errors)),
                    FollowUpDelayDays = ReadInt(row, "FollowUpDelayDays", errors),
                    Summary = ReadString(row, "Summary", errors),
                    SummaryLocalizationKey = ReadString(row, "SummaryLocalizationKey", errors),
                };
                foreach (object costRow in ReadObjectList(row, "Costs"))
                {
                    option.Costs.Add(new BuqiEventCostConfigRow
                    {
                        Kind = ReadString(costRow, "Kind", errors),
                        Amount = ReadInt(costRow, "Amount", errors),
                        Value = ReadString(costRow, "Value", errors),
                    });
                }
                foreach (object outcomeRow in ReadObjectList(row, "Outcomes"))
                {
                    option.Outcomes.Add(new BuqiEventOutcomeConfigRow
                    {
                        Kind = ReadString(outcomeRow, "Kind", errors),
                        Amount = ReadInt(outcomeRow, "Amount", errors),
                        Value = ReadString(outcomeRow, "Value", errors),
                        DurationDays = ReadInt(outcomeRow, "DurationDays", errors),
                        ReasonCode = ReadString(outcomeRow, "ReasonCode", errors),
                    });
                }
                catalog.EventOptions.Add(option);
            }
        }

        private static IEnumerable<object> ReadTableRows(
            object tables,
            string tableName,
            List<string> errors)
        {
            object table = RequireMember(tables, tableName, errors);
            if (table == null)
                yield break;
            object dataList = RequireMember(table, "DataList", errors);
            if (dataList == null)
                yield break;
            foreach (object row in (IEnumerable)dataList)
                yield return row;
        }

        private static object RequireMember(object source, string name, List<string> errors)
        {
            if (source == null)
            {
                errors.Add(BuqiText.Format("缺少 {0}：数据源为空", name));
                return null;
            }
            if (TryGetMember(source, name, out object value))
                return value;
            errors.Add(BuqiText.Format("{0} 不包含成员 {1}", source.GetType().Name, name));
            return null;
        }

        private static bool TryGetMember(object source, string name, out object value)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = source.GetType().GetProperty(name, Flags);
            if (property != null)
            {
                value = property.GetValue(source, null);
                return true;
            }
            FieldInfo field = source.GetType().GetField(name, Flags);
            if (field != null)
            {
                value = field.GetValue(source);
                return true;
            }
            value = null;
            return false;
        }

        private static string ReadString(object source, string name, List<string> errors)
        {
            object value = RequireMember(source, name, errors);
            return value == null ? string.Empty : value.ToString();
        }

        private static int ReadInt(object source, string name, List<string> errors)
        {
            object value = RequireMember(source, name, errors);
            if (value == null)
                return 0;
            if (value is int intValue)
                return intValue;
            if (int.TryParse(value.ToString(), out int parsed))
                return parsed;
            errors.Add(BuqiText.Format("{0}.{1} 不是整数", source.GetType().Name, name));
            return 0;
        }

        private static int ReadOptionalInt(
            object source,
            string name,
            int fallback,
            List<string> errors)
        {
            if (source == null || !TryGetMember(source, name, out object value) || value == null)
                return fallback;
            if (value is int intValue)
                return intValue;
            if (int.TryParse(value.ToString(), out int parsed))
                return parsed;
            errors.Add(BuqiText.Format("{0}.{1} 不是整数", source.GetType().Name, name));
            return fallback;
        }

        private static bool ReadBool(object source, string name, List<string> errors)
        {
            object value = RequireMember(source, name, errors);
            if (value == null)
                return false;
            if (value is bool boolValue)
                return boolValue;
            if (bool.TryParse(value.ToString(), out bool parsed))
                return parsed;
            errors.Add(BuqiText.Format("{0}.{1} 不是布尔值", source.GetType().Name, name));
            return false;
        }

        private static T ReadEnum<T>(object source, string name, T fallback, List<string> errors)
            where T : struct
        {
            object value = RequireMember(source, name, errors);
            if (value == null)
                return fallback;
            if (value is T typed)
                return typed;
            string text = value.ToString();
            if (Enum.TryParse(text, out T parsed))
                return parsed;
            if (int.TryParse(text, out int intValue) &&
                Enum.IsDefined(typeof(T), intValue))
            {
                return (T)Enum.ToObject(typeof(T), intValue);
            }
            errors.Add(BuqiText.Format("{0}.{1} 不是有效的 {2}", source.GetType().Name, name, typeof(T).Name));
            return fallback;
        }

        private static List<string> ReadStringList(object source, string name)
        {
            var result = new List<string>();
            foreach (object value in ReadObjectList(source, name))
            {
                if (value != null)
                    result.Add(value.ToString());
            }
            return result;
        }

        private static List<object> ReadObjectList(object source, string name)
        {
            var result = new List<object>();
            if (source == null || !TryGetMember(source, name, out object value) || value == null)
                return result;
            foreach (object item in (IEnumerable)value)
                result.Add(item);
            return result;
        }

        private static string NormalizeOptionalId(string value)
        {
            return value == "None" ? string.Empty : value;
        }
    }
}
