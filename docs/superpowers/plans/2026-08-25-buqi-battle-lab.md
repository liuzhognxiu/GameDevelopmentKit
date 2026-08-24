# Buqi Battle Lab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an independent mouse-driven Buqi battle lab where a tester selects a fixed hero, drags unlimited copies of every configured item onto either editable board, selects a preset or custom enemy, and launches the existing deterministic battle replay.

**Architecture:** Keep all mutable lab state in pure C# under `Game.Hot.Buqi.BattleLab`: a read-only catalog projection, a configuration-sized board, a controller, and a replay factory. Compile those files in the existing .NET 8 headless validator and expose immutable views to a single `BuqiBattleLabForm`; Unity widgets only translate pointer events into controller commands. Generate the form and menu entry through an editor builder, while UI registration remains sourced from the existing GameHot UI workbook.

**Tech Stack:** C# 9, Unity 6000.3.18f1, Unity UI/EventSystem, UnityGameFramework `StarForceUIForm`, Luban UI configuration, NUnit EditMode tests, .NET 8 headless contracts.

**Design:** `docs/superpowers/specs/2026-08-24-buqi-battle-lab-design.md`

---

## File Map

### Pure C# runtime

- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabModels.cs`: enums and immutable hero, item, opponent, placement, board, view, preview, and command-result DTOs.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabCatalog.cs`: project `BuqiConfigCatalog.Items` and `.Echoes`, define the three approved heroes, copy preset snapshots, and expose `BuqiDefinitionProvider`.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabBoard.cs`: configuration-sized atomic add, move, remove, clear, and preview operations.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabController.cs`: phase, both boards, hero selection, opponent modes, stable instance IDs, and state-preserving commands.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabReplayFactory.cs`: build and validate both snapshots, derive a deterministic seed, simulate, and return `BattleReplayData`.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs`: Unity-free behavioral contract suite shared by headless and EditMode validation.
- Create matching `.meta` files with unique 32-character lowercase hexadecimal GUIDs.

### Headless validation

- Modify `Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj`: link the BattleLab folder plus the two pure config files used by it.
- Modify `Share/Buqi.Simulation.Headless/Program.cs`: add `battle-lab` mode and run the new contract suite in `verify` and `all` modes.

### Unity UI runtime

- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiBattleLabForm.cs`: form lifecycle, rendering, dynamic template instances, drag ghost, drop routing, replay opening, and responsive layout.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabItemWidget.cs`: library/board item click, hover, begin-drag, drag, and end-drag source events.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabSlotWidget.cs`: side/index-aware hover and drop target.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabHeroWidget.cs`: hero selection card.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabOpponentWidget.cs`: preset opponent selection row.
- Create `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabRecycleWidget.cs`: explicit board-instance deletion target.
- Modify `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`: add `OnBattleLabButtonClick()`.
- Create matching `.meta` files.

### Editor assets and registration

- Create `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleLabUIBuilder.cs`: build widget prefabs, build the full form, and idempotently patch `MenuForm.prefab`.
- Create `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiBattleLabItemWidget.prefab` and `.meta`.
- Create `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiBattleLabSlotWidget.prefab` and `.meta`.
- Create `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiBattleLabHeroWidget.prefab` and `.meta`.
- Create `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiBattleLabOpponentWidget.prefab` and `.meta`.
- Create `Unity/Assets/Res/UI/UIPrefab/Buqi/BuqiBattleLabRecycleWidget.prefab` and `.meta`.
- Create `Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiBattleLabForm.prefab` and `.meta`.
- Modify `Unity/Assets/Res/UI/UIForm/Hot/MenuForm.prefab`: add one `BattleLab` button under `Buttons` and retain all existing buttons.
- Modify `Design/Excel/GameHot/Datas/Game/UI.xlsx`: add UI row 109.
- Regenerate `Unity/Assets/Res/Editor/Luban/dtuiform.json`, `Unity/Assets/Res/Luban/dtuiform.bytes`, `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`, and `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Generate/UGF/UGFUIFormId.cs`.

### Tests

- Create `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabCoreTests.cs`: NUnit wrapper over contracts plus focused replay assertions.
- Create `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabInteractionTests.cs`: widget and form behavior without relying on scene state.
- Create `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabPrefabTests.cs`: serialized binding, dynamic template, menu entry, UI ID, and 1280/1920 layout contracts.
- Create matching `.meta` files.

## Test Commands

Run these from the repository root.

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- battle-lab
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
dotnet build Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -warnaserror
git diff --check
```

