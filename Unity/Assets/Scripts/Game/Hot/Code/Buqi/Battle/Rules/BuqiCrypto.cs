using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 构筑快照与战斗日志的规范化、SHA-256 摘要工具。
    /// 规范化文本是跨 Unity 与 .NET 无头端的版本化协议，字段顺序或编码方式不可随意修改。
    /// </summary>
    public static class BuqiCrypto
    {
        /// <summary>以 UTF-8 编码输入并返回小写十六进制 SHA-256。</summary>
        public static string Sha256Hex(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 将构筑快照转换为与输入列表顺序无关的规范文本。
        /// 法门按锚点、实例 ID 排序，临时修正也稳定排序；所有字符串采用长度前缀编码，避免分隔符转义歧义。
        /// </summary>
        public static string CanonicalSnapshot(BuildSnapshot snapshot)
        {
            if (snapshot == null)
                return "N";

            var builder = new StringBuilder();
            AppendString(builder, snapshot.SnapshotId);
            AppendString(builder, snapshot.ContentVersion);
            AppendString(builder, snapshot.ArchetypeId);
            AppendInt(builder, snapshot.InitialExecution);
            AppendInt(builder, snapshot.InitialBuffer);
            AppendInt(builder, snapshot.InitialNoiseDebt);

            var items = snapshot.Items == null
                ? new List<ItemInstance>()
                : new List<ItemInstance>(snapshot.Items);
            items.Sort(CompareItems);
            AppendInt(builder, items.Count);
            foreach (ItemInstance item in items)
                AppendItem(builder, item);
            return builder.ToString();
        }

        /// <summary>计算构筑规范文本的 SHA-256，用作请求身份和回放索引。</summary>
        public static string SnapshotHash(BuildSnapshot snapshot)
        {
            return Sha256Hex(CanonicalSnapshot(snapshot));
        }

        /// <summary>
        /// 将结果元数据与全量稳定事件序列转换为规范日志。
        /// 规则、模拟和内容版本均进入哈希，避免不同协议版本被误判为同一场战斗。
        /// </summary>
        public static string CanonicalLog(BattleResult result, List<BattleEvent> events)
        {
            var builder = new StringBuilder();
            AppendString(builder, result.RuleVersion);
            AppendString(builder, result.SimulationVersion);
            AppendString(builder, result.ContentVersion);
            AppendUlong(builder, result.BattleSeed);
            AppendInt(builder, result.RoundIndex);
            AppendInt(builder, (int)result.Outcome);
            AppendInt(builder, result.DurationTicks);
            AppendInt(builder, result.LeftExecution);
            AppendInt(builder, result.RightExecution);
            AppendInt(builder, result.LeftBuffer);
            AppendInt(builder, result.RightBuffer);
            AppendInt(builder, result.LeftNoise);
            AppendInt(builder, result.RightNoise);
            AppendString(builder, result.TerminationReason);
            AppendString(builder, result.LeftSnapshotHash);
            AppendString(builder, result.RightSnapshotHash);

            List<BattleEvent> safeEvents = events ?? new List<BattleEvent>();
            AppendInt(builder, safeEvents.Count);
            foreach (BattleEvent battleEvent in safeEvents)
            {
                AppendInt(builder, battleEvent.Sequence);
                AppendInt(builder, battleEvent.Tick);
                AppendInt(builder, (int)battleEvent.Phase);
                AppendInt(builder, battleEvent.ChainDepth);
                AppendString(builder, battleEvent.ChainId);
                AppendString(builder, battleEvent.ActorInstanceId);
                AppendString(builder, battleEvent.SourceInstanceId);
                AppendString(builder, battleEvent.TargetInstanceId);
                AppendInt(builder, (int)battleEvent.Type);
                AppendInt(builder, battleEvent.Amount);
                AppendString(builder, battleEvent.EffectId);
                AppendString(builder, battleEvent.ReasonCode);
            }
            return builder.ToString();
        }

        public static string BattleLogHash(BattleResult result, List<BattleEvent> events)
        {
            return Sha256Hex(CanonicalLog(result, events));
        }

        private static int CompareItems(ItemInstance left, ItemInstance right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            int anchorComparison = left.AnchorSlot.CompareTo(right.AnchorSlot);
            return anchorComparison != 0
                ? anchorComparison
                : string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }

        private static void AppendItem(StringBuilder builder, ItemInstance item)
        {
            if (item == null)
            {
                builder.Append("N;");
                return;
            }

            AppendString(builder, item.InstanceId);
            AppendString(builder, item.DefinitionId);
            AppendInt(builder, item.Quality);
            AppendInt(builder, item.AnchorSlot);
            AppendString(builder, item.AnnotationId);

            var modifiers = item.TemporaryModifiers == null
                ? new List<TemporaryModifier>()
                : new List<TemporaryModifier>(item.TemporaryModifiers);
            modifiers.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                int sourceComparison = string.CompareOrdinal(left.SourceInstanceId, right.SourceInstanceId);
                if (sourceComparison != 0) return sourceComparison;
                int tickComparison = left.RemainingTicks.CompareTo(right.RemainingTicks);
                return tickComparison != 0 ? tickComparison : left.Bps.CompareTo(right.Bps);
            });

            AppendInt(builder, modifiers.Count);
            foreach (TemporaryModifier modifier in modifiers)
            {
                if (modifier == null)
                {
                    builder.Append("N;");
                    continue;
                }
                AppendInt(builder, (int)modifier.Effect);
                AppendString(builder, modifier.SourceInstanceId);
                AppendInt(builder, modifier.RemainingTicks);
                AppendInt(builder, modifier.Bps);
            }
        }

        private static void AppendInt(StringBuilder builder, int value)
        {
            builder.Append('I').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        private static void AppendUlong(StringBuilder builder, ulong value)
        {
            builder.Append('U').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        /// <summary>
        /// 写入“类型标记 + 字符数 + 冒号 + 内容 + 终止符”。
        /// 即使内容本身包含冒号、分号或换行，也能被唯一解析。
        /// </summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            string safeValue = value ?? string.Empty;
            builder.Append('S')
                .Append(safeValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safeValue)
                .Append(';');
        }
    }
}
