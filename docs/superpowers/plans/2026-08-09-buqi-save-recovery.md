# 不器 Demo 存档恢复与重开实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让明确不可兼容的旧存档自动原子替换为当前版本新局，并修复错误态/终局重开交互。

**Architecture:** save codec 返回结构化 schema 不支持类别，orchestrator 在完成结构验证后仅对该类别或无迁移的内容版本不一致自动创建新局；所有其它失败保留原存档。Shell form 将按钮和受限 R 快捷键都委托到一个 Restart 方法，错误态可在 controller 创建失败后重新创建 controller。

**Tech Stack:** Unity C#、NUnit EditMode、现有 `IBuqiRunStore` 原子写入、BuqiRunSaveCodec。

---

### Task 1: 存档判定 RED

**Files:**
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunDayLoopIntegrationTests.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunSettlementTests.cs`

- [ ] **Step 1: Add failing tests** for old content automatic recovery/current version write, unsupported schema automatic recovery, v2/v3 migration progress retention, current valid load, read/write I/O preservation, uncertain payload preservation, pending settlement preservation, and first-stage interaction.
- [ ] **Step 2: Run the focused Unity test entry or available isolated non-Unity build and record RED.** Expected new recovery tests fail because initialization still returns the old mismatch/unsupported error and no controller exists.

### Task 2: Codec classification GREEN

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveCodec.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Settlement/BuqiRunSaveData.cs`

- [ ] **Step 1: Add `BuqiRunSaveFailureKind` and an overload preserving the old `TryFromJson` signature.** Unknown schema returns `UnsupportedVersion`; supported v2/v3 still migrate before validation; all other failures remain `InvalidData`.
- [ ] **Step 2: Run codec tests and confirm GREEN.**

### Task 3: Safe initialization recovery GREEN

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Integration/BuqiRunDemoIntegration.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`

- [ ] **Step 1: Handle structured unsupported schema and validated content mismatch by calling existing `TryStartNewRun`; do not call `TryDelete`.** Translate initialization failures into Chinese and preserve all non-recoverable bytes.
- [ ] **Step 2: Add a controller factory path that can create a fresh run after an initialization error without duplicating run creation logic.**
- [ ] **Step 3: Run integration tests and confirm GREEN, including failed replacement writes leaving old JSON unchanged.**

### Task 4: Restart input and UI GREEN

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiFullUIPrefabTests.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`

- [ ] **Step 1: Add a single restart dispatch path and tests for click/R parity, allowed error/terminal states, and ignored normal-phase R.**
- [ ] **Step 2: Implement `Update` gating and error-state controller recreation.** Hide the restart button during normal operation, render Chinese “重新开始” with an unobtrusive R hint, clear error state, re-render resources/stages after success.
- [ ] **Step 3: Run UI/controller tests and confirm GREEN.**

### Task 5: Verification and commit

**Files:**
- No additional production files.

- [ ] **Step 1: Run `dotnet build` for the available `Game.Hot.Buqi.Tests` and `Game.Hot.Editor` project paths; run any isolated non-Unity related tests available.**
- [ ] **Step 2: Inspect `git diff`, ensure no main Unity/AgentBridge files were touched, and record Unity click verification as pending for the parent task.**
- [ ] **Step 3: Commit the worktree with a focused message and report root cause, boundary, evidence, commit, and portable files.**