Unity is not currently discoverable on this machine. When Unity 6000.3.18f1 is installed and repository `AGENTS.md` plus the installed Unity Agent Bridge `AGENT.md` have been read, resolve and run it with:

```powershell
$buqiUnityPath = (Get-Command Unity.exe -ErrorAction Stop).Source
New-Item -ItemType Directory -Force -Path "$PWD/artifacts" | Out-Null
& $buqiUnityPath -batchmode -quit -projectPath "$PWD/Unity" -executeMethod Game.Hot.Editor.BuqiBattleLabUIBuilder.BuildAll -logFile "$PWD/artifacts/buqi-battle-lab-builder.log"
& $buqiUnityPath -batchmode -nographics -projectPath "$PWD/Unity" -runTests -testPlatform EditMode -testFilter Game.Hot.Buqi.Tests.BuqiBattleLab -testResults "$PWD/artifacts/buqi-battle-lab-tests.xml" -logFile "$PWD/artifacts/buqi-battle-lab-tests.log" -quit
```

Expected Unity result: both commands exit `0`, the XML reports zero failed tests, and neither log contains a compile error, missing serialized reference, or UI asset load error.

---

### Task 1: Headless Harness, Models, and Catalog Projection

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabModels.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabCatalog.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs`
- Modify: `Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj`
- Modify: `Share/Buqi.Simulation.Headless/Program.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabCoreTests.cs`

- [ ] **Step 1: Add a failing catalog contract**

Create the contract entry with a `RunAll()` method that aggregates named failures rather than throwing. The first check must construct a source catalog with `BoardSlotCount = 8`, three items deliberately inserted in reverse `DefinitionId` order, and one echo; then assert:

```csharp
BuqiBattleLabCatalog.TryCreate(source, out BuqiBattleLabCatalog catalog, out string error)
catalog.BoardSlotCount == 8
catalog.Heroes.Select(hero => hero.HeroId) == new[] { "balanced", "guarded", "survivor" }
catalog.Heroes[0] == ("归衡者", 100, 0, 0)
catalog.Heroes[1] == ("铁衣客", 85, 20, 0)
catalog.Heroes[2] == ("长生客", 115, 0, 4)
catalog.Items.Select(item => item.DefinitionId) is ordinally sorted
catalog.Items.All(item => item.Quality == BuqiQuality.Normal)
catalog.PresetOpponents.Single().Snapshot is not the same object as source.Echoes[0].Snapshot
```

Add a second source with `BoardSlotCount = 10` and assert catalog creation succeeds; add a third with 7 and assert the exact error `"战斗实验室棋盘只支持 8 至 10 格"`.

- [ ] **Step 2: Wire the missing contract into headless compilation**

Add exact linked sources to the headless project:

```xml
<Compile Include="..\..\Unity\Assets\Scripts\Game\Hot\Code\Buqi\Config\BuqiConfigModels.cs" />
<Compile Include="..\..\Unity\Assets\Scripts\Game\Hot\Code\Buqi\Config\BuqiDefinitionProvider.cs" />
<Compile Include="..\..\Unity\Assets\Scripts\Game\Hot\Code\Buqi\BattleLab\**\*.cs" />
```

In `Program.Main`, accept `battle-lab`, run `BuqiBattleLabContractChecks.RunAll()`, print each failure as `[battle-lab-fail] {failure}`, and return `1` when any failure exists. Also run the same suite before hashes in `verify` and `all`.

- [ ] **Step 3: Run the contract to verify RED**

Run:

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- battle-lab
```

Expected: FAIL to compile because `BuqiBattleLabCatalog` and its DTOs do not exist.

- [ ] **Step 4: Implement the exact public model surface**

Define these enums:

```csharp
public enum BuqiBattleLabPhase { HeroSelection, Workbench }
public enum BuqiBattleLabSide { Player, Enemy }
public enum BuqiBattleLabOpponentMode { Preset, Custom }
public enum BuqiBattleLabDragKind { Library, Board }
```

Define immutable DTOs with constructor-initialized get-only properties and these exact constructor signatures:

