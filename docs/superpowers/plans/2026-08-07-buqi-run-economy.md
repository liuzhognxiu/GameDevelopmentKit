# Buqi Run Economy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build deterministic item-instance, inventory, shop, merge, upgrade, sell, and refinement behavior for the approved Buqi day-run Demo.

**Architecture:** Add a pure C# economy package that consumes the already-merged `BuqiRunState` contract and returns atomic cloned snapshots. The package owns item instances and prices but never advances day phases, resolves battles, generates encounters, writes saves, or touches Unity UI.

**Tech Stack:** C#, NUnit EditMode tests, Unity GameHot assembly, existing Buqi configuration model adapters.

---

## Dependency and File Ownership

Start only after the run-core commit is merged into the base branch. This worktree may only create or modify:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/*.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs`

Do not modify `Run/Core`, the old Demo controller/state, UI forms, stage widgets, generated files, prefabs, or configuration workbooks.

## Task 1: Item Instances and Atomic Economy Snapshot

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/BuqiRunEconomyTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/BuqiRunEconomySnapshot.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs`

- [ ] **Step 1: Write failing clone and instance-identity tests**

```csharp
using NUnit.Framework;

using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEconomyTests
    {
        [Test]
        public void CreateInitialUsesCoreEightSlotStorageAndNoSharedCollections()
        {
            BuqiRunEconomySnapshot snapshot = BuqiRunEconomySnapshot.CreateInitial(700);
            BuqiRunEconomySnapshot clone = snapshot.Clone();

            Assert.That(snapshot.Run.StorageInstanceIds, Has.Count.EqualTo(8));
            Assert.That(snapshot.Items, Is.Empty);
            clone.Run.StorageInstanceIds[0] = "changed";
            Assert.That(snapshot.Run.StorageInstanceIds[0], Is.Empty);
        }

        [Test]
        public void ItemCopiesHaveStableUniqueInstanceIds()
        {
            BuqiRunEconomySnapshot snapshot = BuqiRunEconomySnapshot.CreateInitial(701);
            string first = snapshot.CreateInstanceId();
            string second = snapshot.CreateInstanceId();

            Assert.That(first, Is.EqualTo("run-701-item-1"));
            Assert.That(second, Is.EqualTo("run-701-item-2"));
            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
```

- [ ] **Step 2: Run the focused build and confirm missing-type failure**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal`.

Expected: missing `BuqiRunEconomySnapshot` and item economy types.

- [ ] **Step 3: Add complete economy contracts**

Create these contracts in `BuqiRunEconomyTypes.cs`:

```csharp
namespace Game.Hot.Buqi.Run.Economy
{
    public enum BuqiRunItemQuality
    {
        Common,
        Improved,
        Finalized,
    }

    public sealed class BuqiRunItemInstance
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public BuqiRunItemQuality Quality;
        public string RefinementId = string.Empty;

        public BuqiRunItemInstance Clone()
        {
            return (BuqiRunItemInstance)MemberwiseClone();
        }
    }

    public sealed class BuqiRunItemDefinition
    {
        public string DefinitionId = string.Empty;
        public int Size;
        public int BuyPrice;
        public int SellPrice;
        public int UpgradePrice;
        public int RefinementPrice;
    }

    public interface IBuqiRunItemCatalog
    {
        bool TryGet(string definitionId, out BuqiRunItemDefinition definition);
    }

    public sealed class BuqiRunEconomyResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public BuqiRunEconomySnapshot Snapshot;
        public string AffectedInstanceId = string.Empty;
    }
}
```

- [ ] **Step 4: Implement deep-cloned economy snapshot**

`BuqiRunEconomySnapshot` must contain:

```csharp
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Economy
{
    public sealed class BuqiRunEconomySnapshot
    {
        public BuqiRunState Run;
        public int NextItemOrdinal = 1;
        public Dictionary<string, BuqiRunItemInstance> Items =
            new Dictionary<string, BuqiRunItemInstance>();

        public static BuqiRunEconomySnapshot CreateInitial(long runSeed)
        {
            return new BuqiRunEconomySnapshot { Run = BuqiRunState.CreateInitial(runSeed) };
        }

        public string CreateInstanceId()
        {
            return $"run-{Run.RunSeed}-item-{NextItemOrdinal++}";
        }

        public BuqiRunEconomySnapshot Clone()
        {
            var clone = new BuqiRunEconomySnapshot
            {
                Run = Run.Clone(),
                NextItemOrdinal = NextItemOrdinal,
            };
            foreach (KeyValuePair<string, BuqiRunItemInstance> pair in Items)
                clone.Items.Add(pair.Key, pair.Value.Clone());
            return clone;
        }
    }
}
```

- [ ] **Step 5: Run focused tests**

Expected: initial/clone/identity tests pass.

## Task 2: Purchase, Capacity, and Automatic Merge

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/BuqiRunEconomyService.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs`

- [ ] **Step 1: Add failing purchase and merge tests**

Cover these exact cases with an in-test fake `IBuqiRunItemCatalog`:

Keep every referenced fixture helper in this same test file. `TestCatalog.With` must create a fake catalog entry whose arguments are `(definitionId, size, buyPrice)` and derive the remaining prices using the documented fallback formulas. `FilledStorageWithCommonBlade` and `FilledStorageWithoutMerge` must populate all eight Core storage slots plus matching `Items` entries without calling production mutation helpers. Do not leave helper names undefined.

```csharp
[Test]
public void PurchaseDeductsCoinsAndAddsUniqueInstanceToFirstStorageSlot()
{
    BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(800);
    var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

    BuqiRunEconomyResult result = service.Purchase(state, "blade");

    Assert.That(result.Success, Is.True);
    Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(8));
    Assert.That(result.Snapshot.Run.StorageInstanceIds[0], Is.EqualTo("run-800-item-1"));
    Assert.That(result.Snapshot.Items["run-800-item-1"].DefinitionId, Is.EqualTo("blade"));
}

[Test]
public void FullStorageAllowsPurchaseOnlyWhenNewCopyImmediatelyMerges()
{
    BuqiRunEconomySnapshot state = FilledStorageWithCommonBlade(801);
    var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

    BuqiRunEconomyResult result = service.Purchase(state, "blade");

    Assert.That(result.Success, Is.True);
    Assert.That(result.Snapshot.Items[result.AffectedInstanceId].Quality,
        Is.EqualTo(BuqiRunItemQuality.Improved));
    Assert.That(result.Snapshot.Run.StorageInstanceIds, Has.Count.EqualTo(8));
}

[Test]
public void RejectedPurchaseNeverChangesCoinsOrInventory()
{
    BuqiRunEconomySnapshot state = FilledStorageWithoutMerge(802);
    var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

    BuqiRunEconomyResult result = service.Purchase(state, "blade");

    Assert.That(result.Success, Is.False);
    Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(state.Run.Coins));
    Assert.That(result.Snapshot.Items.Keys, Is.EquivalentTo(state.Items.Keys));
}
```

- [ ] **Step 2: Implement validate-then-commit purchase**

`BuqiRunEconomyService.Purchase` must:

1. Resolve the exact definition through `IBuqiRunItemCatalog`.
2. Reject unknown IDs, non-positive size, insufficient coins, and no-space/no-merge states without mutating the input.
3. Prefer merging with the lowest-slot matching instance of the same definition and quality below `Finalized`.
4. Preserve the existing instance ID and board/storage placement during merge; consume only the new virtual copy.
5. Otherwise create a deterministic instance ID and place it in the first empty storage slot.
6. Deduct coins only on the cloned successful result.

Use this public signature:

```csharp
public BuqiRunEconomyResult Purchase(BuqiRunEconomySnapshot source, string definitionId)
```

- [ ] **Step 3: Run purchase tests**

Expected: purchase, merge-at-capacity, insufficient-coins, unknown-definition, and full-storage rejection tests pass.

## Task 3: Sell, Upgrade, and Refinement

**Files:**

- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/BuqiRunEconomyService.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs`

- [ ] **Step 1: Add failing operation tests**

Tests must prove:

- Selling removes the exact instance from `Items`, board, and storage and adds configured `SellPrice`.
- Upgrade deducts configured `UpgradePrice`, advances one quality tier, and rejects `Finalized`.
- Refinement deducts configured `RefinementPrice`, writes the requested non-empty refinement ID, and rejects a second refinement.
- Unknown instance, insufficient coins, and illegal target leave the full input snapshot unchanged.

Use these signatures:

```csharp
public BuqiRunEconomyResult Sell(BuqiRunEconomySnapshot source, string instanceId)
public BuqiRunEconomyResult Upgrade(BuqiRunEconomySnapshot source, string instanceId)
public BuqiRunEconomyResult Refine(
    BuqiRunEconomySnapshot source,
    string instanceId,
    string refinementId)
```

- [ ] **Step 2: Implement exact-instance operations**

All methods clone first, validate against the clone, and return a result containing either the fully committed clone or a fresh clone of the untouched source. Never expose a partially mutated snapshot.

When selling a board item, clear every board slot containing its instance ID. When upgrading/refining, preserve instance ID and placement.

- [ ] **Step 3: Run all economy tests**

Expected: every operation and rollback assertion passes.

## Task 4: Existing Config Adapter and Verification

**Files:**

- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy/BuqiRunItemCatalogAdapter.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs`

- [ ] **Step 1: Add adapter tests using `BuqiConfigCatalog` fixtures**

Verify that the adapter maps definition ID, size, base buy price, and deterministic derived prices. Use these fallback formulas only when current configuration has no explicit field:

```text
SellPrice = max(1, BuyPrice / 2)
UpgradePrice = max(1, BuyPrice)
RefinementPrice = max(1, BuyPrice)
```

- [ ] **Step 2: Implement `BuqiRunItemCatalogAdapter`**

The adapter reads only existing `BuqiConfigCatalog.Items`; it does not change generated table types or workbooks. Reject duplicate IDs and missing definitions explicitly.

- [ ] **Step 3: Run verification**

Run:

```powershell
dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal
```

Then run `BuqiRunEconomyTests` and existing Buqi config/drag-deploy tests in Unity EditMode.

Expected: all focused and affected tests pass.

- [ ] **Step 4: Commit only owned files**

```powershell
git add -- Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Economy Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunEconomyTests.cs
git commit -m "feat(buqi): add run inventory and economy"
```

## Completion Report

Return the commit hash, exact file list, tests and results, and any adapter mapping that differed from the plan because of an existing exact config field. Confirm no Core, UI, prefab, generated file, or workbook was modified.
