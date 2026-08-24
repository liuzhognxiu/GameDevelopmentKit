# Buqi Infinite Run Core and Save Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the nine-day/eight-slot run core with the approved unlimited-day, ten-win, ten-slot run and a deliberately incompatible save schema.

**Architecture:** Keep the existing six-period state machine and settlement coordinator, but replace day-based termination with victory/life state transitions. Put realm math in a pure progression helper, keep old `Lives`/`StartingLives` members as temporary source-compatible aliases, enforce the one-time `HeartTrialUsed` heart trial, and make save v5 the only accepted schema so the existing orchestrator recovery path deletes incompatible saves and starts a clean run.

**Tech Stack:** Unity 6000.3, C# GameHot hot-reload assembly, NUnit EditMode tests, Luban-backed runtime content, UGF save integration.

---

## File Structure

- Extend `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunRules.cs` with the pure `BuqiRunProgression` helper so the ignored Unity-generated project file does not need manual edits.
- Modify `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs`: add new-baseline contract cases before replacing its old nine-day expectations.
- Modify `BuqiRunRules.cs`: new constants, ten-slot capacity, and finite content-schedule clamp.
- Modify `BuqiRunState.cs`: hero, cultivation, realm, life-pool, and one-time heart-trial state (`InTribulationTrial` plus `HeartTrialUsed`).
- Modify `BuqiRunController.cs`: battle rewards, day-scaled life loss, heart trial, and nine-win tribulation entry.
- Modify `BuqiRunSaveData.cs` and `BuqiRunSaveCodec.cs`: save v5 schema, validation, and old-save rejection.
- Modify `BuqiRunDemoIntegration.cs`: keep unsupported-save automatic recovery and project new fields into the demo state.
- Modify `BuqiBazaarSupplyViewSource.cs` and `BuqiSupplyIntegration.cs`: clamp unlimited run days to the last configured content schedule instead of rejecting days after nine.
- Update affected EditMode tests in place, preserving unrelated uncommitted changes.

### Task 1: Lock the New Run Contract with Failing Tests