```csharp
public BuqiBattleLabHeroDefinition(string heroId, string displayName, string role, int initialExecution, int initialBuffer, int initialNoiseDebt)
public BuqiBattleLabItemDefinition(string definitionId, string displayName, string description, int size, BuqiQuality quality, int cooldownTicks, string archetypeId, string role, string positionHint, IReadOnlyList<string> tags, bool enabled, string error)
public BuqiBattleLabPresetOpponent(string echoId, string displayName, string build, BuildSnapshot snapshot, IReadOnlyList<string> validationErrors)
public BuqiBattleLabPlacement(string instanceId, string definitionId, string displayName, int size, BuqiQuality quality, int anchorSlot, string annotationId)
public BuqiBattleLabBoardView(int slotCount, IReadOnlyList<BuqiBattleLabPlacement> placements, IReadOnlyList<string> occupiedInstanceIds)
public BuqiBattleLabView(BuqiBattleLabPhase phase, BuqiBattleLabHeroDefinition playerHero, BuqiBattleLabOpponentMode opponentMode, string selectedPresetId, BuqiBattleLabHeroDefinition customEnemyHero, BuqiBattleLabBoardView playerBoard, BuqiBattleLabBoardView customEnemyBoard, int simulationCount)
public BuqiBattleLabPlacementPreview(BuqiBattleLabSide side, int anchorSlot, int span, IReadOnlyList<int> coveredSlots, bool accepted, string reason)
public BuqiBattleLabCommandResult(bool accepted, string reason, BuqiBattleLabView view)
```

Every constructor must defensively copy list inputs into a local array and expose `Array.AsReadOnly(copy)`. Do not expose `BuqiItemConfigRow`, `BuqiEchoConfigRow`, or mutable `List<T>` through a view.

- [ ] **Step 5: Implement catalog creation**

`BuqiBattleLabCatalog.TryCreate` must:

```csharp
if (source?.Global == null || source.Items == null || source.Echoes == null)
    reject "不器战斗实验室配置不可用"
if (source.Global.BoardSlotCount < 8 || source.Global.BoardSlotCount > 10)
    reject "战斗实验室棋盘只支持 8 至 10 格"
```

Create the heroes in approved order. Sort copied item rows and echoes with `StringComparer.Ordinal`. Mark an item disabled with `"道具尺寸必须为 1 至 3 格"` when size is outside 1..3; keep it in the catalog so the test tool exposes invalid content. For each echo, deep-copy `BuildSnapshot`, validate it through `BuqiBoardValidator`, and store all returned errors without dropping the row.

- [ ] **Step 6: Run the contract to verify GREEN**

Run the headless command. Expected: exit `0` and `[battle-lab] all behavioral checks passed`.

- [ ] **Step 7: Add the NUnit wrapper**

Create:

```csharp
[Test]
public void HeadlessContracts_PassInUnityAssembly()
{
    Assert.That(BuqiBattleLabContractChecks.RunAll(), Is.Empty);
}
```

- [ ] **Step 8: Commit**

```powershell
git add Share/Buqi.Simulation.Headless Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabCoreTests.cs*
git commit -m "feat(buqi): add battle lab catalog contracts"
```

---

### Task 2: Configuration-Sized Atomic Boards

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabBoard.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs`

- [ ] **Step 1: Add failing board contracts**

Add checks for both slot counts 8 and 10:

```csharp
BuqiBattleLabBoard board = new BuqiBattleLabBoard(slotCount);
board.TryAdd(new BuqiBattleLabPlacement("p-1", "small", "小型", 1, BuqiQuality.Normal, 0, ""), out reason)
board.TryAdd(new BuqiBattleLabPlacement("p-2", "medium", "中型", 2, BuqiQuality.Normal, slotCount - 2, ""), out reason)
board.Preview("large", 3, slotCount - 2, "").Reason == "需要连续 3 格"
board.TryMove("p-2", 1, out reason)
board.TryRemove("p-1", out reason)
board.Clear()
```

Assert the failed large preview and a failed overlap leave the exact same `View` reference and placement sequence. Assert moving an unknown ID returns `"来源位置没有道具"`; duplicate instance IDs return `"同一实例不能重复放置"`.

- [ ] **Step 2: Run RED**

Run the headless battle-lab command. Expected: compile failure because `BuqiBattleLabBoard` does not exist.

- [ ] **Step 3: Implement the board transaction boundary**

The board owns a private `List<BuqiBattleLabPlacement>` and caches an immutable `View`. All mutations must plan against a copy first, call one shared `TryValidatePlacement(IReadOnlyList<BuqiBattleLabPlacement> candidate, BuqiBattleLabPlacement placement, string ignoredInstanceId, out string reason)`, then replace both list and view only on success.

Use these signatures:

```csharp
public BuqiBattleLabBoard(int slotCount)
public BuqiBattleLabBoardView View { get; }
public BuqiBattleLabPlacementPreview Preview(string definitionId, int size, int anchorSlot, string ignoredInstanceId)
public bool TryAdd(BuqiBattleLabPlacement placement, out string reason)
public bool TryMove(string instanceId, int anchorSlot, out string reason)
public bool TryRemove(string instanceId, out string reason)
public bool Clear()
public IReadOnlyList<BuqiBattleLabPlacement> CopyPlacements()
```

Validation order and exact reasons:

```text
invalid size -> 道具尺寸必须为 1 至 3 格
anchor outside 0..SlotCount-1 -> 目标位置无效
anchor + size > SlotCount -> 需要连续 {size} 格
overlap -> 与{DisplayName}重叠
duplicate ID -> 同一实例不能重复放置
unknown source -> 来源位置没有道具
```

- [ ] **Step 4: Run GREEN and existing battle verification**

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- battle-lab
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
```

