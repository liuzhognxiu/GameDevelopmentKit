using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Battle
{
    public sealed class BuqiLocalOpponentProvider
    {
        private readonly BuqiLocalOpponentPool m_Pool;

        public BuqiLocalOpponentProvider(BuqiLocalOpponentPool pool)
        {
            m_Pool = pool ?? new BuqiLocalOpponentPool();
        }

        public bool TrySelect(
            BuqiRunState run,
            BuqiRunBattleKind kind,
            out BuqiRunOpponent opponent,
            out int nextRngCursor,
            out string error)
        {
            opponent = null;
            nextRngCursor = run == null ? 0 : run.RngCursor;
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

            if (run.RngCursor < 0)
            {
                error = "rng cursor is invalid";
                return false;
            }

            if (!MatchesPhase(run.Phase, kind))
            {
                error = "phase mismatch";
                return false;
            }

            List<BuqiRunOpponent> source = kind == BuqiRunBattleKind.Pve ? m_Pool.Pve : m_Pool.Pvp;
            if (source == null || source.Count == 0)
            {
                error = "pool is empty";
                return false;
            }

            if (!ValidatePool(source, kind, out error))
                return false;

            int cursor = run.RngCursor;
            int index = BuqiRunRandom.Next(run.RunSeed, ref cursor, source.Count);
            opponent = BuqiRunBattleSnapshotUtility.CloneOpponent(source[index]);
            nextRngCursor = cursor;
            return true;
        }

        public bool TryCreatePveChoices(
            BuqiRunState run,
            out List<BuqiRunOpponent> opponents,
            out int nextRngCursor,
            out string error)
        {
            opponents = null;
            nextRngCursor = run == null ? 0 : run.RngCursor;
            error = string.Empty;

            if (run == null)
            {
                error = "run is null";
                return false;
            }

            if (run.RngCursor < 0)
            {
                error = "rng cursor is invalid";
                return false;
            }

            if (run.Phase != BuqiRunPhase.PveBattle)
            {
                error = "phase mismatch";
                return false;
            }

            if (m_Pool.Pve == null || m_Pool.Pve.Count < 3)
            {
                error = "PVE pool requires at least three opponents";
                return false;
            }

            if (!ValidatePool(m_Pool.Pve, BuqiRunBattleKind.Pve, out error))
                return false;

            var ranked = new List<BuqiRunOpponent>(m_Pool.Pve);
            ranked.Sort(CompareThreat);
            int cursor = run.RngCursor;
            opponents = new List<BuqiRunOpponent>(3);
            for (int difficultyIndex = 0; difficultyIndex < 3; difficultyIndex++)
            {
                int start = (ranked.Count * difficultyIndex) / 3;
                int end = (ranked.Count * (difficultyIndex + 1)) / 3;
                int selectedIndex = start + BuqiRunRandom.Next(run.RunSeed, ref cursor, end - start);
                opponents.Add(BuqiRunBattleSnapshotUtility.CloneOpponent(ranked[selectedIndex]));
            }

            nextRngCursor = cursor;
            return true;
        }

        private static bool IsDefinedKind(BuqiRunBattleKind kind)
        {
            return kind == BuqiRunBattleKind.Pve || kind == BuqiRunBattleKind.Pvp;
        }

        private static bool MatchesPhase(BuqiRunPhase phase, BuqiRunBattleKind kind)
        {
            return kind == BuqiRunBattleKind.Pve
                ? phase == BuqiRunPhase.PveBattle
                : phase == BuqiRunPhase.PvpBattle;
        }

        private static bool ValidatePool(
            List<BuqiRunOpponent> source,
            BuqiRunBattleKind kind,
            out string error)
        {
            BuqiRunOpponentSource expectedSource =
                kind == BuqiRunBattleKind.Pve
                    ? BuqiRunOpponentSource.PvePreset
                    : BuqiRunOpponentSource.LocalPlayerPreset;

            for (int index = 0; index < source.Count; index++)
            {
                BuqiRunOpponent entry = source[index];
                if (entry == null ||
                    entry.Build == null ||
                    string.IsNullOrEmpty(entry.OpponentId) ||
                    entry.Source != expectedSource)
                {
                    error = "invalid pool entry";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static int CompareThreat(BuqiRunOpponent left, BuqiRunOpponent right)
        {
            int scoreComparison = ThreatScore(left).CompareTo(ThreatScore(right));
            if (scoreComparison != 0)
                return scoreComparison;
            return string.CompareOrdinal(left.OpponentId, right.OpponentId);
        }

        private static int ThreatScore(BuqiRunOpponent opponent)
        {
            BuildSnapshot build = opponent.Build;
            int itemCount = build.Items == null ? 0 : build.Items.Count;
            return build.InitialExecution + build.InitialBuffer - build.InitialNoiseDebt + (itemCount * 10);
        }
    }
}
