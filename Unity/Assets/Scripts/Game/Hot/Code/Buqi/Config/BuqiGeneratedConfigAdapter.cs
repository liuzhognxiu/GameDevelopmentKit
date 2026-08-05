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
                errors.Add("tables component is null");
                return false;
            }

            if (!TryReadGlobal(tables, catalog, errors))
                return false;
            ReadItems(tables, catalog, errors);
            ReadRefinements(tables, catalog, errors);
            ReadEchoes(tables, catalog, errors);
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
                NormalDurationTicks = ReadInt(row, "NormalDurationTicks", errors),
                HardCapTicks = ReadInt(row, "HardCapTicks", errors),
                OvertimeStartTicks = ReadInt(row, "OvertimeStartTicks", errors),
                MaxTickEvents = ReadInt(row, "MaxTickEvents", errors),
                MaxItemEventsPerTick = ReadInt(row, "MaxItemEventsPerTick", errors),
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
                    Size = ReadEnum(row, "Size", BattleSize.S, errors),
                    BasePrice = ReadInt(row, "BasePrice", errors),
                    BaseCooldownTicks = ReadInt(row, "BaseCooldownTicks", errors),
                    ArchetypeId = ReadString(row, "ArchetypeId", errors),
                    Tags = ReadStringList(row, "Tags"),
                };
                foreach (object effectRow in ReadObjectList(row, "Effects"))
                    item.Effects.Add(ReadEffect(effectRow, errors));
                catalog.Items.Add(item);
            }
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
                ChargeReadLimit = ReadInt(row, "ChargeReadLimit", errors),
                AmountPerCharge = ReadInt(row, "AmountPerCharge", errors),
                ChargeConsume = ReadBool(row, "ChargeConsume", errors),
                ResetCountOnReached = ReadBool(row, "ResetCountOnReached", errors),
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
                    Build = ReadString(row, "Build", errors),
                    Snapshot = ReadSnapshot(snapshotRow, errors),
                });
            }
        }

        private static BuqiBuildSnapshotConfigRow ReadSnapshot(object row, List<string> errors)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = ReadString(row, "SnapshotId", errors),
                ArchetypeId = ReadString(row, "ArchetypeId", errors),
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
                errors.Add(BuqiText.Format("missing {0}: source is null", name));
                return null;
            }
            if (TryGetMember(source, name, out object value))
                return value;
            errors.Add(BuqiText.Format("{0} does not contain member {1}", source.GetType().Name, name));
            return null;
        }

        private static bool TryGetMember(object source, string name, out object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = source.GetType().GetProperty(name, flags);
            if (property != null)
            {
                value = property.GetValue(source, null);
                return true;
            }
            FieldInfo field = source.GetType().GetField(name, flags);
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
            errors.Add(BuqiText.Format("{0}.{1} is not an int", source.GetType().Name, name));
            return 0;
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
            errors.Add(BuqiText.Format("{0}.{1} is not a bool", source.GetType().Name, name));
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
            errors.Add(BuqiText.Format("{0}.{1} is not a valid {2}", source.GetType().Name, name, typeof(T).Name));
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