Expected: both exit `0`.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabBoard.cs* Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs
git commit -m "feat(buqi): add dynamic battle lab boards"
```

---

### Task 3: State Controller and Stable Instance Identity

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabController.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs`

- [ ] **Step 1: Add failing state and identity contracts**

Cover these exact sequences:

```csharp
controller.EnterWorkbench() rejects "请先选择我方英雄"
controller.SelectPlayerHero("balanced") accepts
controller.EnterWorkbench() accepts and keeps the selected hero
controller.AddFromLibrary(Player, "small", 0) creates lab-player-0001
controller.AddFromLibrary(Player, "small", 1) creates lab-player-0002
an illegal third add does not consume lab-player-0003
the next legal add creates lab-player-0003
controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0) rejects in Preset mode with "预设敌人不可编辑"
controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom) accepts
controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0) creates lab-enemy-0001
controller.Move(Player, "lab-player-0001", Enemy, 2) rejects "不能转移双方已有实例"
```

Add state preservation checks: select preset A, switch Custom, choose `guarded`, add an enemy item, switch Preset then Custom, and assert both the preset ID and custom board/hero remain unchanged. Assert changing either hero keeps its board.

- [ ] **Step 2: Run RED**

Expected: compile failure for missing controller.

- [ ] **Step 3: Implement the controller API**

Use these public methods:

```csharp
public static bool TryCreate(BuqiBattleLabCatalog catalog, out BuqiBattleLabController controller, out string error)
public BuqiBattleLabView View { get; }
public BuqiBattleLabCommandResult SelectPlayerHero(string heroId)
public BuqiBattleLabCommandResult EnterWorkbench()
public BuqiBattleLabCommandResult ReturnToHeroSelection()
public BuqiBattleLabCommandResult SelectOpponentMode(BuqiBattleLabOpponentMode mode)
public BuqiBattleLabCommandResult SelectPresetOpponent(string echoId)
public BuqiBattleLabCommandResult SelectCustomEnemyHero(string heroId)
public BuqiBattleLabPlacementPreview PreviewLibrary(BuqiBattleLabSide side, string definitionId, int anchorSlot)
public BuqiBattleLabCommandResult AddFromLibrary(BuqiBattleLabSide side, string definitionId, int anchorSlot)
public BuqiBattleLabPlacementPreview PreviewMove(BuqiBattleLabSide side, string instanceId, int anchorSlot)
public BuqiBattleLabCommandResult Move(BuqiBattleLabSide sourceSide, string instanceId, BuqiBattleLabSide targetSide, int anchorSlot)
public BuqiBattleLabCommandResult Remove(BuqiBattleLabSide side, string instanceId)
public BuqiBattleLabCommandResult Clear(BuqiBattleLabSide side)
```

Generate an ID only after preview acceptance. Increment the side counter only after `TryAdd` succeeds. Rebuild `View` after each accepted command; rejected commands return the same `View` reference and store the reason only in the returned result, not by mutating state.

- [ ] **Step 4: Run GREEN**

Run battle-lab and verify commands. Expected: exit `0` and no approved battle hash changes.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabController.cs* Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs
git commit -m "feat(buqi): add battle lab state controller"
```

---

### Task 4: Snapshot, Simulation, and Replay Factory

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabReplayFactory.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabController.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab/BuqiBattleLabContractChecks.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabCoreTests.cs`

- [ ] **Step 1: Add failing replay contracts**

Build one legal player board and test both enemy modes. Assert:

```csharp
controller.TryCreateReplay(out BattleReplayData replay, out string error) rejects until an enemy is selected
preset replay preserves echo InitialExecution, InitialBuffer, InitialNoiseDebt, Quality, AnchorSlot, and AnnotationId
custom replay uses the selected custom hero and normal/no-annotation newly created instances
replay.LeftBuild.ContentVersion == catalog.Definitions.ContentVersion
replay.RightBuild.ContentVersion == catalog.Definitions.ContentVersion
replay.Result.Outcome != BattleOutcome.InvalidBuild
replay.Log.Count > 0
replay.Definitions is the catalog definition provider
controller.View.SimulationCount increments only after successful replay creation
```

