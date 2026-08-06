# Buqi Drag Deploy UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a 1920x1080 full-screen drag deployment form that supports legal preview, reversible drag/click placement, reset/cancel, and validated synchronization back to the current Buqi RunShell board editor.

**Architecture:** A Unity-independent `BuqiDragDeployController` owns immutable eight-slot board and five-slot storage snapshots. Unity widgets translate pointer/click intent into source and destination references but never mutate slot state. `BuqiDragDeployForm` renders controller output and confirms one complete snapshot; `BuqiRunShellForm` then submits `ApplyDeployment` to `BuqiUIDemoController`, which revalidates the snapshot before replacing its Demo-only state.

**Tech Stack:** Unity 6000.3.21f1, GameHot/UGF `StarForceUIForm`, Unity UI event interfaces, Luban UI configuration, NUnit EditMode, Unity Agent Bridge, openpyxl.

---

## File Map

Create the pure deployment model:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/Deployment/BuqiDeploymentTypes.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/Deployment/BuqiDragDeployController.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployControllerTests.cs`

Create runtime UI:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiDraggableItemWidget.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiDeploySlotWidget.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiDragDeployForm.cs`

Create builder and prefab verification:

- `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiDragDeployUIBuilder.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployPrefabTests.cs`
- `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDraggableItemWidget.prefab`
- `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDeploySlotWidget.prefab`
- `Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiDragDeployForm.prefab`

Modify RunShell and generated configuration:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoState.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BoardEditorWidget.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`
- `Design/Excel/GameHot/Datas/Game/UI.xlsx`
- generated `dtuiform.json`, `dtuiform.bytes`, and UI form ID files.

### Task 1: Pure Immutable Deployment Controller

**Files:**
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployControllerTests.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/Deployment/BuqiDeploymentTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/Deployment/BuqiDragDeployController.cs`

- [ ] **Step 1: Write the failing controller tests**

Cover deterministic creation, storage-to-board placement, multi-slot span preview, overlap rejection, out-of-range rejection, board-to-board movement, board-to-storage movement, reset, and stale source rejection. For every rejected move, retain the exact same `View` instance.

```csharp
[Test]
public void TryMove_OverlapIsRejectedWithoutMutation()
{
    BuqiDragDeployController controller = CreateController();
    Assert.That(controller.TryMove(Storage(0), Board(1)).Accepted, Is.True);
    BuqiDeploymentSnapshot before = controller.View;

    BuqiDeploymentCommandResult result = controller.TryMove(Storage(1), Board(1));

    Assert.That(result.Accepted, Is.False);
    Assert.That(result.Reason, Is.Not.Empty);
    Assert.That(controller.View, Is.SameAs(before));
}
```

- [ ] **Step 2: Run the focused class and verify RED**

Use Agent Bridge `run_tests` with mode `edit` and group `Game.Hot.Buqi.Tests.BuqiDragDeployControllerTests`.

Expected: FAIL because deployment types do not exist.

- [ ] **Step 3: Define source/destination and immutable view types**

Define `BuqiDeploymentArea` (`Board`, `Storage`), `BuqiDeploymentSlotRef`, `BuqiDeploymentPlacement`, `BuqiDeploymentTargetPreview`, `BuqiDeploymentSnapshot`, and `BuqiDeploymentCommandResult`. Copy every input/list into private arrays and expose them through `IReadOnlyList<T>`. A board item is represented once by item ID plus anchor/span; continuation slots are derived during snapshot construction.

- [ ] **Step 4: Implement validated copy-on-command moves**

`Create` requires exactly eight board slots and five storage slots and validates each item against `BuqiUIDemoCatalog`. `Preview` and `TryMove` validate source ownership, target range, item span, overlap, duplicate placement, and storage capacity. Accepted commands replace `View`; rejected commands preserve the existing instance. `Reset` restores a deep copy of the opening snapshot.

- [ ] **Step 5: Run tests and verify GREEN**

Expected: all deployment controller tests PASS and production files have no `UnityEngine` dependency.

- [ ] **Step 6: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/Deployment Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployControllerTests.cs
git commit -m "feat(buqi): add immutable drag deployment model"
```

### Task 2: RunShell Deployment Contract

**Files:**
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoState.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BoardEditorWidget.cs`

- [ ] **Step 1: Add failing synchronization tests**

