# Buqi Effects Builds DB Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand Buqi from the current 9-card, 3-build, 6-effect slice into a stage-gated effects/builds foundation that supports player-facing attack, shield, heal, regen, poison, burn, freeze, charge, speed, slow, and overload terms.

**Architecture:** Keep the existing pure C# battle core and Luban adapter boundary. Add deterministic runtime state for side statuses and item freeze, then update validators, generated schema inputs, and content gates so builds and echoes are data-driven instead of fixed-count arrays.

**Tech Stack:** C# 9, .NET 8 headless validator, Unity GameHot pure C# battle code, Luban Excel configuration.

---

### File Structure

**Modify:**
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiTypes.cs` - add new effect enum values and condition kinds.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiRuntimeState.cs` - add max life, timed side statuses, and frozen item duration.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiBattleDtos.cs` - allow temporary freeze modifiers and keep compatibility with haste/slow.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiDefinitions.cs` - keep effect identity stable with new fields.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Simulation/BuqiBattleSimulator.cs` - implement heal, regen, poison, burn, freeze, and public-term aliases.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Rules/BuqiBoardValidator.cs` - validate new effect target policies.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Rules/BuqiCrypto.cs` - ensure snapshot/status-impacting fields remain hashed.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Definition/BuqiTestSuite.cs` - add test fixture definitions for new effects.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Definition/BuqiContractChecks.cs` - add deterministic behavior checks.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Config/BuqiConfigValidator.cs` - replace hard-coded Step 3 count gates with stage gates and validate new effects.
- `Design/Excel/GameHot/Datas/__enums__.xlsx` - add new effect/build enum rows.
- `Design/Excel/GameHot/Datas/Buqi/BuqiItem.xlsx` - later content pass for 18-card sandbox.
- `Design/Excel/GameHot/Datas/Buqi/BuqiEcho.xlsx` - later content pass for 12 echo decks.
- `docs/game-concepts/buqi-battle-contract.md` - document version `0.5.0` settlement order.

**Test command:**
- `dotnet run --project Share/Buqi.Simulation.Headless -- verify`

### Task 1: Red Tests For Expanded Effect Semantics

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Definition/BuqiTestSuite.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Definition/BuqiContractChecks.cs`

- [ ] **Step 1: Add fixture items for new effects**

Add fixture items named `heal`, `regen`, `poison`, `burn`, and `freeze` in `CreateFixtureProvider()` using the existing `Item()` and `Effect()` helpers:

```csharp
["heal"] = Definition("heal", 1, 40,
    Effect(BuqiTrigger.OnUse, BuqiEffect.Heal, BuqiTarget.Self, 12, "heal")),
["regen"] = Definition("regen", 1, 40,
    Effect(BuqiTrigger.OnUse, BuqiEffect.Regen, BuqiTarget.Self, 3, "regen", 30)),
["poison"] = Definition("poison", 1, 40,
    Effect(BuqiTrigger.OnUse, BuqiEffect.Poison, BuqiTarget.EnemyExecution, 4, "poison", 30)),
["burn"] = Definition("burn", 1, 40,
    Effect(BuqiTrigger.OnUse, BuqiEffect.Burn, BuqiTarget.EnemyExecution, 5, "burn", 30)),
["freeze"] = Definition("freeze", 1, 40,
    Effect(BuqiTrigger.OnUse, BuqiEffect.Freeze, BuqiTarget.ShortestCooldownEnemyItem, 10, "freeze", 10)),
```

- [ ] **Step 2: Add contract checks**

Append checks called from `RunAll()`:

```csharp
CheckHealAndRegen(provider, failures);
CheckPoisonBypassesShield(provider, failures);
CheckBurnUsesShield(provider, failures);
CheckFreezeStopsCooldown(provider, failures);
```

Each check must assert log reasons: `Heal`, `Regen`, `PoisonDamage`, `BurnDamage`, and `FreezeApplied`.

- [ ] **Step 3: Run red verification**

Run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless -- verify
```

Expected: build fails with `CS0117` because `BuqiEffect` does not yet define `Heal`, `Regen`, `Poison`, `Burn`, or `Freeze`.

### Task 2: Runtime State And Enum Support

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiTypes.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Model/BuqiRuntimeState.cs`

- [ ] **Step 1: Add enum values**

Extend `BuqiEffect` after `Noise`:

```csharp
Heal = 6,
Regen = 7,
Poison = 8,
Burn = 9,
Freeze = 10,
```

- [ ] **Step 2: Add runtime status types**

Add `TimedStatus`:

```csharp
public sealed class TimedStatus
{
    public BuqiEffect Effect;
    public int Amount;
    public int RemainingTicks;
    public int TickIntervalTicks = 10;
    public int TickProgressTicks;
    public string SourceInstanceId = string.Empty;
    public string EffectId = string.Empty;
    public string ReasonCode = string.Empty;
}
```

Add to `ItemState`:

```csharp
public int FrozenTicks;
```

Add to `SideState`:

```csharp
public int MaxExecution = 100;
public List<TimedStatus> Statuses = new List<TimedStatus>();
```

- [ ] **Step 3: Run green compile target**

Run:

```powershell
dotnet build Share/Buqi.Simulation.Headless
```

Expected: build still fails because simulator handling is not implemented. Missing switch handling is the desired next failure.

### Task 3: Deterministic Simulator Semantics

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Simulation/BuqiBattleSimulator.cs`

