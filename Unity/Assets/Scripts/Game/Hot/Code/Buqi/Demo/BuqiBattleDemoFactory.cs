using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Demo
{
    public static class BuqiBattleDemoFactory
    {
        private const ulong DemoBattleSeed = 20260806UL;

        public static bool TryCreate(
            BuqiConfigCatalog catalog,
            out BattleReplayData data,
            out string error)
        {
            data = null;
            error = string.Empty;
            if (catalog == null || catalog.Global == null)
            {
                error = "不器配置目录不可用。";
                return false;
            }

            if (catalog.Echoes == null || catalog.Echoes.Count < 2)
            {
                error = "演示战斗至少需要两个对手快照。";
                return false;
            }

            var definitions = new BuqiDefinitionProvider(catalog);
            List<BuqiEchoConfigRow> echoes = FindLegalEchoes(catalog.Echoes, definitions);
            if (echoes.Count < 2)
            {
                error = "演示战斗至少需要两个合法的对手快照。";
                return false;
            }

            BuqiEchoConfigRow leftEcho = echoes[0];
            BuqiEchoConfigRow rightEcho = echoes[1];
            BuildSnapshot leftBuild = CopySnapshot(leftEcho.Snapshot, definitions.ContentVersion, "L-");
            BuildSnapshot rightBuild = CopySnapshot(rightEcho.Snapshot, definitions.ContentVersion, "R-");
            var request = new BattleRequest
            {
                RuleVersion = BuqiBattleSimulator.RuleVersion,
                BattleSeed = DemoBattleSeed,
                RoundIndex = 1,
                Left = leftBuild,
                Right = rightBuild,
            };

            BattleResult result = BuqiBattleSimulator.Simulate(
                request,
                definitions,
                out List<BattleEvent> log,
                out _,
                out _);
            if (result == null || result.Outcome == BattleOutcome.InvalidBuild)
            {
                error = "演示战斗快照未通过战斗校验。";
                return false;
            }

            var replay = new BattleReplayData
            {
                Title = BuqiText.Format(
                    "{0} 对阵 {1}",
                    DisplayName(leftEcho),
                    DisplayName(rightEcho)),
                LeftName = DisplayName(leftEcho),
                RightName = DisplayName(rightEcho),
                LeftBuild = leftBuild,
                RightBuild = rightBuild,
                Result = result,
                Log = log,
                Definitions = definitions,
            };

            try
            {
                var controller = new BattleReplayController(replay);
                controller.SkipToEnd();
                if (!string.IsNullOrEmpty(controller.Frame.Error))
                {
                    error = controller.Frame.Error;
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = BuqiText.Format("演示回放校验失败：{0}", exception.Message);
                return false;
            }

            data = replay;
            return true;
        }

        private static List<BuqiEchoConfigRow> FindLegalEchoes(
            List<BuqiEchoConfigRow> source,
            IItemDefinitionProvider definitions)
        {
            var sorted = new List<BuqiEchoConfigRow>();
            foreach (BuqiEchoConfigRow echo in source)
            {
                if (echo != null && echo.Snapshot != null)
                    sorted.Add(echo);
            }
            sorted.Sort((left, right) => string.Compare(
                left.EchoId,
                right.EchoId,
                StringComparison.Ordinal));

            var legal = new List<BuqiEchoConfigRow>(2);
            foreach (BuqiEchoConfigRow echo in sorted)
            {
                BuildSnapshot snapshot = CopySnapshot(echo.Snapshot, definitions.ContentVersion, "V-");
                if (!BuqiBoardValidator.Validate(snapshot, definitions, out _))
                    continue;
                legal.Add(echo);
                if (legal.Count == 2)
                    break;
            }
            return legal;
        }

        private static BuildSnapshot CopySnapshot(
            BuqiBuildSnapshotConfigRow source,
            string contentVersion,
            string instancePrefix)
        {
            var snapshot = new BuildSnapshot
            {
                SnapshotId = BuqiText.Format("{0}{1}", instancePrefix, source.SnapshotId),
                ContentVersion = contentVersion,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };
            foreach (BuqiItemInstanceConfigRow item in source.Items)
            {
                if (item == null)
                    continue;
                snapshot.Items.Add(new ItemInstance
                {
                    InstanceId = BuqiText.Format("{0}{1}", instancePrefix, item.InstanceId),
                    DefinitionId = item.DefinitionId,
                    Quality = (int)item.Quality,
                    AnchorSlot = item.AnchorSlot,
                    AnnotationId = item.RefinementId,
                });
            }
            return snapshot;
        }

        private static string DisplayName(BuqiEchoConfigRow echo)
        {
            return string.IsNullOrEmpty(echo.DisplayName) ? echo.EchoId : echo.DisplayName;
        }
    }
}
