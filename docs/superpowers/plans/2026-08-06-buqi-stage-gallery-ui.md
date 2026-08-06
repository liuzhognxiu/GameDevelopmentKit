# Buqi Stage Gallery UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement every non-battle Demo phase as an interactive prefab, connect them through `BuqiRunShellForm`, add the main-menu preview entry, and verify the complete 1920x1080 Gallery flow.

**Architecture:** Each phase has one render-only stage component implementing `IBuqiStageWidget`; the shell owns the controller and translates widget callbacks into commands. Stage widgets compose shared domain widgets and never write `BuqiUIDemoState`. The battle-replay phase opens the real `BattleForm`; summary facts may reopen it at a requested tick through typed open data.

**Tech Stack:** Unity 6000.3.21f1, GameHot/UGF, Unity UI + TMP, Run Shell/DemoState from the preceding plan, BattleForm from the battle plan, Editor prefab builder, CodeBind, NUnit EditMode, Unity Agent Bridge screenshots.

---

## File Map

Create stage contract and registry:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/IBuqiStageWidget.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BuqiStageWidgetRegistry.cs`

Create eleven stage scripts:

- `StarterSelectionWidget.cs`
- `OpponentIntelWidget.cs`
- `PreparationChoiceWidget.cs`
- `ShopWidget.cs`
- `EventWidget.cs`
- `ModificationWidget.cs`
- `BoardEditorWidget.cs`
- `PredictionWidget.cs`
- `BattleSummaryWidget.cs`
- `RoundSettlementWidget.cs`
- `RunTerminalWidget.cs`

Create eleven prefabs under:

- `Unity/Assets/Res/UI/UIPrefab/Buqi/Stages/`

Modify integration:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleForm.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- `Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab`

Create tests:

- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiStageWidgetTests.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiGalleryFlowTests.cs`

### Task 1: Stage Contract and Registry

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/IBuqiStageWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BuqiStageWidgetRegistry.cs`
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiStageWidgetTests.cs`

- [ ] **Step 1: Write the failing registry test**

```csharp
[Test]
public void Registry_MapsEveryRenderablePhaseExactlyOnce()
{
    BuqiUIDemoPhase[] expected =
    {
        BuqiUIDemoPhase.StarterSelection, BuqiUIDemoPhase.OpponentIntel,
        BuqiUIDemoPhase.PreparationChoice, BuqiUIDemoPhase.Shop,
        BuqiUIDemoPhase.Event, BuqiUIDemoPhase.Modification,
        BuqiUIDemoPhase.BoardEditor, BuqiUIDemoPhase.Prediction,
        BuqiUIDemoPhase.BattleSummary, BuqiUIDemoPhase.RoundSettlement,
        BuqiUIDemoPhase.RunTerminal,
    };
    BuqiStageWidgetRegistry registry = CreateRegistryWithFakeStages(expected);
    Assert.That(expected.All(registry.Contains), Is.True);
    Assert.That(registry.Count, Is.EqualTo(expected.Length));
}
```

`CreateRegistryWithFakeStages` creates one test-only `FakeStageWidget` per supplied phase; it does not depend on the concrete stage classes implemented in later tasks.

- [ ] **Step 2: Run and verify RED**

Expected: contract/registry missing.

- [ ] **Step 3: Implement the contract and duplicate-safe registry**

```csharp
public interface IBuqiStageWidget
{
    BuqiUIDemoPhase Phase { get; }
    GameObject Root { get; }
    void Render(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit,
        Action<ItemCardView> openDetails);
    void Clear();
}
```

The registry constructor accepts stage components, rejects nulls, duplicate phases, and `BattleReplay`, and deactivates every root. `Show` clears/deactivates the current stage before activating/rendering the target.

- [ ] **Step 4: Run and verify GREEN**

