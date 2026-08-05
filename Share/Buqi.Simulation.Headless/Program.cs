using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Hot.Buqi.Battle;

namespace Buqi.Simulation.Headless
{
    /// <summary>
    /// 《不器》纯 C# 战斗内核的 .NET 8 无头验证入口。
    /// 任一契约、批准哈希、压力或参数检查失败都返回非零退出码，便于 CI 和本地脚本阻断错误提交。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 支持 verify、stress、all 与显式 update-hashes；stress 不读取批准基线，其余普通验证永远不会修改批准基线。
        /// </summary>
        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "verify";
            int stressCount = 10000;
            if (args.Length > 1 && !int.TryParse(args[1], out stressCount))
            {
                Console.Error.WriteLine("stress count must be an integer");
                return 2;
            }

            if (mode != "verify" && mode != "stress" && mode != "all" && mode != "update-hashes")
            {
                Console.Error.WriteLine("usage: verify | stress [count] | all [count] | update-hashes");
                return 2;
            }

            Console.WriteLine(BuqiText.Format(
                "=== Buqi Battle Headless Validator ({0}) ===",
                BuqiBattleSimulator.SimulationVersion));
            // approved hash 只证明输出没有漂移；先跑独立行为断言，避免错误逻辑通过更新哈希被自我批准。
            List<string> failures = BuqiContractChecks.RunAll();
            foreach (string failure in failures)
                Console.Error.WriteLine(BuqiText.Format("[contract-fail] {0}", failure));
            if (failures.Count > 0)
            {
                Console.Error.WriteLine(BuqiText.Format("=== CONTRACT CHECKS FAILED: {0} ===", failures.Count));
                return 1;
            }
            Console.WriteLine("[contract] all behavioral checks passed");

            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            if (mode == "stress")
            {
                if (!RunStress(provider, stressCount))
                    return 1;
                Console.WriteLine("=== ALL CHECKS PASSED ===");
                return 0;
            }

            List<BuqiTestVector> vectors = BuqiTestSuite.CreateVectors();
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (BuqiTestVector vector in vectors)
            {
                BattleResult result = BuqiBattleSimulator.Simulate(
                    vector.Request, provider, out List<BattleEvent> log, out _, out _);
                hashes[vector.Id] = result.BattleLogHash;
                Console.WriteLine(BuqiText.Format(
                    "[{0}] outcome={1} duration={2} events={3} hash={4}",
                    vector.Id,
                    result.Outcome,
                    result.DurationTicks,
                    log.Count,
                    result.BattleLogHash));
            }

            string approvedPath = GetApprovedHashPath();
            if (mode == "update-hashes")
            {
                // 只有显式模式会写盘；verify 与 all 始终只读批准基线，stress 不依赖批准基线。
                Directory.CreateDirectory(Path.GetDirectoryName(approvedPath));
                File.WriteAllText(approvedPath, SerializeHashes(hashes), new UTF8Encoding(false));
                Console.WriteLine(BuqiText.Format("[approved] explicitly updated: {0}", approvedPath));
                return 0;
            }

            int hashFailures = VerifyApprovedHashes(approvedPath, hashes);
            if (hashFailures > 0)
                return 1;

            if (mode == "all" && !RunStress(provider, stressCount))
                return 1;

            Console.WriteLine("=== ALL CHECKS PASSED ===");
            return 0;
        }

        /// <summary>
        /// 验证构筑数量、快照唯一性、棋盘合法性和硬上限终止性；不将随机对局结果解释为平衡数据。
        /// </summary>
        private static bool RunStress(IItemDefinitionProvider provider, int count)
        {
            List<BuildSnapshot> builds = BuqiTestSuite.GenerateStressBuilds(count, 12345);
            int invalidCount = 0;
            var distinctHashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuildSnapshot build in builds)
            {
                distinctHashes.Add(BuqiCrypto.SnapshotHash(build));
                if (!BuqiBoardValidator.Validate(build, provider, out _))
                    invalidCount++;
            }

            int hungCount = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int index = 0; index < builds.Count; index++)
            {
                BattleRequest request = BuqiTestSuite.Request(
                    builds[index], builds[(index + 1) % builds.Count]);
                BattleResult result = BuqiBattleSimulator.Simulate(
                    request, provider, out _, out _, out _);
                if (result.DurationTicks > BuqiBattleSimulator.HardCapTick + 1)
                    hungCount++;
            }
            stopwatch.Stop();

            Console.WriteLine(BuqiText.Format(
                "[stress] builds={0} distinct={1} invalid={2} hung={3} elapsedMs={4}",
                builds.Count,
                distinctHashes.Count,
                invalidCount,
                hungCount,
                stopwatch.ElapsedMilliseconds));
            return builds.Count == count &&
                   distinctHashes.Count == count &&
                   invalidCount == 0 &&
                   hungCount == 0;
        }

        private static int VerifyApprovedHashes(
            string approvedPath,
            SortedDictionary<string, string> current)
        {
            if (!File.Exists(approvedPath))
            {
                Console.Error.WriteLine("[approved] baseline missing; run update-hashes only after contract review");
                return 1;
            }

            Dictionary<string, string> approved = ParseHashes(File.ReadAllText(approvedPath));
            int failures = 0;
            foreach (KeyValuePair<string, string> pair in current)
            {
                if (!approved.TryGetValue(pair.Key, out string expected))
                {
                    Console.Error.WriteLine(BuqiText.Format("[approved] missing vector {0}", pair.Key));
                    failures++;
                }
                else if (!string.Equals(expected, pair.Value, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(BuqiText.Format(
                        "[approved] mismatch {0} expected={1} actual={2}",
                        pair.Key,
                        expected,
                        pair.Value));
                    failures++;
                }
            }
            foreach (string id in approved.Keys)
            {
                if (!current.ContainsKey(id))
                {
                    Console.Error.WriteLine(BuqiText.Format("[approved] stale vector {0}", id));
                    failures++;
                }
            }
            if (failures == 0)
                Console.WriteLine("[approved] all hashes match");
            return failures;
        }

        private static string GetApprovedHashPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "Unity", "Assets", "Tests", "GameHot", "Buqi", "EditMode", "Vectors",
                "approved-buqi-hashes.json"));
        }

        private static string SerializeHashes(SortedDictionary<string, string> hashes)
        {
            var builder = new StringBuilder();
            builder.Append("{\n");
            int index = 0;
            foreach (KeyValuePair<string, string> pair in hashes)
            {
                if (index++ > 0)
                    builder.Append(",\n");
                builder.Append("  \"").Append(pair.Key).Append("\": \"")
                    .Append(pair.Value).Append('"');
            }
            builder.Append("\n}\n");
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseHashes(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim().TrimEnd(',');
                if (line == "{" || line == "}")
                    continue;
                int separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;
                string key = line.Substring(0, separator).Trim().Trim('"');
                string value = line.Substring(separator + 1).Trim().Trim('"');
                result[key] = value;
            }
            return result;
        }
    }
}
