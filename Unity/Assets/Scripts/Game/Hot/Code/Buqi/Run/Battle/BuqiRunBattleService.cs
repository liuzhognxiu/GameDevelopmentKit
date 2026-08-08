using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Battle
{
    public sealed class BuqiRunBattleService
    {
        private readonly BuqiLocalOpponentProvider m_OpponentProvider;

        public BuqiRunBattleService(BuqiLocalOpponentProvider opponentProvider)
        {
            m_OpponentProvider = opponentProvider ?? new BuqiLocalOpponentProvider(new BuqiLocalOpponentPool());
        }

        public bool TryCreateAndSimulate(
            BuqiRunState run,
            BuqiRunBattleKind kind,
            BuildSnapshot playerBuild,
            IItemDefinitionProvider definitions,
            out BuqiRunBattleSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            if (run == null)
            {
                error = "run is null";
                return false;
            }

            if (!IsDefinedKind(kind))
            {
                error = "battle kind is invalid";
                return false;
            }

            if (run.Day < 1)
            {
                error = "day is invalid";
                return false;
            }

            if (run.RngCursor < 0)
            {
                error = "rng cursor is invalid";
                return false;
            }

            if (playerBuild == null)
            {
                error = "player build is null";
                return false;
            }

            if (definitions == null)
            {
                error = "definitions are null";
                return false;
            }

            if (!m_OpponentProvider.TrySelect(run, kind, out BuqiRunOpponent opponent, out int nextRngCursor, out error))
                return false;

            if (!BuqiBoardValidator.Validate(playerBuild, definitions, out List<string> playerErrors))
            {
                error = BuqiText.Format("player build invalid: {0}", string.Join("; ", playerErrors));
                return false;
            }

            if (!BuqiBoardValidator.Validate(opponent.Build, definitions, out List<string> opponentErrors))
            {
                error = BuqiText.Format("opponent build invalid: {0}", string.Join("; ", opponentErrors));
                return false;
            }

            int roundIndex = CreateRoundIndex(run.Day, kind);
            ulong battleSeed = CreateBattleSeed(run.RunSeed, roundIndex, nextRngCursor, kind);
            var request = new BattleRequest
            {
                RuleVersion = BuqiBattleSimulator.RuleVersion,
                BattleSeed = battleSeed,
                RoundIndex = roundIndex,
                Left = playerBuild,
                Right = opponent.Build,
            };

            BattleResult result = BuqiBattleSimulator.Simulate(
                request,
                definitions,
                out List<BattleEvent> log,
                out _,
                out _);
            if (result == null)
            {
                error = "simulator returned null result";
                return false;
            }

            if (result.Outcome == BattleOutcome.InvalidBuild)
            {
                error = "simulator rejected the battle request";
                return false;
            }

            if (result.Outcome == BattleOutcome.Aborted)
            {
                error = "simulator aborted the battle request";
                return false;
            }

            List<BattleEvent> safeLog = log ?? new List<BattleEvent>();
            string leftSnapshotHash = BuqiCrypto.SnapshotHash(request.Left);
            string rightSnapshotHash = BuqiCrypto.SnapshotHash(request.Right);
            string battleIdentity = string.Join(
                ":",
                kind,
                roundIndex,
                battleSeed,
                leftSnapshotHash,
                rightSnapshotHash);
            string battleId = BuqiCrypto.Sha256Hex(battleIdentity);

            var replay = new BattleReplayData
            {
                Title = BuqiText.Format("{0} Battle", kind),
                LeftName = ResolvePlayerName(playerBuild),
                RightName = ResolveOpponentName(opponent),
                LeftBuild = playerBuild,
                RightBuild = request.Right,
                Result = result,
                Log = safeLog,
                Definitions = definitions,
            };

            session = new BuqiRunBattleSession
            {
                BattleId = battleId,
                Kind = kind,
                OpponentId = opponent.OpponentId,
                NextRngCursor = nextRngCursor,
                Request = request,
                Result = result,
                Log = safeLog,
                Replay = replay,
                RawOutcome = MapOutcome(result.Outcome),
            };
            return true;
        }

        private static int CreateRoundIndex(int day, BuqiRunBattleKind kind)
        {
            int safeDay = day;
            int offset = kind == BuqiRunBattleKind.Pve ? 1 : 2;
            return ((safeDay - 1) * 2) + offset;
        }

        private static ulong CreateBattleSeed(long runSeed, int roundIndex, int nextRngCursor, BuqiRunBattleKind kind)
        {
            ulong seed = unchecked((ulong)runSeed);
            ulong roundBits = unchecked((ulong)(uint)roundIndex) << 32;
            ulong cursorBits = unchecked((ulong)(uint)nextRngCursor);
            ulong kindBits = kind == BuqiRunBattleKind.Pve ? 0x505645UL : 0x505650UL;
            return seed ^ roundBits ^ cursorBits ^ kindBits;
        }

        private static string ResolvePlayerName(BuildSnapshot playerBuild)
        {
            return string.IsNullOrEmpty(playerBuild.SnapshotId) ? "Player" : playerBuild.SnapshotId;
        }

        private static string ResolveOpponentName(BuqiRunOpponent opponent)
        {
            if (string.IsNullOrEmpty(opponent.DisplayName))
                return opponent.OpponentId;
            return opponent.DisplayName;
        }

        private static BuqiRunRawBattleOutcome MapOutcome(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.LeftWin)
                return BuqiRunRawBattleOutcome.PlayerWin;
            if (outcome == BattleOutcome.RightWin)
                return BuqiRunRawBattleOutcome.OpponentWin;
            if (outcome == BattleOutcome.Draw)
                return BuqiRunRawBattleOutcome.Draw;
            throw new System.ArgumentOutOfRangeException(nameof(outcome), outcome, "Unexpected battle outcome.");
        }

        private static bool IsDefinedKind(BuqiRunBattleKind kind)
        {
            return kind == BuqiRunBattleKind.Pve || kind == BuqiRunBattleKind.Pvp;
        }
    }
}
