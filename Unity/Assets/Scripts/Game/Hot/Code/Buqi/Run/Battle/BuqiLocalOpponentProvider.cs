using System.Collections.Generic;
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
    }
}
