# Buqi Run PVE/PVP Battle Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the player's real build to deterministic PVE and local-preset-player PVP battles while preserving the existing simulator and replay system.

**Architecture:** Add local opponent pools/providers and a run battle service under `Run/Battle`. The service receives an already-built player `BuildSnapshot`, selects the correct local opponent type through the shared Core RNG, creates a real `BattleRequest`, invokes `BuqiBattleSimulator`, and returns replay plus a Core raw outcome. No network code is introduced.

**Tech Stack:** C#, existing Buqi battle DTOs/simulator/replay, NUnit EditMode tests, headless simulator checks.

---

## Dependency and File Ownership

Start after run-core is merged. This worktree may only create or modify:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/*.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunBattleIntegrationTests.cs`

Do not modify Core RNG, Economy, Encounter, Settlement, existing battle simulator/rules/replay, Demo factory, UI, generated files, prefabs, or workbooks.

## Task 1: Local PVE and Preset-Player PVP Pools

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/BuqiRunOpponentTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/BuqiLocalOpponentProvider.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunBattleIntegrationTests.cs`

- [ ] **Step 1: Write failing pool-separation and deterministic-selection tests**

Keep all fixtures in this test file. `TestPool.Create` must build legal, deep-copyable `BuqiRunOpponent` entries for every supplied ID with the requested source; `TestPool.Standard` must contain at least two legal PVE and two legal local-player PVP entries. Build their snapshots through the existing Buqi test-fixture conventions. Do not leave helper names undefined or modify shared battle fixtures.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Hot.Tests
{
    public sealed class BuqiRunBattleIntegrationTests
    {
        [Test]
        public void PveAndPvpSelectOnlyFromTheirOwnLocalPools()
        {
            BuqiLocalOpponentPool pool = TestPool.Create(
                pveIds: new[] { "monster-a", "monster-b" },
                pvpIds: new[] { "player-a", "player-b" });
            var provider = new BuqiLocalOpponentProvider(pool);
            BuqiRunState pveRun = BuqiRunState.CreateInitial(1001);
            pveRun.Phase = BuqiRunPhase.PveBattle;
            BuqiRunState pvpRun = pveRun.Clone();
            pvpRun.Phase = BuqiRunPhase.PvpBattle;

            Assert.That(provider.TrySelect(pveRun, BuqiRunBattleKind.Pve,
                out BuqiRunOpponent pve, out _, out _), Is.True);
            Assert.That(pve.Source, Is.EqualTo(BuqiRunOpponentSource.PvePreset));
            Assert.That(pve.OpponentId, Does.StartWith("monster-"));

            Assert.That(provider.TrySelect(pvpRun, BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent pvp, out _, out _), Is.True);
            Assert.That(pvp.Source, Is.EqualTo(BuqiRunOpponentSource.LocalPlayerPreset));
            Assert.That(pvp.OpponentId, Does.StartWith("player-"));
        }

        [Test]
        public void SameSeedAndCursorSelectSamePresetPlayer()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            BuqiRunState left = BuqiRunState.CreateInitial(2002);
            left.Phase = BuqiRunPhase.PvpBattle;
            BuqiRunState right = left.Clone();

            provider.TrySelect(left, BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent leftOpponent, out int leftCursor, out _);
            provider.TrySelect(right, BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent rightOpponent, out int rightCursor, out _);

            Assert.That(leftOpponent.OpponentId, Is.EqualTo(rightOpponent.OpponentId));
            Assert.That(leftCursor, Is.EqualTo(rightCursor));
        }
    }
}
```

- [ ] **Step 2: Run tests and confirm missing-type failure**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`.

- [ ] **Step 3: Implement opponent contracts**

Use these public types:

```csharp
using System.Collections.Generic;

namespace Game.Hot
{
    public enum BuqiRunOpponentSource
    {
        PvePreset,
        LocalPlayerPreset,
    }

    public sealed class BuqiRunOpponent
    {
        public string OpponentId = string.Empty;
        public string DisplayName = string.Empty;
        public BuqiRunOpponentSource Source;
        public BuildSnapshot Build;
    }

    public sealed class BuqiLocalOpponentPool
    {
        public List<BuqiRunOpponent> Pve = new List<BuqiRunOpponent>();
        public List<BuqiRunOpponent> Pvp = new List<BuqiRunOpponent>();
    }
}
```

- [ ] **Step 4: Implement deterministic local provider**

Use:

```csharp
public bool TrySelect(
    BuqiRunState run,
    BuqiRunBattleKind kind,
    out BuqiRunOpponent opponent,
    out int nextRngCursor,
    out string error)
```

Validate phase matches battle kind. Select from only the matching non-empty local list using Core `BuqiRunRandom`. Return a deep-copied opponent/build and next cursor; do not mutate `run`. Empty/invalid pools fail closed without cursor movement. No class in this package may reference ET sessions, sockets, HTTP, remote APIs, or player upload DTOs.

- [ ] **Step 5: Run provider tests**

Expected: pool separation, deterministic selection, phase mismatch, and empty-pool tests pass.

## Task 2: Real Player-vs-Opponent Request and Replay

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/BuqiRunBattleSession.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/BuqiRunBattleService.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunBattleIntegrationTests.cs`

- [ ] **Step 1: Add failing real-player-request tests**

Tests must prove:

- `BattleRequest.Left` is the exact player snapshot supplied by the caller, not a second opponent preset.
- `BattleRequest.Right` is the selected PVE/PVP opponent.
- `RoundIndex` derives from day and battle kind without collision.
- The service calls the existing simulator and produces non-empty replay logs for legal fixtures.
- Changing player equipment or placement changes request/result hash input.
- Invalid player or opponent builds return failure and never produce a settlable session.

Use this output contract:

```csharp
public sealed class BuqiRunBattleSession
{
    public string BattleId = string.Empty;
    public BuqiRunBattleKind Kind;
    public string OpponentId = string.Empty;
    public int NextRngCursor;
    public BattleRequest Request;
    public BattleResult Result;
    public System.Collections.Generic.List<BattleEvent> Log = new();
    public BattleReplayData Replay;
    public BuqiRunRawBattleOutcome RawOutcome;
}
```

- [ ] **Step 2: Implement run battle service**

Use this entry point:

```csharp
public bool TryCreateAndSimulate(
    BuqiRunState run,
    BuqiRunBattleKind kind,
    BuildSnapshot playerBuild,
    IItemDefinitionProvider definitions,
    out BuqiRunBattleSession session,
    out string error)
```

The service must:

1. Ask `BuqiLocalOpponentProvider` for the matching local candidate.
2. Validate player and opponent through existing `BuqiBoardValidator`.
3. Build a deterministic `BattleRequest` with player on Left.
4. Call existing `BuqiBattleSimulator.Simulate` exactly once.
5. Reject `InvalidBuild` and `Aborted` outcomes.
6. Map `LeftWin -> PlayerWin`, `RightWin -> OpponentWin`, `Draw -> Draw` without hiding the original result.
7. Build `BattleReplayData` from the same request/result/log.
8. Return the selected opponent and updated cursor only in the successful session.

- [ ] **Step 3: Run focused integration tests**

Expected: legal PVE and PVP sessions use the real player build and pass replay-controller validation.

## Task 3: Existing Config Pool Adapter

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle/BuqiLocalOpponentPoolAdapter.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunBattleIntegrationTests.cs`

- [ ] **Step 1: Add adapter tests**

Build a `BuqiConfigCatalog` fixture with explicit local pool assignments. Verify every configured legal snapshot appears exactly once in either PVE or PVP, IDs remain stable, and illegal/duplicate entries are reported.

- [ ] **Step 2: Implement adapter without changing generated configuration**

The Demo currently has one local opponent table. Partition it deterministically in the adapter using an explicit constructor input containing PVE IDs and PVP IDs. The UI integration layer will provide those lists from a small non-generated Demo catalog. Do not infer opponent type from display name, build label, list position, or random choice.

- [ ] **Step 3: Verify no network dependency**

Inspect the new package references and tests. There must be no ET network/session namespace and no server configuration requirement.

## Task 4: Verification and Commit

- [ ] **Step 1: Run tests**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`, focused Unity EditMode tests, existing `BuqiBattleTests`, `BuqiBattleDemoFactoryTests`, and `BuqiReplayTests`.

Also run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -c Release
```

Expected: focused tests pass, existing deterministic battle vectors remain unchanged, and headless verification exits 0.

- [ ] **Step 2: Commit only owned files**

```powershell
git add -- Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Battle Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunBattleIntegrationTests.cs
git commit -m "feat(buqi): connect local PVE and PVP battles"
```

## Completion Report

Return commit hash, files, test results, exact local pool fixture used, and confirmation that PVP is entirely local and the existing simulator was not modified.
