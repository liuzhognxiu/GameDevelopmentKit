using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Integration
{
    public enum BuqiRunRouteNodeKind
    {
        Bazaar = 0,
        Event = 1,
        Training = 2,
        PveBattle = 3,
        PvpBattle = 4,
    }

    [Serializable]
    public sealed class BuqiRunRouteNode
    {
        public string NodeId = string.Empty;
        public BuqiRunRouteNodeKind Kind;
        public string Title = string.Empty;
        public string Benefit = string.Empty;
        public string Cost = string.Empty;
        public string Condition = string.Empty;
        public bool Available = true;

        public BuqiRunRouteNode Clone()
        {
            return (BuqiRunRouteNode)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunRouteState
    {
        public string RouteId = string.Empty;
        public long RunSeed;
        public int Day;
        public BuqiRunPeriod Period;
        public int Revision;
        public List<BuqiRunRouteNode> Nodes = new List<BuqiRunRouteNode>();
        public string SelectedNodeId = string.Empty;
        public string AppliedCommandId = string.Empty;

        public BuqiRunRouteState Clone()
        {
            var clone = new BuqiRunRouteState
            {
                RouteId = RouteId,
                RunSeed = RunSeed,
                Day = Day,
                Period = Period,
                Revision = Revision,
                SelectedNodeId = SelectedNodeId,
                AppliedCommandId = AppliedCommandId,
            };
            for (int index = 0; index < Nodes.Count; index++)
                clone.Nodes.Add(Nodes[index].Clone());
            return clone;
        }
    }

    public sealed class BuqiRunRouteResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunRouteState State = null!;
    }

    public sealed class BuqiRunRouteService
    {
        public BuqiRunRouteState Open(BuqiRunState run, int candidateCount = 3)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            if (candidateCount < 2 || candidateCount > 3)
                throw new ArgumentOutOfRangeException(nameof(candidateCount));

            var state = new BuqiRunRouteState
            {
                RouteId = BuqiText.Format(
                    "route:{0}:{1}:{2}:{3}",
                    run.RunSeed,
                    run.Day,
                    (int)run.Period,
                    run.Revision),
                RunSeed = run.RunSeed,
                Day = run.Day,
                Period = run.Period,
                Revision = run.Revision,
            };

            IReadOnlyList<BuqiRunRouteNode> configured = CreateConfiguredNodes(run.Period);
            int count = Math.Min(candidateCount, configured.Count);
            for (int index = 0; index < count; index++)
                state.Nodes.Add(configured[index].Clone());
            return state;
        }

        public BuqiRunRouteResult Select(BuqiRunRouteState source, string nodeId, string commandId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            BuqiRunRouteState unchanged = source.Clone();
            if (string.IsNullOrWhiteSpace(commandId))
                return Failure(unchanged, "Route command id is required.");
            if (!string.IsNullOrEmpty(source.SelectedNodeId))
            {
                bool replayed = string.Equals(source.SelectedNodeId, nodeId, StringComparison.Ordinal) &&
                                string.Equals(source.AppliedCommandId, commandId, StringComparison.Ordinal);
                return replayed
                    ? Success(unchanged, true)
                    : Failure(unchanged, "The route has already been selected.");
            }

            BuqiRunRouteNode selected = null;
            for (int index = 0; index < source.Nodes.Count; index++)
            {
                if (string.Equals(source.Nodes[index].NodeId, nodeId, StringComparison.Ordinal))
                {
                    selected = source.Nodes[index];
                    break;
                }
            }
            if (selected == null)
                return Failure(unchanged, "Route node is unavailable.");
            if (!selected.Available)
                return Failure(unchanged, "Route node conditions are not satisfied.");

            unchanged.SelectedNodeId = selected.NodeId;
            unchanged.AppliedCommandId = commandId;
            return Success(unchanged, false);
        }

        private static IReadOnlyList<BuqiRunRouteNode> CreateConfiguredNodes(BuqiRunPeriod period)
        {
            if (period == BuqiRunPeriod.Hour3Pve)
            {
                return new[]
                {
                    Node("battle-initial", BuqiRunRouteNodeKind.PveBattle, "初阶试炼", "稳定战斗奖励", "奖励档位较低", "始终可选"),
                    Node("battle-intermediate", BuqiRunRouteNodeKind.PveBattle, "进阶试炼", "战斗奖励提升", "对手强度提升", "始终可选"),
                    Node("battle-final", BuqiRunRouteNodeKind.PveBattle, "险阶试炼", "最高档战斗奖励", "对手强度最高", "始终可选"),
                };
            }
            if (period == BuqiRunPeriod.Hour6Pvp)
            {
                return new[]
                {
                    Node("pvp-scouted", BuqiRunRouteNodeKind.PvpBattle, "已侦察道影", "对手装备已知", "标准奖励", "始终可选"),
                    Node("pvp-unknown", BuqiRunRouteNodeKind.PvpBattle, "未知道影", "奖励提升", "对手详情隐藏", "始终可选"),
                };
            }

            return new[]
            {
                Node("bazaar", BuqiRunRouteNodeKind.Bazaar, "坊市", "购买、出售并整理器物", "消耗金币与栏位空间", "始终可选"),
                Node("event", BuqiRunRouteNodeKind.Event, "际遇", "获得配置事件结果", "代价随选择变化", "需满足选项条件"),
                Node("training", BuqiRunRouteNodeKind.Training, "训练", "升级或强化已有器物", "消耗配置训练费用", "需要符合条件的目标"),
            };
        }

        private static BuqiRunRouteNode Node(
            string id,
            BuqiRunRouteNodeKind kind,
            string title,
            string benefit,
            string cost,
            string condition)
        {
            return new BuqiRunRouteNode
            {
                NodeId = id,
                Kind = kind,
                Title = title,
                Benefit = benefit,
                Cost = cost,
                Condition = condition,
            };
        }

        private static BuqiRunRouteResult Success(BuqiRunRouteState state, bool replayed)
        {
            return new BuqiRunRouteResult { Success = true, Replayed = replayed, State = state };
        }

        private static BuqiRunRouteResult Failure(BuqiRunRouteState state, string reason)
        {
            return new BuqiRunRouteResult { Success = false, FailureReason = reason, State = state };
        }
    }
}
