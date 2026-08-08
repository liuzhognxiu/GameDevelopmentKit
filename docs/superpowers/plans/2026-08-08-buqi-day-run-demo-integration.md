# Buqi Day Run Demo Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the linear Buqi demo flow with a playable deterministic day-run loop wired into the existing shell form, stage widgets, drag-deploy UI, simulator, replay, and save/resume flow.

**Architecture:** Keep `Run/Core`, `Run/Encounter`, `Run/Economy`, `Run/Battle`, and `Run/Settlement` as the rule/source-of-truth layer. Add a thin `Run/Integration` orchestration layer that owns runtime snapshots, stable ids, payload serialization, save checkpoints, and UI-facing view models. Adapt `DemoUI` and `BuqiRunShellForm` to consume that orchestration layer while preserving current prefabs and stage widget contracts where possible.

**Tech Stack:** C#, existing Buqi config/battle/replay code, NUnit EditMode tests, Unity prefab compatibility checks.

---

## Dependency Map

- `BuqiRunShellForm` currently owns the top-level UI loop and delegates per-phase rendering to `BuqiStageWidgetRegistry`.
- `BuqiStageWidgetRegistry` keys widgets by `BuqiUIDemoPhase`, so runtime integration must either preserve that enum surface or map new run phases onto compatible stage widgets.
- `BuqiUIDemoController` is still the old linear stub with starter/intel/prediction logic, 5-slot storage assumptions, and definition-id inventory; it is the main replacement point.
- `Run/Core` already enforces `Encounter -> PveBattle -> PvpBattle -> DaySettlement -> RunTerminal`, 3 encounters/day, 9 wins, 3 lives, 8 board slots, 8 storage slots, and draw-as-player-win settlement.
- `Run/Encounter`, `Run/Economy`, `Run/Battle`, and `Run/Settlement` already provide deterministic encounter freezing, item-instance economy, real battle simulation/replay, idempotent settlement, and fail-closed save validation, but they are not wired together yet.
- Existing UI/prefab tests still assert the old stage inventory (`StarterSelectionWidget`, `OpponentIntelWidget`, `PredictionWidget`); integration work must update those tests without breaking `BuqiRunShellForm`, `BattleForm`, or drag-deploy prefab compatibility.

## TDD Order

### Task 1: Integration red tests

**Files:**
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunDayLoopIntegrationTests.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiFullUIPrefabTests.cs`

- [ ] Write failing tests for a new run starting directly in `Encounter`, granting a deterministic legal starting build, freezing three daily encounters, driving `Encounter x3 -> PVE -> PVP -> DaySettlement -> next day`, using item instances, and never re-advancing on rejected duplicate/stale commands.
- [ ] Add failing save/resume tests covering startup restore, frozen encounter restore, successful economy checkpoint, confirmed deployment checkpoint, generated battle checkpoint, settled battle checkpoint, and day-settlement checkpoint.
- [ ] Update existing demo/shell prefab tests to fail against removed `StarterSelection`, `OpponentIntel`, and `Prediction` runtime flow while still preserving shell/stage prefab compatibility.

### Task 2: Runtime orchestration

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/*.cs`

- [ ] Add a run integration coordinator that holds `BuqiRunEconomySnapshot`, current frozen encounter, current battle session, current pending settlement, and serialized payloads for `Settlement`.
- [ ] Add deterministic starting-build rules from existing local config, with tests pinning the exact initial board/storage outcome and guaranteeing the first PVE request is valid.
- [ ] Add stable `commandId`, `battleId`, and `settlementId` generation derived from seed/day/phase/revision so replays and retries are idempotent.
- [ ] Add payload codecs for economy/encounter/battle integration state and save them through `BuqiRunSettlementCoordinator` without deleting incompatible old saves.

### Task 3: Demo/UI adaptation

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/*.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/*.cs`

- [ ] Replace the old linear demo controller with a controller/view-model adapter over `Run/Integration`.
- [ ] Preserve `BuqiRunShellForm`, `BattleForm`, and drag-deploy entry points, but stop entering runtime `StarterSelection`, `OpponentIntel`, and `Prediction`.
- [ ] Reuse existing stage widgets where possible by remapping them to encounter/shop/event/board/battle-summary/day-settlement/terminal states and surfacing item-instance-aware board/storage views.

### Task 4: Verification

**Files:**
- Modify only tests touched above if needed for final assertions.

- [ ] Run focused EditMode coverage for run core/battle/economy/encounter/settlement plus the new integration tests.
- [ ] Run affected prefab/controller tests to verify stage compatibility and shell-form behavior.
- [ ] Record remaining prefab/manual-Unity risks separately instead of masking them in code.