**Files:**
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs`

- [ ] **Step 1: Add the new failing contract tests**

```csharp
[Test]
public void InitialState_UsesApprovedUnlimitedRunBaseline()
{
    BuqiRunState state = BuqiRunState.CreateInitial(240824L, "content-v1");

    Assert.That(BuqiRunRules.WinsToVictory, Is.EqualTo(10));
    Assert.That(BuqiRunRules.StartingLifePool, Is.EqualTo(20));
    Assert.That(BuqiRunRules.BoardSlotCount, Is.EqualTo(10));
    Assert.That(BuqiRunRules.StorageSlotCount, Is.EqualTo(10));
    Assert.That(state.Day, Is.EqualTo(1));
    Assert.That(state.LifePool, Is.EqualTo(20));
    Assert.That(state.Cultivation, Is.Zero);
    Assert.That(state.Realm, Is.Zero);
    Assert.That(state.InTribulationTrial, Is.False);
    Assert.That(state.BoardInstanceIds, Has.Count.EqualTo(10));
    Assert.That(state.StorageInstanceIds, Has.Count.EqualTo(10));
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore`

Expected: FAIL because `StartingLifePool`, `LifePool`, `Cultivation`, `Realm`, and `InTribulationTrial` do not exist.

- [ ] **Step 3: Commit the failing contract**

```powershell
git add Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs
git commit -m "test(buqi): lock infinite run baseline"
```

### Task 2: Add Rules, State, and Realm Progression

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunRules.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunState.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs`

- [ ] **Step 1: Add failing realm/reward tests**

```csharp
[TestCase(0, 0)]
[TestCase(7, 0)]
[TestCase(8, 1)]
[TestCase(119, 7)]
[TestCase(120, 8)]
public void RealmProgression_UsesApprovedThresholds(int cultivation, int expectedRealm)
{
    Assert.That(BuqiRunProgression.GetRealm(cultivation), Is.EqualTo(expectedRealm));
}

[TestCase(BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin, 3)]
[TestCase(BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.OpponentWin, 1)]
[TestCase(BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.PlayerWin, 2)]
[TestCase(BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin, 1)]
public void BattleCultivationReward_IsExplicit(
    BuqiRunBattleKind kind,
    BuqiRunRawBattleOutcome outcome,
    int expected)
{
    Assert.That(BuqiRunProgression.GetBattleReward(kind, outcome), Is.EqualTo(expected));
}
```

- [ ] **Step 2: Run the build and verify RED**

Run: `dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore`

Expected: FAIL because `BuqiRunProgression` is missing.

- [ ] **Step 3: Implement the minimum rules and progression helper**

```csharp
public static class BuqiRunRules
{
    public const int OperationsBeforePve = 2;
    public const int OperationsAfterPve = 2;
    public const int OperationsPerDay = 4;
    public const int TribulationStageCount = 3;
    public const int WinsToVictory = 10;
    public const int MaxBattleWins = WinsToVictory;
    public const int MaxDaoSeals = WinsToVictory;
    public const int MaxOmen = WinsToVictory;
    public const int StartingLifePool = 20;
    public const int StartingLives = StartingLifePool;
    public const int RealmCount = 9;
    public const int ContentScheduleDayCount = 9;
    public const int BoardSlotCount = 10;
    public const int StorageSlotCount = 10;
    public const int StartingCoins = 12;

    public static int GetContentScheduleDay(int runDay)
    {
        if (runDay < 1)
            throw new System.ArgumentOutOfRangeException(nameof(runDay));
        return System.Math.Min(runDay, ContentScheduleDayCount);
    }
}
```

```csharp
public static class BuqiRunProgression
{
    private static readonly int[] s_RealmThresholds = { 0, 8, 18, 30, 44, 60, 78, 98, 120 };

    public static int GetRealm(int cultivation)
    {
        if (cultivation < 0)
            throw new System.ArgumentOutOfRangeException(nameof(cultivation));

        int realm = 0;
        for (int index = 1; index < s_RealmThresholds.Length; index++)
        {
            if (cultivation < s_RealmThresholds[index])
                break;
            realm = index;
        }
        return realm;
    }

    public static int GetBattleReward(BuqiRunBattleKind kind, BuqiRunRawBattleOutcome outcome)
    {
        if (outcome == BuqiRunRawBattleOutcome.PlayerWin)
            return kind == BuqiRunBattleKind.Pve ? 3 : 2;
        return 1;
    }
}
```

Add `HeroId`, `Cultivation`, `Realm`, `LifePool`, `InTribulationTrial`, and `HeartTrialUsed` to `BuqiRunState`; initialize and clone all six. `HeartTrialUsed` must remain true after entering or clearing the first heart trial, so a later life-pool depletion cannot open a second trial. Keep `Lives` as a temporary property forwarding to `LifePool` so existing UI and tests compile while their names are migrated:

```csharp
public int LifePool;
public bool InTribulationTrial;
public bool HeartTrialUsed;
public int Lives
{
    get => LifePool;
    set => LifePool = value;
}
```

- [ ] **Step 4: Build and verify GREEN**

Run: `dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore`

Expected: build succeeds with no new warnings.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs
git commit -m "feat(buqi): add infinite run progression state"
```

### Task 3: Replace Day-Nine Settlement with Ten-Win and Heart-Trial Transitions

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunController.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs`

- [ ] **Step 1: Add failing tests for all terminal branches**

Add tests that prove:

```csharp
// PvE victory: Cultivation +3, Wins/DaoSeals unchanged, then Hour4Operation.
// PvP victory before nine wins: Cultivation +2, Wins/DaoSeals +1, next day.
// PvP loss on day 4: LifePool decreases by 4 and Omen increases by 1.
// First depletion: LifePool == 0, InTribulationTrial == true, run continues.
// Trial victory: LifePool == Day, InTribulationTrial == false, win is counted.
// Trial loss: Outcome == Defeat and Phase == RunTerminal.
// Nine wins: Hour5 completion opens TribulationRoute instead of ordinary PvpBattle.
// Three survived tribulation stages: Wins == 10 and Outcome == Victory.
```

- [ ] **Step 2: Run the Unity EditMode filter and verify RED**

Use the repository AgentBridge fixed-slot protocol, refresh `list_commands`, then run the returned EditMode test command filtered to `Game.Hot.Buqi.Tests.BuqiBazaarDayLoopCoreTests`.

Expected: settlement and tribulation assertions fail under the nine-day logic.

- [ ] **Step 3: Implement settlement transitions**

In `ResolveEncounter`, change only the `Hour5Operation` branch:

```csharp
next.Period = BuqiRunPeriod.Hour6Pvp;
next.Phase = next.Wins == BuqiRunRules.WinsToVictory - 1
    ? BuqiRunPhase.TribulationRoute
    : BuqiRunPhase.PvpBattle;
```

In `SettleBattle`, always apply `BuqiRunProgression.GetBattleReward`, update `Realm`, let only PvP wins increment `Wins`/`DaoSeals`, and route PvP through these rules:

```csharp
if (isPvpLoss && next.InTribulationTrial)
{
    next.Outcome = BuqiRunOutcome.Defeat;
    next.Phase = BuqiRunPhase.RunTerminal;
}
else if (isPvpLoss)
{
    next.LifePool = Math.Max(0, next.LifePool - next.Day);
    next.CurrentOmen = Math.Min(BuqiRunRules.MaxOmen, next.CurrentOmen + 1);
    next.InTribulationTrial = next.LifePool == 0;
    StartNextDay(next);
}
else if (isPvpWin)
{
    next.Wins++;
    next.DaoSeals++;
    if (next.InTribulationTrial)
    {
        next.LifePool = next.Day;
        next.InTribulationTrial = false;
    }
    StartNextDay(next);
}
else
{
    StartNextDay(next);
}
```

Extract `StartNextDay(BuqiRunState state)` to reset encounter index/period/phase without a day cap. On the third survived tribulation stage, increment `Wins` and `DaoSeals` once before assigning `Victory`.

- [ ] **Step 4: Run focused and core tests**

Run the AgentBridge EditMode test command for:

```text
Game.Hot.Buqi.Tests.BuqiBazaarDayLoopCoreTests
Game.Hot.Buqi.Tests.BuqiRunCoreTests
```

Expected: all selected tests pass; no test still expects day nine to trigger tribulation.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Core/BuqiRunController.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunCoreTests.cs
git commit -m "feat(buqi): implement ten-win run transitions"
```

### Task 4: Introduce Save v5 and Discard Incompatible Runs

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveData.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveCodec.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunDemoIntegration.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs`
- Modify carefully: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

- [ ] **Step 1: Add failing save tests**

```csharp
[Test]
public void SaveV5_RoundTripsNewRunFieldsAndTenSlots()
{
    BuqiRunState source = BuqiRunState.CreateInitial(44L, "content-v1");
    source.HeroId = 2;
    source.Cultivation = 44;
    source.Realm = 4;
    source.LifePool = 7;
    source.InTribulationTrial = false;

    string json = BuqiRunSaveCodec.ToJson(BuqiRunSaveCodec.FromState(source));
    Assert.That(BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData data, out string error), Is.True, error);
    Assert.That(BuqiRunSaveCodec.TryToState(data, out BuqiRunState loaded, out error), Is.True, error);
    Assert.That(loaded.HeroId, Is.EqualTo(2));
    Assert.That(loaded.Cultivation, Is.EqualTo(44));
    Assert.That(loaded.Realm, Is.EqualTo(4));
    Assert.That(loaded.LifePool, Is.EqualTo(7));
    Assert.That(loaded.BoardInstanceIds, Has.Count.EqualTo(10));
}

[Test]
public void SaveV4_IsReportedAsUnsupportedSoInitializationCanReplaceIt()
{
    BuqiRunSaveData old = BuqiRunSaveCodec.FromState(BuqiRunState.CreateInitial(45L, "content-v1"));
    old.SaveVersion = "buqi-run-save-v4";

    Assert.That(BuqiRunSaveCodec.TryFromJson(
        BuqiRunSaveCodec.ToJson(old), out _, out _, out BuqiRunSaveFailureKind kind), Is.False);
    Assert.That(kind, Is.EqualTo(BuqiRunSaveFailureKind.UnsupportedVersion));
}
```

- [ ] **Step 2: Verify RED**

Run the focused EditMode suite for `BuqiBazaarDayLoopCoreTests`.

Expected: missing save fields and v4 still being accepted/migrated.

- [ ] **Step 3: Implement v5 schema and validation**

Set `CurrentSaveVersion = "buqi-run-save-v5"`, retain v4 only as a named previous version for diagnostics, and accept only v5 in `TryFromJson`. Add these serialized fields:

```csharp
public int HeroId;
public int Cultivation;
public int Realm;
public int LifePool;
public bool InTribulationTrial;
public bool HeartTrialUsed;
```

Map them in `FromState` and `TryToState`. Remove the day upper bound. Validate `HeroId` in `0..4`, non-negative cultivation, exact realm derivation, life in `0..20`, ten board slots, ten storage slots, and these phase invariants:

```text
ordinary run: Wins <= 9
TribulationRoute/Stage: Wins == 9
tribulation victory terminal: Wins == 10
LifePool == 0 in a non-terminal state only when InTribulationTrial is true
early terminal defeat requires InTribulationTrial and LifePool == 0
```

Keep the existing `TryRecoverIncompatibleSave` flow: unsupported v4 causes `TryDelete`, creates a new v5 run, and returns the existing Chinese recovery message.

- [ ] **Step 4: Run save, settlement, and initialization tests**

Run the AgentBridge EditMode command for `BuqiBazaarDayLoopCoreTests`, `BuqiRunSettlementTests`, and `BuqiRunDayLoopIntegrationTests`.

Expected: all selected tests pass, an incompatible old save is replaced once, and replay/settlement idempotency remains intact.

- [ ] **Step 5: Commit only the save migration changes**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveData.cs Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveCodec.cs Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunDemoIntegration.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarDayLoopCoreTests.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunDayLoopIntegrationTests.cs
git commit -m "feat(buqi): migrate runs to save v5"
```

### Task 5: Keep Merchant Supply Valid After Day Nine

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiBazaarSupplyViewSource.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Supply/BuqiSupplyIntegration.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarSupplyViewSourceTestSuite.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiSupplyTestSuite.cs`

- [ ] **Step 1: Add a failing day-10 supply test**

```csharp
[Test]
public void Generate_DayBeyondAuthoredScheduleUsesFinalDayPool()
{
    BuqiBazaarSupplyContext context = CreateContext(day: 10);
    Assert.That(source.TryGenerate(context, out var offers, out string error), Is.True, error);
    Assert.That(offers, Is.Not.Empty);
}
```

- [ ] **Step 2: Verify RED**

Run the focused EditMode supply tests.

Expected: day 10 is rejected by the old `RunDayCount` checks.

- [ ] **Step 3: Clamp only content lookup days**

Use the real run day for seeds, loss cost, UI, and save state. Use `BuqiRunRules.GetContentScheduleDay(context.Day)` only when evaluating merchant unlock/retire ranges and weighted rows. Replace `RunDayCount` schema validation with `ContentScheduleDayCount` for authored data bounds.

- [ ] **Step 4: Run supply and run integration tests**

Run the AgentBridge EditMode command for both supply suites and `BuqiRunDayLoopIntegrationTests`.

Expected: days 1-9 remain deterministic and day 10+ use the final authored pool without changing the real day.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiBazaarSupplyViewSource.cs Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Supply/BuqiSupplyIntegration.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarSupplyViewSourceTestSuite.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiSupplyTestSuite.cs
git commit -m "feat(buqi): extend merchant supply beyond day nine"
```

### Task 6: Full Regression and Documentation Alignment

**Files:**
- Modify only failing old-baseline assertions in `Unity/Assets/Tests/GameHot/Buqi/EditMode/*.cs`
- Modify: `docs/game-concepts/buqi-core.md`
- Modify: `docs/策划案/README.md`

- [ ] **Step 1: Compile both Unity projects**

Run:

```powershell
dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore
dotnet build Unity/Game.Hot.Editor.csproj --no-restore
```

Expected: zero errors; no new warnings.

- [ ] **Step 2: Run all Buqi EditMode tests through AgentBridge**

Follow `AGENTS.md`: read the installed AgentBridge `AGENT.md`, wait for a free fixed slot, call `list_commands` once for this session, then run the runtime-provided all-Buqi EditMode test command.

Expected: all tests pass. Any remaining failure must be classified as a stale nine-day/eight-slot expectation or a genuine regression before editing.

- [ ] **Step 3: Align the document index**

Update `buqi-core.md` and the review registry so they consistently say 10 slots, 7 build directions, unlimited days, six periods, nine wins then tribulation, and local snapshots for Demo PvP. Do not expand background story text.

- [ ] **Step 4: Commit regression cleanup**

```powershell
git add Unity/Assets/Tests/GameHot/Buqi/EditMode docs/game-concepts/buqi-core.md docs/策划案/README.md
git commit -m "test(buqi): align suite with infinite run baseline"
```

- [ ] **Step 5: Record the next implementation boundary**

The next plan starts only after this package is green. Its scope is hero selection and content-pool filtering; hero-specific combat traits and story presentation remain separate later plans.