Create a 10-slot catalog and assert replay creation returns the exact migration guard:

```text
战斗规则当前支持 8 格，实验室配置为 10 格
```

- [ ] **Step 2: Run RED**

Expected: compile failure for missing replay factory and controller method.

- [ ] **Step 3: Implement snapshot construction and deterministic seed**

Use:

```csharp
public static bool TryCreate(
    BuqiBattleLabCatalog catalog,
    BuqiBattleLabHeroDefinition playerHero,
    IReadOnlyList<BuqiBattleLabPlacement> playerPlacements,
    BuqiBattleLabOpponentMode opponentMode,
    BuqiBattleLabPresetOpponent presetOpponent,
    BuqiBattleLabHeroDefinition customEnemyHero,
    IReadOnlyList<BuqiBattleLabPlacement> customEnemyPlacements,
    int simulationIndex,
    out BattleReplayData replay,
    out string error)
```

For custom snapshots set `ArchetypeId = string.Empty`. Convert each placement into a fresh `ItemInstance`; preserve quality and annotation fields. Validate both sides with `BuqiBoardValidator.Validate` and prefix joined errors with `我方：` or `敌方：`.

Derive the seed exactly as:

```csharp
string seedMaterial = BuqiCrypto.SnapshotHash(left) + ":" +
                      BuqiCrypto.SnapshotHash(right) + ":" + simulationIndex;
string seedHash = BuqiCrypto.Sha256Hex(seedMaterial);
ulong seed = ulong.Parse(
    seedHash.Substring(0, 16),
    NumberStyles.HexNumber,
    CultureInfo.InvariantCulture);
```

Call `BuqiBattleSimulator.Simulate`, reject `InvalidBuild`, and populate title, names, both snapshots, result, log, and definitions in `BattleReplayData`.

- [ ] **Step 4: Add focused NUnit replay validation**

In `BuqiBattleLabCoreTests`, instantiate `BattleReplayController`, call `SkipToResult()`, and assert `Frame.Error` is empty. This proves the produced DTO satisfies the existing replay consumer.

- [ ] **Step 5: Run GREEN and battle regression**

Run battle-lab, verify, and headless build. Expected: all exit `0`; approved hashes remain unchanged.

- [ ] **Step 6: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/BattleLab Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabCoreTests.cs
git commit -m "feat(buqi): create battle lab replays"
```

---

### Task 5: Mouse Interaction Widgets

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabItemWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabSlotWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabHeroWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabOpponentWidget.cs`
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLabRecycleWidget.cs`
- Test: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabInteractionTests.cs`

- [ ] **Step 1: Write failing widget interaction tests**

Instantiate each widget on a temporary `GameObject`, bind delegates, and directly call pointer interfaces. Assert:

- item click and hover report the bound `BuqiBattleLabDragKind`, side, and key;
- begin drag lowers alpha and disables raycasts; end drag restores both;
- slot hover and drop report side/index;
- a locked slot still reports hover but rejects drop through `CanAccept = false`;
- hero click returns its HeroId;
- opponent click returns its EchoId;
- recycle drop reports only board-instance payloads and ignores library payloads.

- [ ] **Step 2: Run RED in Unity when available**

Run the EditMode test filter. Expected: compile failure because widgets do not exist. If Unity remains unavailable, record this test as pending and continue only after headless build proves runtime code remains clean.

- [ ] **Step 3: Implement item and drop payloads**

Define:

```csharp
public readonly struct BuqiBattleLabDragPayload
{
    public BuqiBattleLabDragPayload(BuqiBattleLabDragKind kind, BuqiBattleLabSide side, string key)
    public BuqiBattleLabDragKind Kind { get; }
    public BuqiBattleLabSide Side { get; }
    public string Key { get; }
}
```

`BuqiBattleLabItemWidget` implements `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IPointerClickHandler`, `IPointerEnterHandler`, and `IPointerExitHandler`. `BuqiBattleLabSlotWidget` implements `IPointerEnterHandler`, `IPointerExitHandler`, and `IDropHandler`. `BuqiBattleLabRecycleWidget` implements `IDropHandler`.

Keep all widget delegates nullable and clear them in `Clear()`. No widget may call `GameEntry`, mutate the controller, or infer a price.

- [ ] **Step 4: Run GREEN or compile gate**

