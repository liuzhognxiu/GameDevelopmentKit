# Buqi Battle Replay UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deterministic 1920x1080 `BattleForm` that plays one real Buqi simulation result without re-running battle rules during playback.

**Architecture:** Keep replay data, validation, projection, filtering, paging, and fact extraction in pure C# under `Buqi/Battle/Replay`, so the existing .NET 8 headless project compiles the code. Put Luban adaptation in `Buqi/Demo`, Unity rendering in `Buqi/UI`, and prefab generation in the existing GameHot Editor assembly. `BattleForm` owns one controller per open lifecycle and consumes only immutable replay frames.

**Tech Stack:** Unity 6000.3.21f1, GameHot/UGF `StarForceUIForm`, Unity UI + TMP, CodeBind, Luban, NUnit EditMode, .NET 8 headless verifier, Unity Agent Bridge.

---

## File Map

Create runtime-neutral replay files:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayData.cs`: validated immutable input and effect metadata.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayFrame.cs`: side/item/frame/error view data.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayFacts.cs`: three evidence-backed post-battle facts.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayController.cs`: projection, timing, replay, filtering, paging, and final-state validation.

Create GameHot adapters and views:

- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Demo/BuqiBattleDemoFactory.cs`: deterministic runtime scenario from `BuqiConfigCatalog`.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleForm.cs`: UIForm lifecycle and event ownership.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/ItemCardWidget.cs`: item rendering only.
- `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleLogWidget.cs`: log/fact rendering only.
- `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleUIBuilder.cs`: prefab builder and demo-open menu.

Create tests and generated resources:

- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiReplayTests.cs`: pure replay behavior.
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleDemoFactoryTests.cs`: config-to-demo determinism.
- `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleUIPrefabTests.cs`: prefab hierarchy and bindings.
- `Unity/Assets/Res/UI/UIForm/Hot/Buqi/BattleForm.prefab`.
- `Unity/Assets/Res/UI/UIPrefab/Buqi/ItemCardWidget.prefab`.
- `Unity/Assets/Res/UI/UIPrefab/Buqi/BattleLogWidget.prefab`.
- Generated `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/*.Bind.cs` from CodeBind.

Modify integration files:

- `Design/Excel/GameHot/Datas/Game/UI.xlsx`: add `BattleForm` as ID `103`.
- `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`: regenerate through GameHot ExcelExporter, never hand edit.
- `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`: start button opens deterministic battle replay.
- `Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab`: rename visible start label to `开始战斗` through the UI builder/menu workflow.

### Task 1: Replay Input and Initial Frame

**Files:**
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiReplayTests.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayData.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayFrame.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayController.cs`

- [ ] **Step 1: Write the failing initial-frame test**

Add a `ReplayFixture()` helper that uses `BuqiTestSuite.CreateFixtureProvider()` and the first `BuqiTestSuite.CreateVectors()` request, runs `BuqiBattleSimulator.Simulate` once, and constructs replay data. Assert that `new BattleReplayController(data).Frame` has tick `0`, both snapshots' initial execution/buffer/noise, eight slot descriptors per side, and no applied event.

```csharp
[Test]
public void InitialFrame_ComesFromBuildSnapshots()
{
    BattleReplayController controller = new BattleReplayController(ReplayFixture());

    Assert.That(controller.Frame.Tick, Is.Zero);
    Assert.That(controller.Frame.Left.Execution, Is.EqualTo(controller.Data.LeftBuild.InitialExecution));
    Assert.That(controller.Frame.Left.Buffer, Is.EqualTo(controller.Data.LeftBuild.InitialBuffer));
    Assert.That(controller.Frame.Left.Noise, Is.EqualTo(controller.Data.LeftBuild.InitialNoiseDebt));
    Assert.That(controller.Frame.Left.Slots.Count, Is.EqualTo(8));
    Assert.That(controller.Frame.CurrentEvent, Is.Null);
}
```

- [ ] **Step 2: Run the test and verify RED**

Use Agent Bridge `run_tests` with `mode=edit` and `testNames=["Game.Hot.Buqi.Tests.BuqiReplayTests.InitialFrame_ComesFromBuildSnapshots"]`, then poll `get_test_result` with the returned `runId`.

Expected: FAIL because `BattleReplayData` and `BattleReplayController` do not exist.

- [ ] **Step 3: Add minimal immutable replay types**

Define these public APIs exactly:

```csharp
public sealed class BattleReplayEffectInfo
{
    public string EffectId;
    public BuqiEffect Effect;
    public BuqiTarget Target;
}

public sealed class BattleReplayData
{
    public string Title;
    public string LeftName;
    public string RightName;
    public BuildSnapshot LeftBuild;
    public BuildSnapshot RightBuild;
    public BattleResult Result;
    public IReadOnlyList<BattleEvent> Log;
    public IItemDefinitionProvider Definitions;
    public IReadOnlyDictionary<string, BattleReplayEffectInfo> Effects;
}

public sealed class BattleReplayItemFrame
{
    public string InstanceId;
    public string DefinitionId;
    public int AnchorSlot;
    public int Size;
    public int Charge;
    public int FrozenTicks;
    public float Cooldown01;
}

public sealed class BattleReplaySideFrame
{
    public int Execution;
    public int MaxExecution;
    public int Buffer;
    public int Noise;
    public IReadOnlyList<BattleReplayItemFrame> Items;
    public IReadOnlyList<string> Slots;
}

public sealed class BattleReplayFrame
{
    public int Tick;
    public BattleReplaySideFrame Left;
    public BattleReplaySideFrame Right;
    public BattleEvent CurrentEvent;
    public bool IsFinished;
    public string Error;
}
```

Construct eight slots from each `BuildSnapshot` and item definition. Reject null input, missing definitions, out-of-range spans, overlap, version gaps, non-monotonic sequence/tick, and a recomputed `BuqiCrypto.BattleLogHash` mismatch.

Add the minimal `BattleReplayController(BattleReplayData data)`, read-only `Data`, and read-only `Frame` members needed by the initial-frame test. Playback methods remain out of scope until Task 2.

- [ ] **Step 4: Run EditMode and headless tests and verify GREEN**

Run the focused EditMode test through Agent Bridge. Then run:

```powershell
dotnet build Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
```

Expected: focused test PASS, headless build succeeds with zero warnings, verifier reports all contracts and approved hashes passed.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiReplayTests.cs
git commit -m "feat(buqi): add battle replay input model"
```

### Task 2: Deterministic Projection and Playback

**Files:**
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiReplayTests.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayController.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay/BattleReplayFacts.cs`

- [ ] **Step 1: Add failing timing and replay tests**

Add separate tests for pause, `1x/2x/4x`, same-tick sequence order, skip, replay, and final equality.

```csharp
[Test]
public void Pause_PreventsAdvance()
{
    BattleReplayController controller = new BattleReplayController(ReplayFixture());
    controller.SetPaused(true);
    controller.Advance(2f);
    Assert.That(controller.Frame.Tick, Is.Zero);
}

[TestCase(1, 10)]
[TestCase(2, 20)]
[TestCase(4, 40)]
public void Speed_AdvancesPresentationTicks(int speed, int expectedTick)
{
    BattleReplayController controller = new BattleReplayController(ReplayFixture());
    controller.SetSpeed(speed);
    controller.Advance(1f);
    Assert.That(controller.Frame.Tick, Is.EqualTo(Math.Min(expectedTick, controller.Data.Result.DurationTicks)));
}

[Test]
public void SkipAndReplay_ReproduceFinalState()
{
    BattleReplayController controller = new BattleReplayController(ReplayFixture());
    controller.SkipToEnd();
    BattleReplayFrame first = controller.Frame;
    controller.Replay();
    controller.SkipToEnd();
    Assert.That(controller.Frame.Left.Execution, Is.EqualTo(first.Left.Execution));
    Assert.That(controller.Frame.Right.Execution, Is.EqualTo(first.Right.Execution));
    Assert.That(controller.Frame.IsFinished, Is.True);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run `Game.Hot.Buqi.Tests.BuqiReplayTests` through Agent Bridge.

Expected: FAIL because playback methods and projection do not exist.

- [ ] **Step 3: Implement minimal projection**

Use `TickSeconds = 0.1f`, speeds `{1,2,4}`, and an integer event cursor. `Advance` computes a target presentation tick, then applies every log row with `event.Tick <= targetTick` in `Sequence` order. `SkipToEnd` resets and projects all rows; `Replay` resets to snapshot state and speed `1`.

Resource projection rules are explicit:

```csharp
switch (effectInfo.Effect)
{
    case BuqiEffect.Buffer: ApplyBuffer(targetSide, battleEvent); break;
    case BuqiEffect.Damage:
    case BuqiEffect.Burn:
    case BuqiEffect.Poison: ApplyDamageOrAbsorb(targetSide, battleEvent); break;
    case BuqiEffect.Heal:
    case BuqiEffect.Regen: ApplyHeal(targetSide, battleEvent); break;
    case BuqiEffect.Noise: ApplyNoise(targetSide, battleEvent); break;
    case BuqiEffect.Charge: ApplyCharge(targetItem, battleEvent.Amount); break;
    case BuqiEffect.Freeze: ApplyFreeze(targetItem, battleEvent.Amount); break;
}
```

Resolve side from source ownership plus `BuqiTarget`: enemy execution/enemy item targets use the opposite side; self/adjacent targets use the source side. `OvertimeDamage` is the only source-less resource event and targets the side encoded by simulator ordering; preserve an explicit `BattleReplayEventSide` entry in `BattleReplayData` when the factory captures the simulator output. At end, compare projected execution/buffer/noise against all six `BattleResult` fields and enter an error state on mismatch.

- [ ] **Step 4: Add failing cooldown, filtering, paging, and facts tests**

Assert cooldown interpolation only changes between real `Declare` events, filters do not mutate `Frame`, each page contains at most 12 rows, and facts always contain contribution/chain/risk entries with source event sequences.

```csharp
[Test]
public void Filtering_DoesNotChangeProjectedFrame()
{
    BattleReplayController controller = new BattleReplayController(ReplayFixture());
    controller.Advance(1f);
    int tick = controller.Frame.Tick;
    int execution = controller.Frame.Left.Execution;
    controller.SetFilter(new BattleReplayFilter { ReasonCode = "Damage" });
    Assert.That(controller.Frame.Tick, Is.EqualTo(tick));
    Assert.That(controller.Frame.Left.Execution, Is.EqualTo(execution));
    Assert.That(controller.GetLogPage(0).Rows.Count, Is.LessThanOrEqualTo(12));
}
```

- [ ] **Step 5: Run tests and verify RED**

Expected: new filter/fact tests FAIL because query APIs are absent.

- [ ] **Step 6: Implement query APIs and facts**

Add `BattleReplayFilter` fields `KeyOnly`, `SourceInstanceId`, `TargetInstanceId`, `ChainId`, and `ReasonCode`; add `GetLogPage(int)` with stable sequence ordering and 12 rows. Build facts from effective amounts only and store `IReadOnlyList<int> EventSequences`. Never emit strategy advice.

- [ ] **Step 7: Run full replay tests and headless verification**

Expected: all `BuqiReplayTests` PASS, headless build and `verify` PASS.

- [ ] **Step 8: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/Replay Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiReplayTests.cs
git commit -m "feat(buqi): implement deterministic battle replay"
```

### Task 3: Runtime Demo Factory

**Files:**
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleDemoFactoryTests.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Demo/BuqiBattleDemoFactory.cs`

- [ ] **Step 1: Write failing deterministic factory tests**

Construct a catalog through existing config test helpers. Call `TryCreate(catalog, out data, out error)` twice and assert identical title, snapshots, result fields, event sequences, and hash. Remove the selected echo and assert a false result with an exact non-empty reason.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because `BuqiBattleDemoFactory` is missing.

- [ ] **Step 3: Implement the factory**

Use stable echo IDs sorted with `StringComparer.Ordinal`; select the first two echoes that have legal, non-overlapping snapshots and different IDs. Convert `BuqiBuildSnapshotConfigRow` to new runtime `BuildSnapshot` instances, prefix instance IDs with `L-`/`R-` to keep ownership unambiguous, build `BuqiDefinitionProvider`, and call `BuqiBattleSimulator.Simulate` exactly once. Recompute effect metadata from item definitions and event side metadata from final log creation order. Return false instead of falling back to random content.

```csharp
public static bool TryCreate(BuqiConfigCatalog catalog, out BattleReplayData data, out string error)
{
    data = null;
    error = string.Empty;
    if (catalog == null || catalog.Global == null)
    {
        error = "不器配置尚未加载";
        return false;
    }
    return TryCreateFromStableEchoPair(catalog, out data, out error);
}
```

`TryCreateFromStableEchoPair` is a private method in the same task. It performs the stable sort, validates and clones both snapshots, prefixes instance IDs, constructs definitions, runs one simulation, builds effect/event-side metadata, validates the assembled `BattleReplayData`, and returns false with the validator reason on any failure.

- [ ] **Step 4: Run factory, replay, config, and headless tests**

Expected: all PASS and no new headless dependency.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/Demo/BuqiBattleDemoFactory.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleDemoFactoryTests.cs
git commit -m "feat(buqi): add deterministic battle demo factory"
```

### Task 4: Widgets, BattleForm, and Prefab Builder

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleForm.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/ItemCardWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleLogWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleUIBuilder.cs`
- Create: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleUIPrefabTests.cs`

- [ ] **Step 1: Write failing prefab contract tests**

Assert the three prefab paths exist, roots contain the expected component, `BattleForm` contains `LeftTrack/Slot01..08`, `RightTrack/Slot01..08`, `Evidence/Log01..12`, and controls `Back`, `PlayPause`, `Speed1`, `Speed2`, `Speed4`, `Skip`, and `Replay`.

- [ ] **Step 2: Run prefab tests and verify RED**

Expected: FAIL because prefabs do not exist.

- [ ] **Step 3: Add UI component shells and builder**

Mark all three runtime classes `partial` with `[MonoBehaviourBinding]`. The builder creates fixed 1920x1080 anchors, reuses `Assets/Res/UI/UISprite/Common/*.png` and component-library prefabs, names CodeBind nodes as `<Property>_<ComponentType>`, saves prefabs, labels them `All` and `Pack`, and never writes prefab YAML.

`BattleForm` must override the repository's conditional lifecycle signatures, create a fresh controller in `OnOpen`, advance with `realElapseSeconds` in `OnUpdate`, and clear all handlers/state in `OnClose`.

- [ ] **Step 4: Trigger compile and verify builder menu exists**

Use Agent Bridge `recompile`, poll `get_compile_result` until compilation is idle, and require `errorCount=0`. Invoke `Game/Buqi/Rebuild Battle UI Demo` through `invoke_menu`.

Expected: three prefab assets are created.

- [ ] **Step 5: Generate and serialize CodeBind references**

For each prefab, call Agent Bridge `codebind` with `action=generate_code` and its `assetPath`. Recompile and require zero errors. Then call `codebind` with `action=set_serialization` for each prefab. Do not hand-edit generated `*.Bind.cs`.

- [ ] **Step 6: Complete rendering behavior after bindings exist**

`ItemCardWidget.Render` shows name, size, primary effect marker, charge, freeze, and cooldown. `BattleLogWidget.Render` shows tick/source/target/effect/amount and applies semantic color. `BattleForm.Render` updates both resources, static slots, active cards, current event, page rows, timeline, speed state, error panel, and three final facts without resizing layout.

- [ ] **Step 7: Run prefab and lifecycle tests**

Add a lifecycle test that opens/renders/closes two component instances and proves the second uses a different controller and default speed/filter. Run the complete `Game.Hot.Buqi.Tests` assembly.

Expected: PASS, no missing serialized fields.

- [ ] **Step 8: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleUIBuilder.cs Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleUIPrefabTests.cs Unity/Assets/Res/UI/UIForm/Hot/Buqi Unity/Assets/Res/UI/UIPrefab/Buqi
git commit -m "feat(buqi): build battle replay UI prefabs"
```

### Task 5: Luban UI Registration and Main Menu Entry

**Files:**
- Modify: `Design/Excel/GameHot/Datas/Game/UI.xlsx`
- Regenerate: `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`
- Modify: `Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab`

- [ ] **Step 1: Add a failing menu integration test**

Add an EditMode source/prefab contract test that expects `UIFormId.BattleForm`, a menu start label `开始战斗`, and a public `OnStartButtonClick` path that creates replay data and calls `GameEntry.UI.OpenUIForm(UIFormId.BattleForm, data)`.

- [ ] **Step 2: Run it and verify RED**

Expected: FAIL because the ID and menu behavior are absent.

- [ ] **Step 3: Edit UI.xlsx through openpyxl**

Append exactly this structured row to the first worksheet while preserving styles and workbook metadata:

```text
Id=103, CSName=BattleForm, Desc=不器战斗回放, AssetName=Hot/Buqi/BattleForm,
UIGroupName=Default, AllowMultiInstance=false, PauseCoveredUIForm=true
```

Refuse to write if ID `103` or `CSName=BattleForm` already exists with different data.

- [ ] **Step 4: Run the existing GameHot ExcelExporter**

Invoke the repository's exporter through its current Unity menu discovered from the Editor; confirm generated `UIFormId.BattleForm == 103` and generated bytes/JSON include the new row. Never edit `UIFormId.cs` manually.

- [ ] **Step 5: Change the menu behavior**

Keep `ProcedureMenu.StartGame()` intact. In `OnStartButtonClick`, call `BuqiBattleDemoFactory.TryCreate(HotEntry.Tables.BuqiConfig, ...)`; on success open `BattleForm`, on failure log one structured error and leave the menu usable. Update the existing prefab label through the builder or serialized Unity API.

- [ ] **Step 6: Run integration tests and compile**

Expected: menu test PASS, `Game.Hot.Buqi.Tests` PASS, Unity compile has zero errors.

- [ ] **Step 7: Commit**

```powershell
git add Design/Excel/GameHot/Datas/Game/UI.xlsx Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab
git commit -m "feat(buqi): open battle replay from menu"
```

### Task 6: Real Unity Interaction and Screenshot Evidence

**Files:**
- Create evidence under ignored `.agentbridge/screenshots/` and `output/buqi-ui/` only; do not commit temporary bridge files.

- [ ] **Step 1: Clear logs and compile**

Use Agent Bridge `clear_logs`, `recompile`, and `get_compile_result`. Require zero errors.

- [ ] **Step 2: Run all EditMode tests**

Call `run_tests` with `assemblyNames=["Game.Hot.Buqi.Tests"]`; poll `get_test_result(includePassed=true, limit=200)`. Require all tests passed.

- [ ] **Step 3: Run the actual Launcher flow**

Use `list_scenes` to preserve the current scene, open the repository's initialization scene only if needed, enter Play Mode with `play_scene`, and click the real menu entry through the game UI. Verify pause, all three speeds, skip, replay, log tabs/filter, facts, and return.

- [ ] **Step 4: Capture 1920x1080 evidence**

Call `set_game_view_resolution(width=1920,height=1080)`, retain the restore token, and capture `10-battle-replay.jpg` plus one final-state image. Inspect both for nonblank pixels, no overlap, complete key text, visible 8+8 slots, and fixed evidence rail. Restore the Game View with the token.

- [ ] **Step 5: Check Console and stop Play Mode**

Use `search_logs(type=error, limit=200)` and require zero entries, stop Play Mode, and restore the original scene if changed.

- [ ] **Step 6: Run headless verification and inspect git scope**

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
git diff --check
git status --short
```

Expected: headless verification passes; no unrelated gameplay docs, workbook temp files, bridge slots, or `output/` files are staged.

- [ ] **Step 7: Commit final battle UI verification fixes**

Commit only fixes required by the real run, with message `fix(buqi): verify battle replay UI flow`.
