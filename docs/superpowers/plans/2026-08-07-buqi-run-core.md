# Buqi Run Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the pure C# run rules and deterministic day-loop state machine shared by every later Buqi Demo subsystem.

**Architecture:** Add a Unity-independent domain under `Buqi/Run/Core`. The controller owns only phase transitions, counters, terminal checks, revision checks, and idempotency; encounter content, economy mutations, battle simulation, persistence, and UI remain outside this package.

**Tech Stack:** C#, NUnit EditMode tests, Unity GameHot assembly, existing `Game.Hot` namespace.

---

## File Ownership

This worktree may only create or modify:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/*.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs`

Do not modify `BuqiUIDemoController.cs`, `BuqiUIDemoState.cs`, `BuqiRunShellForm.cs`, stage widgets, generated files, prefabs, configuration workbooks, or unrelated dirty files.

## Task 1: Rules and Shared State Contracts

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunRules.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunState.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs`

- [ ] **Step 1: Write failing rules and initial-state tests**

Create the test fixture with these first tests:

```csharp
using NUnit.Framework;

namespace Game.Hot.Tests
{
    public sealed class BuqiRunCoreTests
    {
        [Test]
        public void RulesMatchApprovedDemoContract()
        {
            Assert.That(BuqiRunRules.WinsToVictory, Is.EqualTo(9));
            Assert.That(BuqiRunRules.StartingLives, Is.EqualTo(3));
            Assert.That(BuqiRunRules.EncountersPerDay, Is.EqualTo(3));
            Assert.That(BuqiRunRules.BoardSlotCount, Is.EqualTo(8));
            Assert.That(BuqiRunRules.StorageSlotCount, Is.EqualTo(8));
            Assert.That(BuqiRunRules.StartingCoins, Is.EqualTo(12));
        }

        [Test]
        public void CreateInitialStartsAtFirstEncounterWithEightSlotStorage()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(812345L);

            Assert.That(state.RunSeed, Is.EqualTo(812345L));
            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.EncounterIndex, Is.EqualTo(0));
            Assert.That(state.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(state.Coins, Is.EqualTo(12));
            Assert.That(state.Wins, Is.EqualTo(0));
            Assert.That(state.Lives, Is.EqualTo(3));
            Assert.That(state.BoardInstanceIds, Has.Count.EqualTo(8));
            Assert.That(state.StorageInstanceIds, Has.Count.EqualTo(8));
            Assert.That(state.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(state.Revision, Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: Run the focused test build and confirm failure**

Run:

```powershell
dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal
```

Expected: build fails because `BuqiRunRules`, `BuqiRunState`, and run enums do not exist.

- [ ] **Step 3: Add exact rule and enum contracts**

Create `BuqiRunRules.cs`:

```csharp
namespace Game.Hot
{
    public static class BuqiRunRules
    {
        public const int WinsToVictory = 9;
        public const int StartingLives = 3;
        public const int EncountersPerDay = 3;
        public const int BoardSlotCount = 8;
        public const int StorageSlotCount = 8;
        public const int StartingCoins = 12;
    }
}
```

Create `BuqiRunTypes.cs`:

```csharp
namespace Game.Hot
{
    public enum BuqiRunPhase
    {
        Encounter,
        PveBattle,
        PvpBattle,
        DaySettlement,
        RunTerminal,
    }

    public enum BuqiRunOutcome
    {
        None,
        Victory,
        Defeat,
    }

    public enum BuqiRunBattleKind
    {
        Pve,
        Pvp,
    }

    public enum BuqiRunRawBattleOutcome
    {
        PlayerWin,
        OpponentWin,
        Draw,
    }
}
```

- [ ] **Step 4: Add the versioned mutable state owned by the controller**

Create `BuqiRunState.cs` with this public contract and deep clone:

```csharp
using System.Collections.Generic;

namespace Game.Hot
{
    public sealed class BuqiRunState
    {
        public const string CurrentRuleVersion = "buqi-day-run-v1";

        public string RuleVersion = CurrentRuleVersion;
        public long RunSeed;
        public int RngCursor;
        public int Revision;
        public int Day;
        public int EncounterIndex;
        public BuqiRunPhase Phase;
        public BuqiRunOutcome Outcome;
        public int Coins;
        public int Wins;
        public int Lives;
        public List<string> BoardInstanceIds = new List<string>();
        public List<string> StorageInstanceIds = new List<string>();
        public HashSet<string> AppliedCommandIds = new HashSet<string>();
        public HashSet<string> AppliedSettlementIds = new HashSet<string>();

        public static BuqiRunState CreateInitial(long runSeed)
        {
            return new BuqiRunState
            {
                RunSeed = runSeed,
                Day = 1,
                Phase = BuqiRunPhase.Encounter,
                Coins = BuqiRunRules.StartingCoins,
                Lives = BuqiRunRules.StartingLives,
                BoardInstanceIds = EmptySlots(BuqiRunRules.BoardSlotCount),
                StorageInstanceIds = EmptySlots(BuqiRunRules.StorageSlotCount),
            };
        }

        public BuqiRunState Clone()
        {
            return new BuqiRunState
            {
                RuleVersion = RuleVersion,
                RunSeed = RunSeed,
                RngCursor = RngCursor,
                Revision = Revision,
                Day = Day,
                EncounterIndex = EncounterIndex,
                Phase = Phase,
                Outcome = Outcome,
                Coins = Coins,
                Wins = Wins,
                Lives = Lives,
                BoardInstanceIds = new List<string>(BoardInstanceIds),
                StorageInstanceIds = new List<string>(StorageInstanceIds),
                AppliedCommandIds = new HashSet<string>(AppliedCommandIds),
                AppliedSettlementIds = new HashSet<string>(AppliedSettlementIds),
            };
        }

        private static List<string> EmptySlots(int count)
        {
            var result = new List<string>(count);
            for (int index = 0; index < count; index++)
                result.Add(string.Empty);
            return result;
        }
    }
}
```

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal
```

Expected: build succeeds and `RulesMatchApprovedDemoContract` plus `CreateInitialStartsAtFirstEncounterWithEightSlotStorage` pass when run in Unity EditMode.

## Task 2: Deterministic Day-Loop Transitions

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunTransitionResult.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunController.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs`

- [ ] **Step 1: Add failing phase-transition tests**

Append:

```csharp
[Test]
public void ThreeResolvedEncountersAdvanceToPveThenPvpThenNextDay()
{
    var controller = new BuqiRunController(BuqiRunState.CreateInitial(10));

    Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);
    Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));

    Assert.That(controller.ResolveEncounter("enc-2", 1).Success, Is.True);
    Assert.That(controller.ResolveEncounter("enc-3", 2).Success, Is.True);
    Assert.That(controller.State.EncounterIndex, Is.EqualTo(3));
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));

    Assert.That(controller.SettleBattle("pve-1", 3, BuqiRunBattleKind.Pve,
        BuqiRunRawBattleOutcome.PlayerWin).Success, Is.True);
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PvpBattle));

    Assert.That(controller.SettleBattle("pvp-1", 4, BuqiRunBattleKind.Pvp,
        BuqiRunRawBattleOutcome.OpponentWin).Success, Is.True);
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.DaySettlement));

    Assert.That(controller.CompleteDay("day-1", 5).Success, Is.True);
    Assert.That(controller.State.Day, Is.EqualTo(2));
    Assert.That(controller.State.EncounterIndex, Is.EqualTo(0));
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
}

[Test]
public void InvalidPhaseAndStaleRevisionDoNotMutateState()
{
    var controller = new BuqiRunController(BuqiRunState.CreateInitial(20));

    BuqiRunTransitionResult invalid = controller.CompleteDay("day-early", 0);
    Assert.That(invalid.Success, Is.False);
    Assert.That(controller.State.Revision, Is.EqualTo(0));

    Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);
    BuqiRunTransitionResult stale = controller.ResolveEncounter("enc-stale", 0);
    Assert.That(stale.Success, Is.False);
    Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
    Assert.That(controller.State.Revision, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run tests and confirm missing-controller failure**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`.

Expected: failure for missing `BuqiRunController` and `BuqiRunTransitionResult`.

- [ ] **Step 3: Implement the transition result**

Create `BuqiRunTransitionResult.cs`:

```csharp
namespace Game.Hot
{
    public sealed class BuqiRunTransitionResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunState State;
    }
}
```

- [ ] **Step 4: Implement controller transition methods**

Create `BuqiRunController.cs` with these methods and invariants:

```csharp
using System;

namespace Game.Hot
{
    public sealed class BuqiRunController
    {
        private BuqiRunState m_State;

        public BuqiRunController(BuqiRunState initialState)
        {
            m_State = initialState?.Clone() ?? throw new ArgumentNullException(nameof(initialState));
        }

        public BuqiRunState State => m_State.Clone();

        public BuqiRunTransitionResult ResolveEncounter(string commandId, int expectedRevision)
        {
            if (!ValidateCommand(commandId, expectedRevision, BuqiRunPhase.Encounter, out BuqiRunTransitionResult failure))
                return failure;

            BuqiRunState next = m_State.Clone();
            next.EncounterIndex++;
            if (next.EncounterIndex >= BuqiRunRules.EncountersPerDay)
                next.Phase = BuqiRunPhase.PveBattle;
            ApplyCommand(next, commandId);
            return Commit(next);
        }

        public BuqiRunTransitionResult SettleBattle(
            string settlementId,
            int expectedRevision,
            BuqiRunBattleKind battleKind,
            BuqiRunRawBattleOutcome rawOutcome)
        {
            if (string.IsNullOrEmpty(settlementId))
                return Rejected("Settlement id is required.");
            if (m_State.AppliedSettlementIds.Contains(settlementId))
                return Accepted(replayed: true);
            if (m_State.Revision != expectedRevision)
                return Rejected("State revision mismatch.");
            BuqiRunPhase expectedPhase = battleKind == BuqiRunBattleKind.Pve
                ? BuqiRunPhase.PveBattle
                : BuqiRunPhase.PvpBattle;
            if (m_State.Phase != expectedPhase)
                return Rejected("Battle kind does not match current phase.");

            BuqiRunState next = m_State.Clone();
            bool playerWon = rawOutcome != BuqiRunRawBattleOutcome.OpponentWin;
            if (playerWon)
                next.Wins++;
            else
                next.Lives--;
            next.AppliedSettlementIds.Add(settlementId);

            if (next.Wins >= BuqiRunRules.WinsToVictory)
            {
                next.Wins = BuqiRunRules.WinsToVictory;
                next.Outcome = BuqiRunOutcome.Victory;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else if (next.Lives <= 0)
            {
                next.Lives = 0;
                next.Outcome = BuqiRunOutcome.Defeat;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else
            {
                next.Phase = battleKind == BuqiRunBattleKind.Pve
                    ? BuqiRunPhase.PvpBattle
                    : BuqiRunPhase.DaySettlement;
            }

            next.Revision++;
            m_State = next;
            return Accepted(replayed: false);
        }

        public BuqiRunTransitionResult CompleteDay(string commandId, int expectedRevision)
        {
            if (!ValidateCommand(commandId, expectedRevision, BuqiRunPhase.DaySettlement, out BuqiRunTransitionResult failure))
                return failure;

            BuqiRunState next = m_State.Clone();
            next.Day++;
            next.EncounterIndex = 0;
            next.Phase = BuqiRunPhase.Encounter;
            ApplyCommand(next, commandId);
            return Commit(next);
        }

        private bool ValidateCommand(
            string commandId,
            int expectedRevision,
            BuqiRunPhase requiredPhase,
            out BuqiRunTransitionResult failure)
        {
            failure = null;
            if (string.IsNullOrEmpty(commandId))
                failure = Rejected("Command id is required.");
            else if (m_State.AppliedCommandIds.Contains(commandId))
                failure = Accepted(replayed: true);
            else if (m_State.Revision != expectedRevision)
                failure = Rejected("State revision mismatch.");
            else if (m_State.Phase != requiredPhase)
                failure = Rejected("Command is not valid in the current phase.");
            return failure == null;
        }

        private static void ApplyCommand(BuqiRunState next, string commandId)
        {
            next.AppliedCommandIds.Add(commandId);
            next.Revision++;
        }

        private BuqiRunTransitionResult Commit(BuqiRunState next)
        {
            m_State = next;
            return Accepted(replayed: false);
        }

        private BuqiRunTransitionResult Accepted(bool replayed)
        {
            return new BuqiRunTransitionResult
            {
                Success = true,
                Replayed = replayed,
                State = State,
            };
        }

        private BuqiRunTransitionResult Rejected(string reason)
        {
            return new BuqiRunTransitionResult
            {
                FailureReason = reason,
                State = State,
            };
        }
    }
}
```

- [ ] **Step 5: Run focused tests**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal` and then run `BuqiRunCoreTests` in Unity EditMode.

Expected: build succeeds; all phase-transition tests pass.

## Task 3: Terminal and Idempotency Boundaries

**Files:**

- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunController.cs`

- [ ] **Step 1: Add terminal, draw, and replay tests**

Append tests that construct states directly:

```csharp
[Test]
public void DrawCountsAsPlayerWinAndNineWinsStopsImmediately()
{
    BuqiRunState state = BuqiRunState.CreateInitial(30);
    state.Phase = BuqiRunPhase.PveBattle;
    state.Wins = 8;
    var controller = new BuqiRunController(state);

    BuqiRunTransitionResult result = controller.SettleBattle(
        "draw-terminal", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.Draw);

    Assert.That(result.Success, Is.True);
    Assert.That(controller.State.Wins, Is.EqualTo(9));
    Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Victory));
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
}

[Test]
public void ThirdLossStopsImmediatelyAndSkipsRemainingFlow()
{
    BuqiRunState state = BuqiRunState.CreateInitial(40);
    state.Phase = BuqiRunPhase.PvpBattle;
    state.Lives = 1;
    var controller = new BuqiRunController(state);

    controller.SettleBattle("loss-terminal", 0, BuqiRunBattleKind.Pvp,
        BuqiRunRawBattleOutcome.OpponentWin);

    Assert.That(controller.State.Lives, Is.EqualTo(0));
    Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Defeat));
    Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
}

[Test]
public void RepeatingSettlementIdReturnsOriginalSuccessWithoutDoubleReward()
{
    BuqiRunState state = BuqiRunState.CreateInitial(50);
    state.Phase = BuqiRunPhase.PveBattle;
    var controller = new BuqiRunController(state);

    Assert.That(controller.SettleBattle("same", 0, BuqiRunBattleKind.Pve,
        BuqiRunRawBattleOutcome.PlayerWin).Success, Is.True);
    BuqiRunTransitionResult replay = controller.SettleBattle("same", 0,
        BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin);

    Assert.That(replay.Success, Is.True);
    Assert.That(replay.Replayed, Is.True);
    Assert.That(controller.State.Wins, Is.EqualTo(1));
    Assert.That(controller.State.Revision, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run tests and inspect failures**

Run the Unity EditMode fixture. Expected: any failure must identify a real terminal or replay defect; do not weaken assertions.

- [ ] **Step 3: Fix terminal and replay behavior**

Keep the public signatures from Task 2. Ensure replay detection happens before revision and phase checks, terminal states reject all new command IDs, and a replay never changes `Revision`, `Wins`, `Lives`, `Day`, or `Phase`.

Add this terminal check to `ValidateCommand` after replay detection and before revision validation:

```csharp
else if (m_State.Phase == BuqiRunPhase.RunTerminal)
    failure = Rejected("Run has already ended.");
```

Add the equivalent terminal rejection in `SettleBattle` after settlement replay detection.

- [ ] **Step 4: Run all Buqi EditMode tests**

Run:

```powershell
dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal
```

Then run all tests in `Unity/Assets/Tests/GameHot/Buqi/EditMode` through Unity Test Runner.

Expected: existing tests and all `BuqiRunCoreTests` pass.

- [ ] **Step 5: Commit only owned files**

```powershell
git add -- Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs
git commit -m "feat(buqi): add deterministic day run core"
```

Expected: one commit containing only core contracts, controller, and focused tests.

## Completion Report

Return:

- Commit hash.
- Exact files changed.
- Test commands and pass/fail counts.
- Any deviation from the public signatures in this plan and why it was required.
- Confirmation that no existing UI, generated file, prefab, workbook, or unrelated dirty file was modified.
