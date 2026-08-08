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

            return TrySimulate(
                run,
                kind,
                null,
                playerBuild,
                definitions,
                opponent,
                nextRngCursor,
                out session,
                out error);
        }

        public bool TryGetOrCreatePveSelection(
            BuqiRunState run,
            BuqiPveSelection current,
            BuildSnapshot currentBoard,
            out BuqiPveSelection selection,
            out string error)
        {
            selection = null;
            error = string.Empty;

            if (!TryValidatePveSelectionRequest(run, currentBoard, out error))
                return false;

            if (current != null)
            {
                if (!TryValidateFrozenSelection(run, current, currentBoard, out error))
                    return false;

                selection = current.Clone();
                return true;
            }

            if (!m_OpponentProvider.TryCreatePveChoices(
                    run,
                    out List<BuqiRunOpponent> opponents,
                    out int nextRngCursor,
                    out error))
            {
                return false;
            }

            var created = new BuqiPveSelection
            {
                Day = run.Day,
                SourceRngCursor = run.RngCursor,
                NextRngCursor = nextRngCursor,
                CurrentBoard = BuqiRunBattleSnapshotUtility.CloneBuild(currentBoard),
            };

            for (int index = 0; index < opponents.Count; index++)
            {
                BuqiPveDifficulty difficulty = (BuqiPveDifficulty)index;
                BuqiRunOpponent opponent = opponents[index];
                created.Cards.Add(CreateChoiceCard(run.Day, difficulty, opponent));
            }

            created.SelectionId = CreateSelectionId(run, created);
            selection = created;
            return true;
        }

        public bool TrySelectPveDifficultyAndSimulate(
            BuqiRunState run,
            BuqiPveSelection selection,
            BuqiPveDifficulty difficulty,
            BuildSnapshot currentBoard,
            IItemDefinitionProvider definitions,
            out BuqiRunBattleSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            if (!System.Enum.IsDefined(typeof(BuqiPveDifficulty), difficulty))
            {
                error = "PVE difficulty is invalid";
                return false;
            }

            if (!TryValidatePveSelectionRequest(run, currentBoard, out error))
                return false;
            if (selection == null || !TryValidateFrozenSelection(run, selection, currentBoard, out error))
                return false;

            BuqiPveChoiceCard selected = selection.Cards.Find(card => card.Difficulty == difficulty);
            if (selected == null)
            {
                error = "PVE difficulty is unavailable";
                return false;
            }

            var opponent = new BuqiRunOpponent
            {
                OpponentId = selected.OpponentId,
                DisplayName = selected.OpponentName,
                Source = BuqiRunOpponentSource.PvePreset,
                Build = BuqiRunBattleSnapshotUtility.CloneBuild(selected.OpponentBuild),
            };
            return TrySimulate(
                run,
                BuqiRunBattleKind.Pve,
                difficulty,
                currentBoard,
                definitions,
                opponent,
                selection.NextRngCursor,
                out session,
                out error);
        }

        private static bool TrySimulate(
            BuqiRunState run,
            BuqiRunBattleKind kind,
            BuqiPveDifficulty? pveDifficulty,
            BuildSnapshot playerBuild,
            IItemDefinitionProvider definitions,
            BuqiRunOpponent opponent,
            int nextRngCursor,
            out BuqiRunBattleSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

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
            if (pveDifficulty.HasValue)
                battleIdentity = string.Join(":", battleIdentity, pveDifficulty.Value);
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
                PveDifficulty = pveDifficulty,
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

        private static bool TryValidatePveSelectionRequest(
            BuqiRunState run,
            BuildSnapshot currentBoard,
            out string error)
        {
            if (run == null)
            {
                error = "run is null";
                return false;
            }
            if (run.Phase != BuqiRunPhase.PveBattle)
            {
                error = "phase mismatch";
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
            if (currentBoard == null)
            {
                error = "current board is null";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateFrozenSelection(
            BuqiRunState run,
            BuqiPveSelection selection,
            BuildSnapshot currentBoard,
            out string error)
        {
            if (string.IsNullOrEmpty(selection.SelectionId) ||
                selection.Day != run.Day ||
                selection.SourceRngCursor != run.RngCursor ||
                selection.NextRngCursor < selection.SourceRngCursor)
            {
                error = "PVE selection does not match the current run";
                return false;
            }

            if (selection.CurrentBoard == null ||
                BuqiCrypto.SnapshotHash(selection.CurrentBoard) != BuqiCrypto.SnapshotHash(currentBoard))
            {
                error = "current board changed after PVE choices were frozen";
                return false;
            }

            if (selection.Cards == null || selection.Cards.Count != 3)
            {
                error = "PVE selection must contain three cards";
                return false;
            }

            for (int index = 0; index < selection.Cards.Count; index++)
            {
                BuqiPveChoiceCard card = selection.Cards[index];
                if (card == null ||
                    card.Difficulty != (BuqiPveDifficulty)index ||
                    string.IsNullOrEmpty(card.ChoiceId) ||
                    string.IsNullOrEmpty(card.OpponentId) ||
                    card.OpponentBuild == null ||
                    card.Threat == null ||
                    card.Reward == null)
                {
                    error = "PVE selection card is invalid";
                    return false;
                }
            }

            if (CreateSelectionId(run, selection) != selection.SelectionId)
            {
                error = "PVE selection identity is invalid";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static BuqiPveChoiceCard CreateChoiceCard(
            int day,
            BuqiPveDifficulty difficulty,
            BuqiRunOpponent opponent)
        {
            BuildSnapshot build = BuqiRunBattleSnapshotUtility.CloneBuild(opponent.Build);
            int rank = (int)difficulty + 1;
            return new BuqiPveChoiceCard
            {
                ChoiceId = BuqiText.Format("day-{0}-pve-{1}", day, difficulty.ToString().ToLowerInvariant()),
                Difficulty = difficulty,
                OpponentId = opponent.OpponentId,
                OpponentName = string.IsNullOrEmpty(opponent.DisplayName)
                    ? opponent.OpponentId
                    : opponent.DisplayName,
                OpponentBuild = build,
                Threat = new BuqiPveThreatDto
                {
                    Rank = rank,
                    InitialExecution = build.InitialExecution,
                    InitialBuffer = build.InitialBuffer,
                    InitialNoiseDebt = build.InitialNoiseDebt,
                    EquippedItemCount = build.Items == null ? 0 : build.Items.Count,
                },
                Reward = new BuqiPveRewardDto
                {
                    Rank = rank,
                    VictoryProgress = 1,
                },
            };
        }

        private static string CreateSelectionId(BuqiRunState run, BuqiPveSelection selection)
        {
            var parts = new List<string>
            {
                run.RunSeed.ToString(),
                selection.Day.ToString(),
                selection.SourceRngCursor.ToString(),
                selection.NextRngCursor.ToString(),
                BuqiCrypto.SnapshotHash(selection.CurrentBoard),
            };
            if (selection.Cards != null)
            {
                foreach (BuqiPveChoiceCard card in selection.Cards)
                {
                    if (card == null)
                    {
                        parts.Add("null");
                        continue;
                    }
                    parts.Add(card.ChoiceId ?? string.Empty);
                    parts.Add(((int)card.Difficulty).ToString());
                    parts.Add(card.OpponentId ?? string.Empty);
                    parts.Add(card.OpponentName ?? string.Empty);
                    parts.Add(BuqiCrypto.SnapshotHash(card.OpponentBuild));
                    parts.Add(card.Threat.Rank.ToString());
                    parts.Add(card.Threat.InitialExecution.ToString());
                    parts.Add(card.Threat.InitialBuffer.ToString());
                    parts.Add(card.Threat.InitialNoiseDebt.ToString());
                    parts.Add(card.Threat.EquippedItemCount.ToString());
                    parts.Add(card.Reward.Rank.ToString());
                    parts.Add(card.Reward.VictoryProgress.ToString());
                }
            }
            return BuqiCrypto.Sha256Hex(string.Join(":", parts));
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
