# Buqi Run Shell UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deterministic 1920x1080 `BuqiRunShellForm` with reusable domain widgets and shared detail/confirm/message forms for the complete UI preview flow.

**Architecture:** `BuqiUIDemoController` is a pure C# state machine whose immutable state is recreated for every preview session; it does not write formal RunState, economy, save data, or random cursors. `BuqiRunShellForm` renders one phase at a time and owns shared header, phase rail, context rail, and command bar. Domain widgets expose rendering and click callbacks only; later stage widgets compose them without mutating state directly.

**Tech Stack:** Unity 6000.3.21f1, GameHot/UGF `StarForceUIForm`, Unity UI + TMP, existing ComponentLibrary prefabs, CodeBind, Luban UI table, NUnit EditMode, Unity Agent Bridge.

---

## File Map

Create deterministic preview model files:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`: phase, command, result, item, offer, opponent, fact, and immutable view models.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoCatalog.cs`: fixed preview samples derived from current `BuqiConfigCatalog`.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoState.cs`: complete session state.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`: validation, command application, history, and phase navigation.

Create UIForms:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiItemDetailForm.cs`.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiConfirmForm.cs`.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiMessageForm.cs`.

Create reusable domain widgets under `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/`:

- `BoardSlotWidget.cs`
- `ResourceChipWidget.cs`
- `PhaseStepWidget.cs`
- `ChoiceCardWidget.cs`
- `OfferCardWidget.cs`
- `OpponentSnapshotWidget.cs`
- `FactRowWidget.cs`

Reuse Battle plan widgets:

- `ItemCardWidget.cs`
- `BattleLogWidget.cs`

Create or modify editor/test/config files:

- `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`.
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`.
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs`.
- `Design/Excel/GameHot/Datas/Game/UI.xlsx`: IDs `104..107`.
- Generated `UIFormId.cs` and CodeBind `*.Bind.cs`.

### Task 1: Demo Types and Deterministic Initial State

**Files:**
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoTypes.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoCatalog.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoState.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`

- [ ] **Step 1: Write the failing initial-state test**

```csharp
[Test]
public void Create_AlwaysReturnsSameStarterState()
{
    BuqiUIDemoCatalog catalog = CreateCatalog();
    BuqiUIDemoController first = BuqiUIDemoController.Create(catalog);
    BuqiUIDemoController second = BuqiUIDemoController.Create(catalog);

    Assert.That(first.View.Phase, Is.EqualTo(BuqiUIDemoPhase.StarterSelection));
    Assert.That(first.View.Coins, Is.EqualTo(second.View.Coins));
    Assert.That(first.View.StarterChoices.Select(x => x.Id),
        Is.EqualTo(second.View.StarterChoices.Select(x => x.Id)));
    Assert.That(first.View.BoardSlots.Count, Is.EqualTo(8));
    Assert.That(first.View.StorageSlots.Count, Is.EqualTo(5));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Use Agent Bridge `run_tests` for `Game.Hot.Buqi.Tests.BuqiUIDemoControllerTests.Create_AlwaysReturnsSameStarterState`.

Expected: FAIL because the demo types are absent.

- [ ] **Step 3: Define the complete demo API**

```csharp
public enum BuqiUIDemoPhase
{
    StarterSelection,
    OpponentIntel,
    PreparationChoice,
    Shop,
    Event,
    Modification,
    BoardEditor,
    Prediction,
    BattleReplay,
    BattleSummary,
    RoundSettlement,
    RunTerminal,
}

public enum BuqiUIDemoCommandType
{
    SelectStarter, ConfirmStarter, ContinueFromIntel, ChoosePreparation,
    BuyOffer, RefreshShop, ToggleShopLock, ChooseEvent, ApplyModification,
    SelectBoardSource, PlaceBoardItem, SwapBoardItems, ReturnToStorage,
    SellItem, ConfirmBuild, SubmitPrediction, SkipPrediction,
    OpenBattleReplay, ContinueFromBattle, ContinueRound, FinishRun, Back,
}

public sealed class BuqiUIDemoCommand
{
    public BuqiUIDemoCommandType Type;
    public string PrimaryId;
    public string SecondaryId;
    public int Slot;
}

public sealed class BuqiUIDemoCommandResult
{
    public bool Accepted;
    public string Reason;
    public BuqiUIDemoView View;
}
```

`BuqiUIDemoView` contains phase, coins, wins, lives, round, current/visited phases, 8 board slots, 5 storage slots, starter choices, preparation choices, four shop offers, event choices, modifications, opponent snapshot, prediction, battle summary, settlement, terminal summary, context title/body, primary/secondary command labels, and error/loading state. Lists are exposed as `IReadOnlyList<T>` and copied on every accepted command.

Add the minimal `BuqiUIDemoController.Create(BuqiUIDemoCatalog)` factory and read-only `View` member needed by the initial-state test. Command execution remains out of scope until Task 2.

- [ ] **Step 4: Implement deterministic catalog construction**

Sort catalog items, refinements, and echoes by IDs with `StringComparer.Ordinal`. Build three starter choices, four shop offers, one event with three options, three modifications, and one opponent snapshot. Return a diagnostic error when fewer than the required rows exist; do not use Unity RNG.

- [ ] **Step 5: Run the focused test and verify GREEN**

Expected: PASS and no UnityEngine dependency in `DemoUI` files.

- [ ] **Step 6: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs
git commit -m "feat(buqi): add deterministic UI demo state"
```

