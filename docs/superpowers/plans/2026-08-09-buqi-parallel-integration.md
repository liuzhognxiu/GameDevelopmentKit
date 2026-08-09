# Buqi Parallel Work Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the accepted Buqi shop, Chinese text, save recovery, supply, event/training, content, and eight-slot link work into the main Unity checkout without losing newer main-tree adaptations.

**Architecture:** Keep `8e4b3498` as the integration baseline because it already contains the content, event/training, and link implementations. Commit the six shop files first, cherry-pick the two commits based directly on `8e4b3498`, then add only the missing supply bridge and its contract tests. Treat generated tables as source-derived artifacts and run Unity only after non-Unity verification is green.

**Tech Stack:** Unity 6000.3.18f1, C#, NUnit EditMode tests, Luban/ExcelExporter, .NET 8, Unity AgentBridge.

---

### Task 1: Preserve the shop interaction work

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BuqiStageWidgetBase.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/ShopWidget.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiSellZoneWidget.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBazaarInteractionTests.cs`

- [x] Run `dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore` and `dotnet build Unity/Game.Hot.Editor.csproj --no-restore`; require zero errors.
- [x] Stage exactly the six files above and commit `feat(buqi): wire shop drag-to-sell flow`.

### Task 2: Integrate accepted Chinese text and save recovery

**Commits:**
- `af5aae887f91b7b4e12f3a5dc62676b87175c2a4`
- `f4d63e29e326869092addaa8073785c8d316d5bd`

- [x] Cherry-pick the Chinese text commit, preserving the shop command-drop behavior in overlapping UI files.
- [x] Run the localization audit and confirm newly added shop text is rejected if any player-visible English remains.
- [x] Replace rejected shop text with Chinese/localized output and rerun the audit green.
- [x] Cherry-pick the save recovery commit, preserving both localized text and shop wiring in `BuqiRunShellForm.cs` and `BuqiFullUIBuilder.cs`.
- [x] Run save recovery and restart-policy tests.

### Task 3: Add the missing supply integration bridge

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Supply/BuqiSupplyIntegration.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Run/Supply/BuqiSupplyIntegration.cs.meta`
- Modify/Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiSupplyTestSuite.cs`

- [x] Apply the integration-contract tests from `dd6d5a18` and run them to observe failure because `BuqiSupplyIntegration` is absent.
- [x] Add the bridge from `dd6d5a18`, adapting only where current main supply types intentionally differ.
- [x] Run supply contracts and the three-build 10,000-seed verification green.

### Task 4: Verify already integrated modules

- [x] Compare `6ee8d429`, `ba69b16c`, and `45298be6` file blobs against `HEAD`.
- [x] Keep newer main versions where differences are formatting/analyzer or Unity `.meta` adaptations; do not duplicate cherry-picks.
- [x] Run content counts `42/8/4/12/24/72`, event/training 17 tests, link 12 tests, and 1,000-build link stress.

**Known follow-up:** The merchant supply source is now connected to the Demo shop and covered by source contracts plus controller contracts. The rich event/training engines remain present and tested, but the Demo controller still uses its legacy event encounter path; runtime event/training adapter work remains separate.

### Task 5: Export, build, and Unity QA

- [x] Run the project ExcelExporter check and verify generated tables are reproducible.
- [x] Build `Game.Hot.Code`, `Game.Hot.Editor`, and `Game.Hot.Buqi.Tests` with zero errors.
- [ ] Read the installed AgentBridge `AGENT.md`, obtain runtime commands with `list_commands`, run `BuqiFullUIBuilder.BuildAll`, and execute full Buqi EditMode tests.
- [ ] In Play Mode verify visible Chinese, incompatible-save restart, repeated buying with visible balance, drag-to-sell, board swapping, event/training flow, supply behavior, eight-slot links, nine-day progression, and tribulation entry.
- [ ] Commit only the intended integration files and generated Prefab/meta changes after reviewing `git diff --check` and the staged file list.

**Blocked verification:** AgentBridge instructions were read, but the active coordination constraint prohibits starting or driving Unity/AgentBridge. The remaining EditMode and Play Mode checks must wait for that constraint to be lifted.
