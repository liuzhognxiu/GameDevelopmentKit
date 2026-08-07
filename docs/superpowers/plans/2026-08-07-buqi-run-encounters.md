# Buqi Run Encounters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build deterministic three-per-day random Shop/Event generation, frozen candidates, and atomic event resolution for the Buqi day-run Demo.

**Architecture:** Add a pure encounter package driven only by `RunSeed`, `RngCursor`, current day, encounter index, and an injected catalog. It returns frozen encounter state and explicit deltas; the later integration layer applies those deltas through the economy/core services.

**Tech Stack:** C#, NUnit EditMode tests, Unity GameHot assembly.

---

## Dependency and File Ownership

Start only after run-core is merged. This worktree may only create or modify:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter/*.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEncounterTests.cs`

Do not modify Core, Economy, Battle, Settlement, existing Demo/UI files, generated files, prefabs, or workbooks.

## Task 1: Verify the Shared Deterministic Random Source

**Files:**

- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEncounterTests.cs`

- [ ] **Step 1: Write failing deterministic sequence tests**

```csharp
using NUnit.Framework;

using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Encounter;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEncounterTests
    {
        [Test]
        public void SameSeedAndCursorProduceSameSequence()
        {
            int leftCursor = 0;
            int rightCursor = 0;
            for (int index = 0; index < 20; index++)
            {
                Assert.That(BuqiRunRandom.Next(12345, ref leftCursor, 17),
                    Is.EqualTo(BuqiRunRandom.Next(12345, ref rightCursor, 17)));
            }
            Assert.That(leftCursor, Is.EqualTo(20));
        }

        [Test]
        public void InvalidRangeDoesNotAdvanceCursor()
        {
            int cursor = 4;
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                BuqiRunRandom.Next(10, ref cursor, 0));
            Assert.That(cursor, Is.EqualTo(4));
        }
    }
}
```

- [ ] **Step 2: Run tests against the merged Core random source**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`.

- [ ] **Step 3: Confirm the Core RNG contract without reimplementing it**

`BuqiRunRandom` belongs to `Run/Core` and is read-only in this worktree. Keep the cross-package tests to prove encounter code consumes the shared cursor correctly; do not create a second random implementation under Encounter.

- [ ] **Step 4: Run focused tests**

Expected: deterministic and exact-vector tests pass on repeated runs.

## Task 2: Frozen Shop/Event Generation

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter/BuqiRunEncounterTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter/BuqiRunEncounterService.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEncounterTests.cs`

- [ ] **Step 1: Add failing generation tests**

Tests must prove:

- Each generated encounter is either `Shop` or `Event`.
- Default weights are `1:1` and both outcomes appear across a fixed seed vector.
- Three calls advance the cursor and return IDs containing day and encounter index.
- Calling `GetOrCreate` twice with an unresolved frozen encounter returns the same object data and does not advance the cursor.
- Empty shop/event pools fail closed with a reason and do not advance the run cursor.

Use these public contracts:

```csharp
public enum BuqiRunEncounterKind { Shop, Event }

public sealed class BuqiRunEncounterState
{
    public string EncounterId = string.Empty;
    public BuqiRunEncounterKind Kind;
    public int Day;
    public int EncounterIndex;
    public int NextRngCursor;
    public bool Resolved;
    public System.Collections.Generic.List<string> CandidateIds = new();
}

public interface IBuqiRunEncounterCatalog
{
    System.Collections.Generic.IReadOnlyList<string> ShopOfferIds { get; }
    System.Collections.Generic.IReadOnlyList<string> EventIds { get; }
}
```

- [ ] **Step 2: Implement generation without phase mutation**

Use:

```csharp
public bool TryGetOrCreate(
    BuqiRunState run,
    BuqiRunEncounterState current,
    out BuqiRunEncounterState encounter,
    out string error)
```

On creation, clone the run only to calculate the next cursor and expose it in the result through `NextRngCursor`; do not directly advance `EncounterIndex` or `Phase`. Select four unique shop offers when kind is Shop and three event IDs when kind is Event, or all available entries when a pool is smaller. Candidate order is deterministic.

- [ ] **Step 3: Run generation tests**

Expected: deterministic generation, frozen rerender, and empty-pool failure tests pass.

## Task 3: Event Choice Resolution

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter/BuqiRunEventResolver.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter/BuqiRunEncounterTypes.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEncounterTests.cs`

- [ ] **Step 1: Add failing event-resolution tests**

Cover coin, life, item-definition, and refinement-grant effects. Verify an unlisted choice, already-resolved encounter, empty choice ID, and duplicate resolution command fail without mutating the source encounter.

Use an explicit delta instead of mutating economy state:

```csharp
public sealed class BuqiRunEncounterDelta
{
    public int Coins;
    public int Lives;
    public string GrantedItemDefinitionId = string.Empty;
    public string GrantedRefinementId = string.Empty;
}
```

- [ ] **Step 2: Implement catalog-backed resolution**

Define `IBuqiRunEventCatalog.TryGet(string eventId, out BuqiRunEncounterDelta delta)`. `Resolve` validates the frozen candidate list, clones the delta, marks a cloned encounter resolved, and returns a stable `ResolutionId = EncounterId + ":" + eventId`. It never advances the core phase itself.

- [ ] **Step 3: Run all encounter tests**

Expected: generation and resolution tests pass, including immutability assertions.

## Task 4: Verification and Commit

- [ ] **Step 1: Run static and Unity tests**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`, then `BuqiRunEncounterTests` in Unity EditMode.

- [ ] **Step 2: Commit only owned files**

```powershell
git add -- Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Encounter Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEncounterTests.cs
git commit -m "feat(buqi): add deterministic run encounters"
```

## Completion Report

Return the commit hash, exact files, locked RNG vector, tests/results, and confirmation that no other package or UI file changed.