Add tests that `OpenDragDeploy` is accepted only in `BoardEditor`, valid `ApplyDeployment` replaces board/storage in one immutable transition, malformed slot counts and unknown/overlapping item IDs are rejected, and every rejected synchronization preserves `View` by reference.

- [ ] **Step 2: Run the controller class and verify RED**

Use Agent Bridge `run_tests` for `Game.Hot.Buqi.Tests.BuqiUIDemoControllerTests`.

Expected: FAIL because the command types and snapshot payload are absent.

- [ ] **Step 3: Add typed command payload and validation**

Add `OpenDragDeploy` and `ApplyDeployment` to `BuqiUIDemoCommandType`. Add a `BuqiDeploymentSnapshot Deployment` field to `BuqiUIDemoCommand`. Handle `OpenDragDeploy` as a validated UI intent without advancing phase. Handle `ApplyDeployment` only in `BoardEditor`, rebuilding and validating through the deployment model before copying board and storage into the cloned demo state.

- [ ] **Step 4: Wire the board editor intent**

Give `BoardEditorWidget` one explicit `OpenDragDeploy` action while retaining the existing simple slot actions for demo inspection. Do not open Unity UI from the stage widget.

- [ ] **Step 5: Run tests and verify GREEN**

Expected: controller synchronization tests and the existing demo flow tests PASS.

- [ ] **Step 6: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Stages/BoardEditorWidget.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs
git commit -m "feat(buqi): add drag deployment sync contract"
```

### Task 3: Drag Widgets and Form Runtime

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiDraggableItemWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiDeploySlotWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiDragDeployForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`

- [ ] **Step 1: Add failing lifecycle/interaction tests**

Test typed open data rejection, render count `8 + 5`, click-source/click-target parity with `TryMove`, reset behavior, confirm callback invoked once, cancel without callback, and close/reopen clearing old callbacks, selection, previews, and drag visuals.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: FAIL because widgets/form are absent.

- [ ] **Step 3: Implement `BuqiDraggableItemWidget`**

Implement `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler`. The widget reports its immutable `BuqiDeploymentSlotRef`, creates no model changes, disables its own raycast while dragging, and delegates drag visual ownership to the form. `Clear` removes all callbacks and restores raycast/transform state.

- [ ] **Step 4: Implement `BuqiDeploySlotWidget`**

Implement pointer enter/exit, click, and drop forwarding. Render normal, selected, legal, illegal, continuation, and locked states with label/symbol plus color. Fixed layout dimensions must not depend on current label length.

- [ ] **Step 5: Implement `BuqiDragDeployForm` and typed data**

```csharp
public sealed class BuqiDragDeployOpenData
{
    public BuqiUIDemoCatalog Catalog;
    public IReadOnlyList<BuqiDemoItemView> Board;
    public IReadOnlyList<BuqiDemoItemView> Storage;
    public Action<BuqiDeploymentSnapshot> Confirmed;
}
```

The form owns the pure controller, top-level drag layer, details panel, validation panel, reset/cancel/confirm buttons, and click fallback. Invalid drop renders the reason and original immutable view; cancel closes without callback; confirm invokes the captured delegate once then closes.

- [ ] **Step 6: Integrate RunShell interception**

`BuqiRunShellForm.Submit` intercepts accepted `OpenDragDeploy`, opens UI ID `108` with the current catalog/view, and supplies a callback that submits `ApplyDeployment`. Rejected apply results remain visible in RunShell status. Closing the deploy form without confirmation leaves RunShell unchanged.

- [ ] **Step 7: Compile and run focused tests**

Use Agent Bridge `refresh`, require zero compile errors from `get_compile_result`, then run the interaction/lifecycle test groups.