### Task 2: Command Validation, Phase Navigation, and Reset

**Files:**
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoController.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI/BuqiUIDemoState.cs`

- [ ] **Step 1: Add failing command tests**

Add one test per behavior: invalid phase command is rejected without state mutation; starter selection advances only after confirmation; insufficient coins has a specific reason; locked shop survives refresh; full board/storage rejects buy; selected source plus target performs placement; invalid span is rejected; prediction locks after submit; back restores the prior immutable snapshot; creating a second controller resets all data.

```csharp
[Test]
public void RejectedCommand_DoesNotMutateState()
{
    BuqiUIDemoController controller = CreateController();
    BuqiUIDemoView before = controller.View;

    BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
    {
        Type = BuqiUIDemoCommandType.BuyOffer,
        PrimaryId = "offer-01",
    });

    Assert.That(result.Accepted, Is.False);
    Assert.That(result.Reason, Is.EqualTo("当前阶段不能购买装备"));
    Assert.That(controller.View, Is.SameAs(before));
}
```

- [ ] **Step 2: Run the class and verify RED**

Expected: command tests FAIL because `Execute` is absent.

- [ ] **Step 3: Implement copy-on-command state transitions**

Use a command-handler dictionary keyed by `BuqiUIDemoCommandType`. Every handler validates phase and payload before cloning state. Accepted commands append the previous state to a private history stack and replace `View`; rejected commands return the same `View` instance. `Back` pops one state and never jumps before starter selection. Use exact player-facing terminology from `docs/game-concepts/gameplay-terminology.md`.

- [ ] **Step 4: Run tests and verify GREEN**

Expected: all controller tests PASS; repeated initial state + command sequence yields equal views.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/DemoUI Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiUIDemoControllerTests.cs
git commit -m "feat(buqi): implement UI demo command flow"
```

