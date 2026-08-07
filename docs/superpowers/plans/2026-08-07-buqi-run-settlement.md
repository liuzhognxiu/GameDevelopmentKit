# Buqi Run Settlement and Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real battle summaries, save-before-settle recovery, versioned run persistence, and idempotent settlement coordination.

**Architecture:** Add a settlement package that depends only on merged Core and existing battle result/event DTOs. It serializes Core through explicit list-based DTOs, keeps Economy/Encounter/Battle package payloads opaque until integration, and requires durable pending-result persistence before calling the Core settlement transition.

**Tech Stack:** C#, Unity `JsonUtility`-compatible DTOs, `System.IO` atomic replacement, NUnit EditMode tests.

---

## Dependency and File Ownership

Start after run-core is merged. This worktree may only create or modify:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/*.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

Do not modify Core, Economy, Encounter, Battle, old Demo/UI files, generated files, prefabs, or workbooks.

## Task 1: Real Battle Summary

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunBattleSummary.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunBattleSummaryBuilder.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

- [ ] **Step 1: Write failing summary tests**

Create event fixtures with known `Sequence`, `Tick`, `SourceInstanceId`, `EffectId`, `Amount`, and `ReasonCode`. Tests must prove the summary contains:

- Original `BattleOutcome` and `BattleLogHash`.
- Total positive effect amount grouped by source instance.
- Highest-contribution source with deterministic ordinal tie-break.
- First non-empty truncation/invalid reason as the key interruption.
- Highest positive event amount carrying an overload reason as the risk loss.
- No fabricated facts when log is empty.

Use this contract:

```csharp
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Settlement
{
    public sealed class BuqiRunBattleSummary
    {
        public BattleOutcome RawOutcome;
        public string BattleLogHash = string.Empty;
        public string TopSourceInstanceId = string.Empty;
        public int TopContribution;
        public string KeyInterruptionReason = string.Empty;
        public int OverloadLoss;
        public List<string> FactLines = new List<string>();
    }
}
```

- [ ] **Step 2: Run tests and confirm missing summary types**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`.

- [ ] **Step 3: Implement stable aggregation**

`BuqiRunBattleSummaryBuilder.Build(BattleResult result, IReadOnlyList<BattleEvent> log)` must sort by existing stable `Sequence`, aggregate only positive `Amount`, preserve exact IDs/reason codes, and format concise Chinese fact lines through `BuqiText.Format`. It must not use the old hard-coded `CreateFacts()` output.

- [ ] **Step 4: Run focused summary tests**

Expected: exact facts and empty-log behavior pass.

## Task 2: Explicit Versioned Save DTO and Codec

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveData.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveCodec.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

- [ ] **Step 1: Add failing round-trip and rejection tests**

Tests must round-trip every Core field including board/storage slots, command IDs, settlement IDs, phase, outcome, RNG cursor, day, wins, lives, coins, and revision. Also cover malformed JSON, empty JSON, wrong save version, wrong rule version, and slot counts other than 8.

Use serializable DTO lists, never `HashSet` or `Dictionary` fields:

```csharp
[System.Serializable]
public sealed class BuqiRunSaveData
{
    public const string CurrentSaveVersion = "buqi-run-save-v1";
    public string SaveVersion = CurrentSaveVersion;
    public string RuleVersion = string.Empty;
    public long RunSeed;
    public int RngCursor;
    public int Revision;
    public int Day;
    public int EncounterIndex;
    public int Phase;
    public int Outcome;
    public int Coins;
    public int Wins;
    public int Lives;
    public System.Collections.Generic.List<string> BoardInstanceIds = new();
    public System.Collections.Generic.List<string> StorageInstanceIds = new();
    public System.Collections.Generic.List<string> AppliedCommandIds = new();
    public System.Collections.Generic.List<string> AppliedSettlementIds = new();
    public string EconomyPayload = string.Empty;
    public string EncounterPayload = string.Empty;
    public string BattlePayload = string.Empty;
    public BuqiRunPendingSettlement PendingSettlement;
}
```

- [ ] **Step 2: Implement deterministic mapper and codec**

`FromState` sorts hash-set IDs ordinally. `TryToState` validates versions, enum ranges, non-negative counters, exact slot counts, duplicate IDs, and terminal consistency. `ToJson` and `TryFromJson` use Unity JSON APIs already available to GameHot and return explicit errors rather than throwing through UI.

- [ ] **Step 3: Run codec tests**

Expected: round trip preserves all Core values and all invalid fixtures fail closed.

## Task 3: Durable Store and Save-Before-Settle Coordinator

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/IBuqiRunStore.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiFileRunStore.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSettlementCoordinator.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

- [ ] **Step 1: Add failing persistence-order and recovery tests**

Use an in-memory spy store. Prove:

- Pending battle result is saved before `BuqiRunController.SettleBattle` is called.
- Store failure leaves Core state untouched.
- Successful Core settlement writes the applied save with no pending marker.
- Loading a pending marker resumes the same settlement ID and never double-increments wins/lives.
- Draw persists raw `Draw` but Core increments wins.
- A terminal settlement is persisted as terminal and cannot proceed to PVP/day settlement.

- [ ] **Step 2: Implement store contract**

```csharp
public interface IBuqiRunStore
{
    bool TryRead(out string json, out string error);
    bool TryWrite(string json, out string error);
    bool TryDelete(out string error);
}
```

`BuqiFileRunStore` receives one explicit absolute file path. Write UTF-8 to `path + ".tmp"`, flush/close, then replace/move over the target. Never delete or enumerate parent directories.

- [ ] **Step 3: Implement pending settlement and coordinator**

`BuqiRunPendingSettlement` contains settlement ID, expected revision, battle kind, raw outcome, battle log hash, and summary. The coordinator first writes an envelope with that pending record, then invokes Core, then writes the returned state without pending. Replay of the same ID returns the saved applied state.

- [ ] **Step 4: Run recovery tests**

Expected: ordering spy, failure rollback, duplicate resume, draw, victory, and defeat tests pass.

## Task 4: Verification and Commit

- [ ] **Step 1: Run affected tests**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`, `BuqiRunSettlementTests`, `BuqiRunCoreTests`, and existing replay tests in Unity EditMode.

- [ ] **Step 2: Commit only owned files**

```powershell
git add -- Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs
git commit -m "feat(buqi): add run settlement and recovery"
```

## Completion Report

Return commit hash, files, tests/results, durable-write strategy, and confirmation that opaque payloads were not interpreted or silently discarded.