Run Unity tests when available; always run `git diff --check`. Expected: widget tests pass and no missing Unity callback interfaces.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/Widgets/BuqiBattleLab* Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabInteractionTests.cs*
git commit -m "feat(buqi): add battle lab mouse widgets"
```

---

### Task 6: Form Lifecycle, Dynamic Boards, and Replay Return

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiBattleLabForm.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabInteractionTests.cs`

- [ ] **Step 1: Write failing form contracts**

Use reflection and an injected `BuqiBattleLabOpenData { Catalog = fixture }` to assert:

- `TryInitialize` creates the controller and renders HeroSelection;
- clicking a library item only updates fixed detail and does not change placement count;
- canceling a drag leaves placement count and instance sequence unchanged;
- dropping the same definition twice creates two instances;
- dropping on preset enemy reports `预设敌人不可编辑`;
- switching to Custom exposes enemy hero choices and makes its slots editable;
- `OpenBattleReplay` passes a `BattleReplayOpenData` whose `Confirmed` callback rerenders the same controller view;
- `OnClose` clears dynamic widgets and controller references.

- [ ] **Step 2: Run RED**

Expected: compile failure for `BuqiBattleLabForm` and `BuqiBattleLabOpenData`.

- [ ] **Step 3: Implement open data and initialization**

```csharp
public sealed class BuqiBattleLabOpenData
{
    public BuqiConfigCatalog Catalog;
}
```

`TryInitialize` uses the injected catalog when present. Otherwise it requires `HotEntry.Tables`, calls `BuqiGeneratedConfigAdapter.TryReadFromTables`, creates `BuqiBattleLabCatalog`, then creates the controller. Initialization failures show the full-form error panel and do not close immediately.

- [ ] **Step 4: Implement dynamic rendering**

Serialized templates and hosts must be singular, never fixed arrays:

```csharp
m_HeroTemplate
m_LibraryItemTemplate
m_BoardItemTemplate
m_PlayerSlotTemplate
m_EnemySlotTemplate
m_OpponentTemplate
m_PlayerSlotHost
m_EnemySlotHost
m_LibraryContent
m_OpponentContent
m_DragLayer
```

Also bind explicit Back, Enter Workbench, change-player-hero, Preset mode, Custom mode, clear-player, clear-enemy, and Start Simulation buttons. HeroSelection renders exactly the three catalog heroes; Enter Workbench is interactable only after player selection. Workbench renders every catalog item, every preset opponent, and exactly `controller.View.PlayerBoard.SlotCount` slots per side. Reuse instantiated widgets between renders; destroy extras only when the configured count shrinks. Card library copies remain visible after drops.

Keep `m_FixedDetailKey` and `m_HoverDetailKey` separately. Hover renders the temporary item detail; pointer exit restores the clicked fixed detail or the empty instruction. Render name, size, normal/configured quality, cooldown as `CooldownTicks / 10f` with one decimal place and the `秒` unit, formal effect description, tags, archetype, role, and position hint in the stable detail panel. Disabled catalog items remain visible, show their configuration error, and cannot begin drag.

Preset mode renders the selected echo's copied board in the enemy host with locked slot widgets. Custom mode renders the editable custom board and enemy hero choices. Each side has its own clear command; clear-enemy is hidden or disabled in Preset mode. Start Simulation remains visible in both modes and sends controller validation failures to the inline feedback text.

- [ ] **Step 5: Implement one drag pipeline**

Maintain one active payload, one drag visual, one preview, and `m_DropHandled`. Slot hover asks the controller for `PreviewLibrary` or `PreviewMove`. Slot drop routes:

```text
Library -> Player: AddFromLibrary(Player)
Library -> Enemy/Preset: reject
Library -> Enemy/Custom: AddFromLibrary(Enemy)
Board -> same side: Move
Board -> other side: reject
Board -> recycle: Remove
Library -> recycle: cancel
```

End drag always restores raycasts, clears preview, and destroys the ghost. It never submits a command itself.

- [ ] **Step 6: Implement replay opening without state loss**

```csharp
if (!m_Controller.TryCreateReplay(out BattleReplayData replay, out string error))
{
    ShowFeedback(error);
    return;
}
GameEntry.UI.OpenUIForm(UIFormId.BattleForm, new BattleReplayOpenData
{
    Replay = replay,
    Confirmed = Render,
});
```

Keep the lab form open beneath BattleForm. Do not recreate the controller in the callback.

- [ ] **Step 7: Run tests and commit**