### Task 3: Shared Domain Widgets

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/*.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs`

- [ ] **Step 1: Write failing domain-widget prefab tests**

Assert nine domain prefabs exist under `Assets/Res/UI/UIPrefab/Buqi`, each root has its matching component, fixed preferred dimensions, and complete serialized references. Include the two Battle plan prefabs in the count.

- [ ] **Step 2: Run and verify RED**

Expected: seven new domain prefabs are absent.

- [ ] **Step 3: Implement render-only widget APIs**

Each widget owns no `BuqiUIDemoState`. Use these signatures:

```csharp
public void Render(BoardSlotView view, Action<int> onClick);
public void Render(ResourceChipView view);
public void Render(PhaseStepView view, Action<BuqiUIDemoPhase> onClick);
public void Render(ChoiceCardView view, Action<string> onClick);
public void Render(OfferCardView view, Action<string> onBuy, Action<string> onDetails);
public void Render(OpponentSnapshotView view, Action<string> onItemDetails);
public void Render(FactRowView view, Action<int> onJumpToTick);
public void Clear();
```

`Clear` removes callbacks and resets all visual states. Color is never the only state channel: selected/locked/invalid/current also changes label, outline, or symbol.

- [ ] **Step 4: Build the seven new prefabs**

`BuqiFullUIBuilder` creates stable sizes and uses existing ComponentLibrary prefabs for buttons/progress/toggle/loading. It creates no nested cards and no bitmap art. Save prefabs and label them `All`/`Pack`.

- [ ] **Step 5: Invoke builder, generate CodeBind, and verify GREEN**

Compile, invoke `Game/Buqi/Rebuild Full UI Demo`, generate CodeBind code for all domain prefabs, recompile, serialize bindings, and run prefab tests.

Expected: PASS, zero missing fields, zero compile errors.

- [ ] **Step 6: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs Unity/Assets/Res/UI/UIPrefab/Buqi
git commit -m "feat(buqi): add reusable demo UI widgets"
```

### Task 4: Run Shell Form

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs`

- [ ] **Step 1: Write failing shell prefab and lifecycle tests**

Assert a `1856x1016` safe content area with 72 px header, two 16 px vertical gaps, 824 px body, 88 px command bar, and body columns `208 + 24 + 1112 + 24 + 488`. Assert 12 phase steps, one stage host, fixed context rail, primary/secondary/back commands, loading/error roots, and a fresh controller on the second `OnOpen`.

- [ ] **Step 2: Run and verify RED**

Expected: shell prefab missing.

- [ ] **Step 3: Implement shell ownership**

`OnOpen` accepts optional `BuqiRunShellOpenData`; absent data builds a new catalog from `HotEntry.Tables.BuqiConfig`. It creates a new controller, renders header/rail/context/commands, and asks `BuqiStageWidgetRegistry` for the current stage renderer. `Execute` submits one `BuqiUIDemoCommand`, shows rejection reason without moving controls, then renders the returned immutable view. `OnClose` clears every handler, stage, message, and controller reference.

- [ ] **Step 4: Build, bind, and test the shell prefab**

Run builder, CodeBind generation/serialization, compilation, and focused prefab/lifecycle tests.

Expected: PASS with no callback accumulation.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiRunShellForm.prefab
git commit -m "feat(buqi): build full demo run shell"
```

### Task 5: Detail, Confirm, and Message Forms

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiItemDetailForm.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiConfirmForm.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiMessageForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiRunShellPrefabTests.cs`

- [ ] **Step 1: Write failing popup lifecycle tests**

Open each form twice with different data and assert title/body/button labels and callbacks are replaced, not appended. Closing must clear delegates. Message form must not pause or block the underlying shell.

- [ ] **Step 2: Run and verify RED**

Expected: form classes and prefabs are absent.

- [ ] **Step 3: Implement typed open data**

```csharp
public sealed class BuqiItemDetailOpenData
{
    public ItemCardView Item;
    public string FullEffectText;
    public string ModificationText;
}

public sealed class BuqiConfirmOpenData
{
    public string Title;
    public string Message;
    public string ConfirmLabel;
    public string CancelLabel;
    public Action Confirm;
    public Action Cancel;
}

public sealed class BuqiMessageOpenData
{
    public string Message;
    public bool IsError;
    public float DurationSeconds;
}
```

Forms reject wrong userData with one diagnostic log and close themselves. `BuqiMessageForm` uses unscaled time and defaults to 2 seconds.

- [ ] **Step 4: Build, bind, test, and commit**

Run the Builder/CodeBind/compile sequence, then popup lifecycle tests. Commit runtime scripts, builder changes, tests, and three prefabs with message `feat(buqi): add shared demo UI forms`.

### Task 6: Register UIForms Through Luban

**Files:**
- Modify: `Design/Excel/GameHot/Datas/Game/UI.xlsx`
- Regenerate: `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`
- Modify: generated UI bytes/JSON produced by the existing exporter.

- [ ] **Step 1: Add a failing registration test**

Expect these constants and exact values: `BuqiRunShellForm=104`, `BuqiItemDetailForm=105`, `BuqiConfirmForm=106`, `BuqiMessageForm=107`.

- [ ] **Step 2: Run and verify RED**

Expected: constants absent.

- [ ] **Step 3: Append structured UI rows**

Use openpyxl and preserve workbook metadata:

```text
104, BuqiRunShellForm, 不器界面预览, Hot/Buqi/BuqiRunShellForm, Default, false, true
105, BuqiItemDetailForm, 装备详情, Hot/Buqi/BuqiItemDetailForm, Pop, false, true
106, BuqiConfirmForm, 不器确认框, Hot/Buqi/BuqiConfirmForm, Pop, false, true
107, BuqiMessageForm, 不器状态提示, Hot/Buqi/BuqiMessageForm, Message, true, false
```

Reject conflicting IDs/names instead of overwriting.

- [ ] **Step 4: Export and run tests**

Invoke the current GameHot exporter, require the four generated IDs, compile, and run `Game.Hot.Buqi.Tests`.

- [ ] **Step 5: Commit**

Commit only workbook and generated exporter outputs with `feat(buqi): register full demo UI forms`.

### Task 7: Shell-Level Unity Verification

- [ ] **Step 1: Compile and run all EditMode tests**

Use Agent Bridge; require zero compile errors and every `Game.Hot.Buqi.Tests` result passed.

- [ ] **Step 2: Open Run Shell through the Editor menu**

Invoke `Game/Buqi/Open Full UI Demo`, enter Play Mode if required, and verify header, 12-step rail, work area, context rail, and bottom commands stay fixed at 1920x1080.

- [ ] **Step 3: Verify state semantics**

Exercise one accepted and one rejected command, Back, detail, confirm, message, close/reopen, and ensure the second session resets coins, phase, selection, prediction, and callbacks.

- [ ] **Step 4: Capture shell and popup evidence**

Capture starter shell, item detail, confirm, and error/loading states. Inspect nonblank rendering, no overlap, full Chinese text, and correct group ordering.

- [ ] **Step 5: Check Console and git scope**

Require zero Console errors, restore Game View and scene state, run `git diff --check`, and ensure concurrent gameplay docs and workbook temp files remain untouched.
