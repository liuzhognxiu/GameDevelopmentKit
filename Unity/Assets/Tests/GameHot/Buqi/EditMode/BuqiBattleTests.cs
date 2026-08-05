using System;
using System.Collections.Generic;
using System.IO;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    /// <summary>
    /// 在 Unity Editor 中复用与无头端相同的战斗源码、夹具和行为断言，防止程序集或运行时差异导致结果漂移。
    /// </summary>
    public sealed class BuqiBattleTests
    {
        /// <summary>验证战斗契约 v0.4 的独立行为断言全部通过。</summary>
        [Test]
        public void BattleContract_AllChecksPass()
        {
            List<string> failures = BuqiContractChecks.RunAll();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>
        /// 在 Unity 端读取仓库内同一份 approved hash，只比较不更新；基线更新必须由人工审阅后显式执行无头模式。
        /// </summary>
        [Test]
        public void ApprovedHashes_MatchCurrentContractVectors()
        {
            string approvedPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets", "Tests", "GameHot", "Buqi", "EditMode", "Vectors",
                "approved-buqi-hashes.json");
            Assert.That(File.Exists(approvedPath), Is.True,
                BuqiText.Format("Approved hash file is missing: {0}", approvedPath));

            Dictionary<string, string> approved = ParseHashes(File.ReadAllText(approvedPath));
            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            List<BuqiTestVector> vectors = BuqiTestSuite.CreateVectors();
            Assert.That(approved.Count, Is.EqualTo(vectors.Count), "Approved hash vector count changed");

            foreach (BuqiTestVector vector in vectors)
            {
                BattleResult result = BuqiBattleSimulator.Simulate(
                    vector.Request, provider, out _, out _, out _);
                Assert.That(approved.TryGetValue(vector.Id, out string expected), Is.True,
                    BuqiText.Format("Approved hash is missing vector {0}", vector.Id));
                Assert.That(result.BattleLogHash, Is.EqualTo(expected),
                    BuqiText.Format("Battle hash changed for vector {0}", vector.Id));
            }
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