Run headless contracts and Unity interaction tests when available. Commit:

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiBattleLabForm.cs* Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabInteractionTests.cs
git commit -m "feat(buqi): implement battle lab form flow"
```

---

### Task 7: UI Registration and Main Menu Code Entry

**Files:**
- Modify: `Design/Excel/GameHot/Datas/Game/UI.xlsx`
- Modify generated UI files listed in File Map
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/UI/MenuForm.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabPrefabTests.cs`

- [ ] **Step 1: Write failing registration tests**

Assert by reflection that `UIFormId.BuqiBattleLabForm == 109`. Read `MenuForm.cs` and assert it contains exactly one `OpenUIForm(UIFormId.BuqiBattleLabForm)` call and that `OnStartButtonClick` still opens `BuqiRunShellForm`.

- [ ] **Step 2: Run RED**

Expected: missing UIFormId field and missing menu method.

- [ ] **Step 3: Add the authoritative UI workbook row**

Use the project spreadsheet workflow to add exactly:

| Id | CSName | Desc | AssetName | UIGroupName | AllowMultiInstance | PauseCoveredUIForm |
|---:|---|---|---|---|---|---|
| 109 | BuqiBattleLabForm | 不器战斗实验室 | Hot/Buqi/BuqiBattleLabForm | Default | false | true |

Run the repository Luban export workflow. Verify JSON row 109, regenerated bytes, and both generated C# ID files all agree. Do not hand-edit only one generated artifact.

- [ ] **Step 4: Add the menu method**

```csharp
public void OnBattleLabButtonClick()
{
    GameEntry.UI.OpenUIForm(UIFormId.BuqiBattleLabForm);
}
```

Do not alter `OnStartButtonClick`.

- [ ] **Step 5: Run GREEN and commit**

Run the registration tests when Unity is available and inspect generated files with:

```powershell
rg -n '109|BuqiBattleLabForm' Unity/Assets/Res/Editor/Luban/dtuiform.json Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Generate/UGF/UGFUIFormId.cs
```

Commit all source and generated config artifacts together.

---

### Task 8: Prefab Builder, Form Prefab, and Menu Prefab

**Files:**
- Create: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleLabUIBuilder.cs`
- Create/Modify prefab assets listed in File Map
- Create/Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabPrefabTests.cs`

- [ ] **Step 1: Write failing prefab structure tests**

Require these form children:

```text
Header
HeroSelection
HeroSelection/HeroHost
Workbench
Workbench/LibraryPanel
Workbench/PlayerBoardPanel
Workbench/EnemyBoardPanel
Workbench/DetailPanel
Workbench/RecycleZone
Workbench/Feedback_Text
DragLayer
Templates
```

Require all singular serialized references from Task 6, no serialized slot arrays, and five non-null template prefab references. Require `MenuForm.prefab` to contain `Buttons/BattleLab` with visible text `战斗实验室` and a persistent call to `OnBattleLabButtonClick`.

- [ ] **Step 2: Run RED**

Expected: assets are absent.

- [ ] **Step 3: Implement an idempotent builder**

Expose:

```csharp
[MenuItem("Game/Buqi/Rebuild Battle Lab UI")]
public static void BuildAll()
```

Build opaque, non-nested full-width panels with restrained jade/gold accent colors matching existing Buqi UI. Use a 1920x1080 root, 6px-or-less corner assets, stable 1/2/3 span widths, and templates rather than serialized fixed boards.

Use three `ScrollRect` surfaces only: the full item library, preset opponent list, and long detail body. Hero cards, both boards, mode controls, recycle target, feedback, and Start Simulation stay outside scrolling content. Add visible scrollbars and mouse-wheel support through the stock `ScrollRect`; do not add keyboard or input-system references.

`PatchMenuEntry()` must load `MenuForm.prefab`, return without duplication when `Buttons/BattleLab` already exists, otherwise clone `Buttons/Start`, rename it, set anchored Y to `-80`, change the label to `战斗实验室`, shift Setting/About/Quit down by 80, replace only the cloned navigation persistent listener with `OnBattleLabButtonClick`, preserve the click sound listener, save, and unload prefab contents in `finally`.

- [ ] **Step 4: Generate assets with Unity**

Before this command, read the installed Unity Agent Bridge `AGENT.md` required by repository instructions. Run the builder command from Test Commands. Expected: exit `0` and all six new prefab assets exist.

If Unity is unavailable, do not fabricate serialized GUID/fileID graphs by hand. Commit the builder only after recording Prefab generation and EditMode verification as blocked; the feature is not complete until a Unity-capable environment generates and tests the assets.