- [ ] **Step 1: Extend accumulator**

Add pending lists:

```csharp
public List<PendingAmount> Heal = new List<PendingAmount>();
public List<PendingAmount> PoisonTicks = new List<PendingAmount>();
public List<PendingAmount> BurnTicks = new List<PendingAmount>();
public List<TimedStatus> NewStatuses = new List<TimedStatus>();
```

- [ ] **Step 2: Add freeze gate to cooldown advance**

In cooldown advance, skip progress for frozen items:

```csharp
if (item.FrozenTicks > 0)
{
    item.FrozenTicks--;
    continue;
}
```

- [ ] **Step 3: Apply new effects in `ResolveEffect`**

Route effects:

```csharp
case BuqiEffect.Heal:
    AddPending(accumulators[actorSide], amount, actor, declaration, spec, accumulators[actorSide].Heal);
    break;
case BuqiEffect.Regen:
case BuqiEffect.Poison:
case BuqiEffect.Burn:
    AddStatus(targets.Side, actor, amount, declaration, spec, accumulators[targets.Side].NewStatuses);
    break;
case BuqiEffect.Freeze:
    ApplyFreeze(targets.Items, actor, amount, declaration, spec, ref nextSequence, log, tick);
    break;
```

- [ ] **Step 4: Tick side statuses before aggregation**

Add a deterministic status tick pass that emits pending `Regen`, `PoisonDamage`, and `BurnDamage` events once per 10 ticks.

- [ ] **Step 5: Apply aggregate order**

Order in `ApplyAggregate()`:

```text
shield -> normal attack and burn through shield -> heal/regen -> poison direct life loss -> overload/noise -> overtime
```

- [ ] **Step 6: Run contract verification**

Run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless -- verify
```

Expected: new effect checks pass or fail with specific amount/order differences. Fix simulator until they pass.

### Task 4: Validation And Hash Safety

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Rules/BuqiBoardValidator.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Rules/BuqiCrypto.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Config/BuqiConfigValidator.cs`

- [ ] **Step 1: Validate target policies**

Rules:

```text
Heal/Regen/Shield/Overload target Self.
Attack/Poison/Burn target EnemyExecution.
Freeze/Slow target enemy item selectors.
Haste/Charge target item selectors.
```

- [ ] **Step 2: Remove fixed Step 3 count gates**

Replace exact count checks with lower bounds and stage gates:

```text
items >= 9 for migration slice
refinements >= 3 for migration slice
echoes >= 6 for migration slice
build ids must be known by build table or enum
```

- [ ] **Step 3: Run validation**

Run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless -- verify
```

Expected: all contract checks pass before hash approval.

### Task 5: Luban Schema And Content DB

**Files:**
- Modify: `Design/Excel/GameHot/Datas/__enums__.xlsx`
- Modify: `Design/Excel/GameHot/Datas/Buqi/BuqiItem.xlsx`
- Modify: `Design/Excel/GameHot/Datas/Buqi/BuqiEcho.xlsx`
- Modify generated Luban files by running the existing Luban export, not by hand editing generated files.

- [ ] **Step 1: Add build enum rows**

Add build values:

```text
attack=4, shield=5, heal=6, poison=7, burn=8, freeze=9, overload=10
```

Keep legacy:

```text
fast=1, buffer=2, chain=3
```

- [ ] **Step 2: Add effect enum rows**

Add:

```text
Heal=6, Regen=7, Poison=8, Burn=9, Freeze=10
```

- [ ] **Step 3: Add first expanded content set**

Add 18-card sandbox rows covering attack, shield, heal, poison, burn, and freeze. Do not add the full 48-card set until deterministic tests and first playtest pass.

- [ ] **Step 4: Regenerate Luban**

Run the repository's GameHot Luban export command used by the project. If the export tool is unavailable in the current environment, record the exact missing dependency and do not hand-edit generated files as a substitute.

### Task 6: Documentation And Verification

**Files:**
- Modify: `docs/game-concepts/buqi-battle-contract.md`
- Modify: `docs/game-concepts/buqi-gameplay-spec.md`
- Modify: `docs/superpowers/specs/2026-08-05-buqi-effects-builds-db-design.md`

- [ ] **Step 1: Update battle contract to version 0.5.0**

Record the new settlement order and status meanings in the contract doc.

- [ ] **Step 2: Run final verification**

Run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless -- verify
dotnet run --project Share/Buqi.Simulation.Headless -- stress 1000
```

Expected: both commands print `ALL CHECKS PASSED`.

- [ ] **Step 3: Review git diff**

Run:

```powershell
git status --short
git diff --stat
```

Expected: only Buqi battle/config/docs/Excel files changed, plus generated Luban files if export succeeded.

## Self-Review

Spec coverage: the plan covers player-facing term migration, new effects, expanded build families, DB stage gates, deterministic simulation, validation, content DB, and documentation.

Placeholder scan: no task uses unresolved placeholder values. Content counts are explicit: migration 9, sandbox 18, full expanded set 48.

Type consistency: new runtime effect identifiers are `Heal`, `Regen`, `Poison`, `Burn`, and `Freeze`; legacy identifiers remain `Damage`, `Buffer`, `Haste`, `Delay`, `Charge`, and `Noise`.