Expected: registry tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiStageWidgetTests.cs
git commit -m "feat(buqi): add UI stage registry"
```

### Task 2: Starter, Opponent, and Preparation Stages

**Files:**
- Create: `StarterSelectionWidget.cs`
- Create: `OpponentIntelWidget.cs`
- Create: `PreparationChoiceWidget.cs`
- Modify: `BuqiFullUIBuilder.cs`
- Modify: `BuqiStageWidgetTests.cs`

- [ ] **Step 1: Write failing render/callback tests**

Test three starter cards, exactly eight opponent slots plus three highlighted items, and three preparation choices. A click must submit the exact command and ID once; `Clear` must prevent later clicks from submitting stale callbacks.

- [ ] **Step 2: Run and verify RED**

Expected: stage types missing.

- [ ] **Step 3: Implement the three render-only components**

Starter cards show direction, three core equipment names, occupied slots, and tempo, with no power/win-rate/recommendation field. Opponent shows only public snapshot fields. Preparation shows visible cost, benefit category, and tradeoff.

- [ ] **Step 4: Build and bind prefabs**

Create stable `1112x824` roots. Use horizontal layouts whose child preferred sizes sum within the workspace; cap names at two lines and use zero letter spacing. Invoke Builder, CodeBind generation, compile, CodeBind serialization.

- [ ] **Step 5: Run tests and commit**

Expected: focused stage tests and prefab tests PASS. Commit with `feat(buqi): add opening demo UI stages`.

### Task 3: Shop, Event, and Modification Stages

**Files:**
- Create: `ShopWidget.cs`
- Create: `EventWidget.cs`
- Create: `ModificationWidget.cs`
- Modify: `BuqiFullUIBuilder.cs`
- Modify: `BuqiStageWidgetTests.cs`

- [ ] **Step 1: Write failing behavior tests**

Assert Shop renders four offers, coins, refresh price, lock state, and sell command; unaffordable offers are visibly disabled but details remain available. Event renders two or three mutually exclusive choices and result summary. Modification renders selected equipment, two or three options, and before/after values including both benefit and cost.

- [ ] **Step 2: Run and verify RED**

Expected: stage types missing.

- [ ] **Step 3: Implement exact command mapping**

Shop submits `BuyOffer`, `RefreshShop`, `ToggleShopLock`, or `SellItem`; Event submits `ChooseEvent`; Modification submits `ApplyModification`. Labels use `装备`, `金币`, `改造`, `护盾`, `过载`, and `充能`. No internal IDs are visible on primary cards.

- [ ] **Step 4: Build, bind, test, and commit**

Run Builder/CodeBind/compile and focused tests. Commit with `feat(buqi): add preparation service UI stages`.

### Task 4: Board Editor and Prediction Stages

**Files:**
- Create: `BoardEditorWidget.cs`
- Create: `PredictionWidget.cs`
- Modify: `BuqiFullUIBuilder.cs`
- Modify: `BuqiStageWidgetTests.cs`

- [ ] **Step 1: Write failing board tests**

Assert eight continuous board slots, five storage slots, source selection, green legal targets, red illegal targets with a reason, place/swap/return/sell/details/confirm actions, and no drag requirement. Prediction must show object/window/expected result, submit once, support confirm-gated skip, and render locked state after submit.

- [ ] **Step 2: Run and verify RED**

Expected: stage types missing.

- [ ] **Step 3: Implement selection-first board interaction**

First click submits `SelectBoardSource`. Target click submits `PlaceBoardItem` or `SwapBoardItems` based only on immutable view legality flags. The widget does not reimplement span validation. Keep all eight slot dimensions stable when selected/error markers appear.

- [ ] **Step 4: Implement prediction interaction**

Use three segmented option groups, not free text. Submit `SubmitPrediction`; skip opens `BuqiConfirmForm` and submits `SkipPrediction` only after confirm.

- [ ] **Step 5: Build, bind, test, and commit**

Expected: stage/prefab tests PASS. Commit with `feat(buqi): add board and prediction UI stages`.

### Task 5: Battle Bridge, Summary, Settlement, and Terminal

**Files:**
- Modify: `BuqiRunShellForm.cs`
- Modify: `BattleForm.cs`
- Create: `BattleSummaryWidget.cs`
- Create: `RoundSettlementWidget.cs`
- Create: `RunTerminalWidget.cs`
- Modify: `BuqiFullUIBuilder.cs`
- Modify: `BuqiStageWidgetTests.cs`

- [ ] **Step 1: Write failing flow tests**

Assert `BattleReplay` opens real `BattleForm` with `BattleReplayData`; closing it resumes shell at `BattleSummary`. Fact clicks open replay at a requested tick and select the log page. Settlement supports next round and review. Terminal supports return menu and restart preview for both win/loss variants.

- [ ] **Step 2: Run and verify RED**

Expected: stage bridge and terminal types missing.

- [ ] **Step 3: Add typed BattleForm open data**

```csharp
public sealed class BattleFormOpenData
{
    public BattleReplayData Replay;
    public int StartTick;
    public bool OpenAllLogs;
    public Action Closed;
}
```

Default `StartTick=0`. `OnClose` invokes and clears `Closed` once. `BattleForm` does not know DemoState.

- [ ] **Step 4: Implement the final three stages**

Summary shows layer-1 result and layer-2 evidence facts with event sequences; it never recommends strategy. Settlement labels wins/lives/coins/result and clearly marks values as Demo. Terminal shares one layout for win/loss and renders total battles, wins/losses, common build direction, key adjustment, and elapsed time.

- [ ] **Step 5: Build, bind, test, and commit**

Expected: tests PASS, all eleven stage prefabs exist. Commit with `feat(buqi): complete demo UI result stages`.

### Task 6: Connect Registry to Run Shell

**Files:**
- Modify: `BuqiRunShellForm.cs`
- Modify: `BuqiFullUIBuilder.cs`
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiGalleryFlowTests.cs`

