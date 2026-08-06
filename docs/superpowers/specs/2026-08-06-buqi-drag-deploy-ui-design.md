# Buqi Drag Deploy UI Design

**Date:** 2026-08-06  
**Status:** Approved  
**Target:** Unity 6000.3.21f1, GameHot/UGF, 1920x1080 landscape

## Goal

Add an independent full-screen deployment UI where players can drag equipment from a five-slot storage area onto a continuous eight-slot board. The interface must also support moving deployed equipment, returning it to storage, resetting the draft, and confirming the result back into the current RunShell demo session.

## Approved Layout

Use layout A, an independent `BuqiDragDeployForm`:

- Header: round, coins, wins, lives, and opponent context.
- Left column: five stable storage slots.
- Center: one continuous eight-slot board with span preview.
- Right column: selected equipment details and validation feedback.
- Bottom command bar: back, reset, and confirm deployment.

The form uses UI ID `108` and asset path `Hot/Buqi/BuqiDragDeployForm`.

## Interaction

The following flows are required:

1. Drag an equipment item from storage to a board slot.
2. Drag an equipment item already on the board to another board slot.
3. Drag a board item back to an empty storage slot to remove it from deployment.
4. Click an equipment item and then click a destination as a complete non-drag alternative.
5. Reset restores the snapshot received when the form opened.
6. Cancel closes the form without changing RunShell.
7. Confirm validates the complete snapshot and synchronizes it back to RunShell.

During a board drag, every slot in the proposed equipment span is previewed. Legal targets use a green marker and a text label. Illegal targets use a red marker, a non-color invalid symbol, and a specific reason. An illegal drop leaves the model unchanged and returns the visual item to its source.

## Architecture

### Pure Deployment Model

`BuqiDragDeployController` owns an immutable deployment view copied after each accepted command. It receives:

- equipment definitions from `BuqiUIDemoCatalog`;
- exactly eight board slots;
- exactly five storage slots.

It validates item existence, source ownership, destination range, equipment span, overlap, storage capacity, and duplicate placement. UI drag objects never mutate slot data directly.

The deployment model exposes source and destination references rather than GameObjects. A move is accepted only after the controller produces a complete legal next snapshot.

### Runtime UI

`BuqiDraggableItemWidget` implements Unity pointer and drag interfaces. It creates a drag visual under a top-level drag layer and reports hover/drop intent to the form.

`BuqiDeploySlotWidget` represents either one board slot or one storage slot. It renders normal, selected, legal target, illegal target, occupied continuation, and locked states using text/symbol channels in addition to color.

`BuqiDragDeployForm` owns the controller and renders its immutable view. It handles drag start, target preview, drop, click fallback, reset, cancel, and confirm. Closing always clears drag visuals, callbacks, and selected state.

### RunShell Synchronization

`BoardEditorWidget` submits `OpenDragDeploy`. `BuqiRunShellForm` intercepts this UI-only command and opens `BuqiDragDeployForm` with typed data:

```csharp
public sealed class BuqiDragDeployOpenData
{
    public BuqiUIDemoCatalog Catalog;
    public IReadOnlyList<string> Board;
    public IReadOnlyList<string> Storage;
    public Action<BuqiDeploymentSnapshot> Confirmed;
}
```

On confirm, RunShell submits `ApplyDeployment` to `BuqiUIDemoController`. The controller revalidates slot counts, item IDs, spans, overlap, and storage ownership before replacing its board/storage snapshot. A rejected synchronization preserves the previous RunShell view and displays the rejection reason.

The feature remains Demo-only. It does not write formal RunState, save data, economy ledgers, battle RNG, or generated configuration state.

## Prefabs And Configuration

Create:

- `Assets/Res/UI/UIForm/Hot/Buqi/BuqiDragDeployForm.prefab`
- `Assets/Res/UI/UIPrefab/Buqi/BuqiDraggableItemWidget.prefab`
- `Assets/Res/UI/UIPrefab/Buqi/BuqiDeploySlotWidget.prefab`

Register:

```text
108, BuqiDragDeployForm, Drag deployment, Hot/Buqi/BuqiDragDeployForm, Pop, false, true
```

All prefabs are generated through an Editor Builder. Prefab YAML is not edited manually.

## Error Handling

Player-facing failures include:

- equipment no longer exists;
- source slot does not own the equipment;
- target slot is outside the board;
- equipment span exceeds the board;
- target span overlaps another equipment item;
- storage has no empty slot;
- synchronized snapshot is stale or invalid.

Drag failure is local and reversible. Configuration or typed-open-data failures show a diagnostic panel and close only when no usable deployment state can be created.

## Verification

Automated EditMode coverage must include:

- deterministic initial deployment view;
- legal storage-to-board placement;
- multi-slot span preview and placement;
- overlap and out-of-range rejection without mutation;
- board-to-board move;
- board-to-storage removal;
- click fallback producing the same result as drag;
- reset restoring the opening snapshot;
- confirm callback invoked once;
- RunShell `ApplyDeployment` synchronization;
- complete serialized prefab bindings and stable dimensions;
- reopen clearing old callbacks and transient drag state.

Unity acceptance must verify at 1920x1080 that the drag visual is visible, slot dimensions do not shift, illegal feedback does not overlap controls, and both drag and click workflows can confirm the same deployment.

## Non-Goals

- Physics-based dragging or world-space equipment placement.
- Touch gesture variants beyond normal Unity pointer events.
- Selling equipment by dragging.
- Formal save/load integration.
- Replacing the existing click-first board command contract outside this form.
