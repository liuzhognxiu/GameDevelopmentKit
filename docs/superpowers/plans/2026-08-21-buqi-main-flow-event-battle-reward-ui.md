# Buqi Main Flow, Event, Battle, and Reward UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the approved six-period run flow to authored route nodes, the production event/training runtime, explicit reward and level-up stages, blocking battle results, and a mouse-only pause overlay.

**Architecture:** Keep `BuqiRunController` as the authoritative nine-day state machine. Extend the existing Demo orchestrator with a persisted presentation payload and a production catalog adapter for `BuqiRunOperationFlowAdapter`; add focused reward and route policies whose commands are idempotent. Render the new presentation states with dedicated stage widgets and overlays while leaving shop, supply, merchant, and item configuration code unchanged.

**Tech Stack:** Unity 6000 C#, existing Buqi deterministic run/economy/event/training/battle services, Unity UI, NUnit EditMode contract tests, non-Unity `dotnet build` and headless verification.

---

### Task 1: Persisted route, transition, and operation runtime

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunAuthoredOperationCatalog.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunDemoIntegration.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveData.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveCodec.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunMainFlowTests.cs`

- [ ] **Step 1: Write failing tests for frozen 2-3 node routes**

Assert that opening/previewing a period never changes candidate ids or RNG, candidates expose benefit/cost/condition text, and selecting a node consumes the current operation exactly once.

- [ ] **Step 2: Verify RED**

Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj -v:minimal` when the generated project exists. Expected: compile failures for the missing route view and commands; otherwise record the Unity EditMode run as pending without launching Unity.

- [ ] **Step 3: Implement catalog conversion and orchestrator wiring**

Create an adapter implementing `IBuqiRunEventDefinitionCatalog`, `IBuqiRunEventItemCatalog`, and `IBuqiRunTrainingDefinitionCatalog`. Translate authored event/training rows into the existing runtime definitions, use configured item tags for targets, and expose localized names/summaries without editing the source configuration.

- [ ] **Step 4: Write failing event/training integration tests**

Cover coins, lives, items, experience, upgrade/refinement, scheduled returns, unavailable targets, insufficient costs, duplicate resolution ids, and restore of a frozen pending event.

- [ ] **Step 5: Implement event/training commands and save recovery**

Store `BuqiRunEventSaveData` plus the frozen route/presentation payload in `BuqiRunSaveData`. Synchronize the operation runtime after every economy mutation and reject mismatched or stale restores.

- [ ] **Step 6: Verify GREEN and commit**

Run focused compilation/headless checks, then commit only task-owned files with `feat(buqi): connect route event and training flow`.

### Task 2: Explicit rewards and level progression

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunRewardService.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunDemoIntegration.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunRewardFlowTests.cs`

- [ ] **Step 1: Write failing reward-stage tests**

Require 2-4 deterministic candidates from controller options, preview without mutation, explicit claim, a distinct level-up marker when the experience threshold is crossed, and no double grant after repeated claim or restore.

- [ ] **Step 2: Verify RED**

Expected failures identify missing `RewardSelection`, `PreviewReward`, and `ClaimReward` contracts.

- [ ] **Step 3: Implement minimal deterministic reward policy**

Generate candidate ids from the settled battle id and configured candidate count. Apply coin, item, experience, upgrade, or refinement rewards through cloned economy/event state, append an idempotency record, and persist the chosen/claimed state before advancing.

- [ ] **Step 4: Verify GREEN and commit**

Run focused compilation/headless checks and commit with `feat(buqi): add explicit reward and level stages`.

### Task 3: Battle result and pause command state

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`

- [ ] **Step 1: Write failing overlay-state tests**

Assert battle completion exposes a blocking win/loss result, rejects movement/trading commands, `ContinueBattleResult` is idempotent, pause blocks run commands, resume restores the same view, and exit only raises an exit request.

- [ ] **Step 2: Verify RED**

Expected failures identify the missing overlay flags and pause commands.

- [ ] **Step 3: Implement command gates**

Add `IsPaused`, `BattleResultVisible`, `InputLocked`, and `ExitRequested` to the immutable view. Permit only preview and approved replay commands during battle/result states; do not add polling or keyboard paths.

- [ ] **Step 4: Verify GREEN and commit**

Commit with `feat(buqi): gate battle results and pause flow` after focused checks pass.

### Task 4: Dedicated stage widgets and overlays

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/PeriodTransitionWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/TrainingWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/RewardSelectionWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiRouteNodeCardWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleResultOverlay.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiRunPauseOverlay.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/OperationChoiceWidget.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/EventWidget.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs` (non-shop wiring only)
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiMainFlowUIPrefabTests.cs`

- [ ] **Step 1: Write failing source/prefab contract tests**

Require enemy top/player bottom, life/shield/cooldown labels, trigger/float feedback, hover details during battle, 2-3 mutually exclusive route cards, separate reward preview/claim, blocking result overlay, pause resume/exit, and no keyboard polling.

- [ ] **Step 2: Verify RED**

Expected failures name absent widget components and serialized bindings.

- [ ] **Step 3: Implement widgets and form wiring**

Use fixed action areas and stable layout dimensions. Battle and result overlays disable mutation controls but retain hover details. `BuqiRunShellForm` changes are limited to phase/overlay dispatch and form exit.

- [ ] **Step 4: Verify GREEN and commit RunShell separately**

Commit all non-shell UI first, then commit the isolated `BuqiRunShellForm` wiring as `feat(buqi): wire main flow overlays in run shell`.

### Task 5: Builder, prefab contracts, and final verification

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiRunFlowUIBuilder.cs`
- Create/Modify: dedicated Buqi stage/overlay prefabs under `Unity/Assets/Res/UI/UIPrefab/Buqi` and `Unity/Assets/Res/UI/UIForm/Hot/Buqi`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiMainFlowUIPrefabTests.cs`

- [ ] **Step 1: Add builder contracts before implementation**

Assert all dedicated prefab paths, component types, button labels/icons, serialized overlay bindings, route card capacity, and battle hierarchy names.

- [ ] **Step 2: Implement the builder without running Unity**

Generate period, route, training, reward, result, and pause surfaces using existing builder helpers. Do not touch `ShopWidget`, `OfferCardWidget`, supply classes, or merchant/item configuration.

- [ ] **Step 3: Run non-Unity verification**

Run available `dotnet build` targets, `dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify`, relevant PowerShell data checks, and forbidden-path/keyboard-polling diffs.

- [ ] **Step 4: Final review and commit**

Review tracked diffs, ensure no `output/imagegen` files, commit remaining owned files, and report Unity-only prefab generation, EditMode, interaction, resolution, and Console checks as pending acceptance.