- [ ] **Step 1: Write a failing complete flow test**

Feed a deterministic accepted command sequence from starter selection through terminal and assert exact phase order. Add alternate commands that visit Shop, Event, and Modification before Board Editor so every phase is reachable without random branching.

```csharp
[Test]
public void Gallery_AcceptedCommandsReachTerminal()
{
    BuqiUIDemoController controller = CreateController();
    foreach (BuqiUIDemoCommand command in CompleteGalleryCommands())
        Assert.That(controller.Execute(command).Accepted, Is.True, command.Type.ToString());
    Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RunTerminal));
}
```

- [ ] **Step 2: Run and verify RED**

Expected: one or more transition/registry integrations are missing.

- [ ] **Step 3: Complete shell rendering and battle handoff**

`Render` shows exactly one stage root. `BattleReplay` deactivates stage host, opens BattleForm, and resumes with `ContinueFromBattle` callback. Editor-only direct-stage open data may preload legal history but the player menu entry always starts at starter selection.

- [ ] **Step 4: Run all controller, stage, prefab, and flow tests**

Expected: `Game.Hot.Buqi.Tests` all PASS and compile errors remain zero.

- [ ] **Step 5: Commit**

Commit shell/registry/builder/tests/prefabs with `feat(buqi): connect complete UI gallery flow`.

### Task 7: Main Menu Preview Entry

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`
- Modify: `Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab`
- Modify: `BuqiFullUIBuilder.cs`
- Modify: `BuqiGalleryFlowTests.cs`

- [ ] **Step 1: Write a failing menu contract test**

Assert the menu has separate visible buttons `开始战斗` and `界面预览`, their click handlers target `BattleForm` and `BuqiRunShellForm`, and settings/about/quit retain existing handlers.

- [ ] **Step 2: Run and verify RED**

Expected: preview button/handler absent.

- [ ] **Step 3: Add the menu entry**

Add `OnPreviewButtonClick` that opens `UIFormId.BuqiRunShellForm`. The start button remains the Battle plan behavior. Update the prefab through `BuqiFullUIBuilder` or Unity serialized APIs, keep button dimensions stable, and place the new entry without obscuring existing commands.

- [ ] **Step 4: Compile, bind, test, and commit**

Expected: menu contract and all existing tests PASS. Commit with `feat(buqi): add full UI preview menu entry`.

### Task 8: Sixteen-State Unity Acceptance

- [ ] **Step 1: Compile and run all EditMode tests**

Clear Console, recompile, require zero errors, run `Game.Hot.Buqi.Tests`, and require all passed.

- [ ] **Step 2: Set 1920x1080 and preserve restore state**

Use Agent Bridge `set_game_view_resolution`; keep the returned restore token until all captures finish.

- [ ] **Step 3: Capture the complete set**

Drive the real UI and capture:

```text
01-main-menu.jpg
02-starter-selection.jpg
03-opponent-intel.jpg
04-preparation-choice.jpg
05-shop.jpg
06-event.jpg
07-modification.jpg
08-board-editor.jpg
09-prediction.jpg
10-battle-replay.jpg
11-battle-summary.jpg
12-round-settlement.jpg
13-run-terminal.jpg
14-item-detail.jpg
15-confirm.jpg
16-error-and-loading.jpg
```

- [ ] **Step 4: Inspect every capture**

For each image verify nonblank pixels, no incoherent overlap, no clipped longest Chinese label, stable 8-slot tracks where applicable, visible focus/selection, semantic colors plus non-color markers, and no nested-card layout.

- [ ] **Step 5: Exercise lifecycle and errors**

Close/reopen preview twice, trigger insufficient coins, illegal board placement, prediction skip confirm, loading, and one diagnostic error. Verify controls do not move and no stale callback fires.

- [ ] **Step 6: Restore editor state and verify logs**

Restore Game View with the token, stop Play Mode, restore the original scene, and require `search_logs(type=error)` to return zero unexpected entries.

- [ ] **Step 7: Run final non-Unity checks**

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
git diff --check
git status --short
```

Expected: verifier passes; only intended UI/code/config/prefab/test files are staged; concurrent gameplay documents, `.wbtmp.xlsx`, Bridge slots, and `output/` remain uncommitted.

- [ ] **Step 8: Commit acceptance fixes**

Commit only concrete fixes found by the real run with `fix(buqi): finish full demo UI acceptance`.