- [ ] **Step 8: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI Unity/Assets/Tests/GameHot/Buqi/EditMode
git commit -m "feat(buqi): implement drag deployment form"
```

### Task 4: Builder-Generated Prefabs

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiDragDeployUIBuilder.cs`
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployPrefabTests.cs`
- Generate: `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDraggableItemWidget.prefab`
- Generate: `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDeploySlotWidget.prefab`
- Generate: `Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiDragDeployForm.prefab`

- [ ] **Step 1: Write string/reflection-based prefab tests and verify RED**

The test must compile before the prefabs exist. Assert exact paths, matching root components, complete serialized references, 1920x1080 root, five stable storage slots, eight stable board slots, right details/feedback region, top drag layer, and reset/cancel/confirm buttons.

- [ ] **Step 2: Implement the Editor Builder**

Add menus `Game/Buqi/Rebuild Drag Deploy UI` and `Game/Buqi/Open Drag Deploy UI Demo`. Build both reusable component prefabs first, then the full form from them. Use Unity serialization APIs and `PrefabUtility`; do not edit prefab YAML manually.

- [ ] **Step 3: Compile and invoke the builder through Agent Bridge**

Run `refresh`, require zero compile errors, then `invoke_menu` with path `Game/Buqi/Rebuild Drag Deploy UI`. Refresh again and require all generated assets to import cleanly.

- [ ] **Step 4: Run prefab tests and verify GREEN**

Use Agent Bridge `run_tests` for `Game.Hot.Buqi.Tests.BuqiDragDeployPrefabTests` and the form lifecycle tests.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiDragDeployUIBuilder.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiDragDeployPrefabTests.cs Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDraggableItemWidget.prefab Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiDeploySlotWidget.prefab Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiDragDeployForm.prefab
git commit -m "feat(buqi): build drag deployment prefabs"
```

### Task 5: Register UI ID 108 Through Luban

**Files:**
- Modify: `Design/Excel/GameHot/Datas/Game/UI.xlsx`
- Regenerate: `Unity/Assets/Res/Editor/Luban/dtuiform.json`
- Regenerate: `Unity/Assets/Res/Luban/dtuiform.bytes`
- Regenerate: `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`
- Regenerate: `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Generate/UGF/UGFUIFormId.cs`

- [ ] **Step 1: Extend the registration test and verify RED**

Assert `UIFormId.BuqiDragDeployForm == 108` and exact asset path `Hot/Buqi/BuqiDragDeployForm`.

- [ ] **Step 2: Append the structured workbook row**

Use openpyxl, preserve workbook metadata/style, and reject conflicting ID/name values:

```text
108, BuqiDragDeployForm, 拖拽上阵, Hot/Buqi/BuqiDragDeployForm, Pop, false, true
```

- [ ] **Step 3: Export from the required working directory**

```powershell
Set-Location E:\Project\GameDevelopmentKit\Bin
.\Tool.exe --AppType=ExcelExporter --Console=1
```

Expected: exporter exits `0`, validates the prefab path, and regenerates JSON/bytes/IDs.

- [ ] **Step 4: Refresh Unity and run registration tests**

Require zero compile errors and passing UI registration/prefab tests.

- [ ] **Step 5: Commit only workbook and generated outputs**

```powershell
git add Design/Excel/GameHot/Datas/Game/UI.xlsx Unity/Assets/Res/Editor/Luban/dtuiform.json Unity/Assets/Res/Luban/dtuiform.bytes Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Generate/UGF/UGFUIFormId.cs
git commit -m "feat(buqi): register drag deployment form"
```

### Task 6: 1920x1080 Acceptance and Full Regression

- [ ] **Step 1: Run the complete Buqi EditMode suite**

Use Agent Bridge `run_tests` with group `Game.Hot.Buqi.Tests`, then `get_test_result` with `includePassed=true` and `limit=200`.

Expected: every test passes, including existing battle replay and full RunShell tests.

- [ ] **Step 2: Open the form in Unity at 1920x1080**

Invoke `Game/Buqi/Open Drag Deploy UI Demo`. Verify storage, board, details, feedback, command bar, and drag layer are visible and fixed; no text overlaps or resizes slots.

- [ ] **Step 3: Exercise both interaction paths**

Drag one single-slot and one multi-slot item, reject one overlap/out-of-range target and verify rollback, move a board item, return one to storage, reset, then repeat one move using click-source/click-target. Confirm and verify RunShell board/storage updates once.

- [ ] **Step 4: Verify cancel/reopen isolation**

Open, mutate locally, cancel, and verify RunShell is unchanged. Reopen and verify no prior selection, callback, preview, or drag visual remains.

- [ ] **Step 5: Final hygiene and commit**

Require zero Unity Console errors, run `git diff --check`, inspect `git status --short`, and stage no unrelated knowledge-base changes, workbook temp files, `.superpowers`, or `output/` content.