- [ ] **Step 5: Run prefab tests and commit**

Run the Unity test filter. Expected: zero failures. Commit builder, prefabs, metas, menu prefab, and tests together.

---

### Task 9: Responsive 8/10-Slot Layout and Visual Acceptance

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiBattleLabForm.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleLabUIBuilder.cs`
- Modify: `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabPrefabTests.cs`

- [ ] **Step 1: Add failing layout tests**

Instantiate the form prefab, call a public testable method `ApplyResponsiveLayout(float width, int boardSlotCount)`, and assert:

```text
1280 width, 8 slots: library columns = 2, slot width >= 72, both boards remain one row
1280 width, 10 slots: library columns = 2, slot width >= 60, no horizontal scroll
1920 width, 8 slots: library columns = 4
1920 width, 10 slots: library columns = 4, slot width >= 88
```

For both viewports, transform all four corners of Header, LibraryPanel, PlayerBoardPanel, EnemyBoardPanel, DetailPanel, RecycleZone, and primary command into root-local space and assert every corner is within the root rect. Assert PlayerBoardPanel and EnemyBoardPanel bounds do not overlap.

- [ ] **Step 2: Run RED**

Expected: missing responsive method or failing 10-slot bounds.

- [ ] **Step 3: Implement responsive sizing**

Compute slot width from the actual board host width:

```csharp
float gap = 6f;
float available = boardHost.rect.width - gap * (boardSlotCount - 1);
float slotWidth = available / boardSlotCount;
```

Use 2 library columns below 1600 logical pixels and 4 at or above 1600. Never scale text with viewport width. Resize board item cards from `slotWidth * span + gap * (span - 1)` and preserve the same anchor calculation on both sides.

- [ ] **Step 4: Regenerate, test, and visually inspect**

Regenerate the form with Unity, run prefab tests, then open the prefab at 1280x720 and 1920x1080. Verify no text overlaps, both boards remain visible, 10 slots remain one row, scrollbars only appear in library/opponent/detail areas, and the primary simulation button does not move when labels change.

- [ ] **Step 5: Commit**

```powershell
git add Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BuqiBattleLabForm.cs Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleLabUIBuilder.cs Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiBattleLabForm.prefab Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleLabPrefabTests.cs
git commit -m "test(buqi): verify battle lab responsive layout"
```

---

### Task 10: End-to-End Regression and Handoff

**Files:**
- None: verification only. A failure returns to the exact files listed by its owning task.

- [ ] **Step 1: Run all non-Unity gates**

```powershell
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- battle-lab
dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -- verify
dotnet build Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj -warnaserror
git diff --check
```

Expected: all exit `0`; battle hashes match; no warnings or whitespace errors.

- [ ] **Step 2: Run Unity EditMode and builder gates**

Use the exact Unity commands from Test Commands. Expected: builder and tests exit `0`. If Unity is absent, report these gates as unrun; do not claim the UI is visually accepted.

- [ ] **Step 3: Manual mouse acceptance**

In Play Mode verify:

1. Main menu Battle Lab opens HeroSelection and Start still opens the normal run.
2. Each hero displays approved numbers and enters Workbench.
3. Every configured item is visible; clicking only fixes details.
4. Dragging the same item twice creates two instances.
5. 1/2/3-slot placement previews cover exact cells; illegal drops preserve state and show a reason.
6. Preset enemies are read-only; custom enemies select a hero and accept items.
7. Both boards remain visible at 1280x720 and 1920x1080.
8. Start Simulation opens BattleForm; Back returns with both boards unchanged.
9. Closing and reopening the lab starts a fresh session and does not alter coins, run phase, rewards, or saves.

- [ ] **Step 4: Review scope and generated artifacts**

```powershell
git status --short
git diff --stat origin/codex/UnityCode...HEAD
git diff --name-only origin/codex/UnityCode...HEAD
```

Confirm no changes exist under supply generation, economy pricing, event/training runtime, battle simulation rules, `output/`, or `imagegen/`.

- [ ] **Step 5: Route any failure back to its owning task**

Do not create a broad final patch. A core failure returns to Tasks 1-4, an interaction failure to Tasks 5-6, a registration failure to Task 7, and an asset/layout failure to Tasks 8-9. After the focused fix and its task-specific commit, rerun Task 10 from Step 1.

- [ ] **Step 6: Report**

Report commit hashes, changed files grouped by pure core/UI/assets/tests, non-Unity command results, Unity results or the explicit missing-Unity gap, the current 8-slot runtime limitation, and the exact 10-slot migration dependencies named in the design.
