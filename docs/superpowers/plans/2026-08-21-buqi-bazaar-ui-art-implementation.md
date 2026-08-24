# Buqi Bazaar UI Art Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate an original cultivation-bazaar UI art set with PixAI and wire it into the current Unity shop, offer-card, and item-detail prefabs without changing shop runtime behavior.

**Architecture:** Four PNG sprites live under one `Buqi/Bazaar` sprite folder: one simple full-bleed backdrop and three 9-slice-compatible frames. Editor builders load those sprites through fixed asset paths and assign them to the same `Image` components that already exist. Checked-in prefabs receive the same GUID references so the result is present before builders are rerun.

**Tech Stack:** PixAI Codex Bridge, PNG, Unity `TextureImporter` sprite metadata, Unity UI `Image`, C# editor builders, PowerShell static contract verification.

**Implementation note (2026-08-25):** PixAI generated all four source assets successfully. Two item-frame attempts on 2026-08-24 failed upstream (`502 upstream_error`, then `503 No available compatible accounts`), so the first integration temporarily used a mechanical composition of the original shelf-frame output. A later independent PixAI request succeeded as `history_49dfb67c-aed7-4098-b12e-696362402633`; that direct result now supplies `item-frame.png`. No external art source was substituted.

---

### Task 1: Add the static art contract

**Files:**
- Create: `Tools/Tests/Verify-BuqiBazaarArt.ps1`
- Test: `Tools/Tests/Verify-BuqiBazaarArt.ps1`

- [ ] **Step 1: Write the failing test**

Create a PowerShell contract that requires these files:

```powershell
$assetNames = @(
    'bazaar-backdrop.png',
    'shop-shelf-panel.png',
    'player-board-panel.png',
    'item-frame.png'
)
```

The test must verify PNG signatures and non-zero dimensions, Unity `.meta` sprite import settings, named asset paths in all relevant builders, and matching sprite GUIDs in checked-in prefabs.

- [ ] **Step 2: Run test to verify it fails**

Run: `pwsh -NoProfile -File Tools/Tests/Verify-BuqiBazaarArt.ps1`

Expected: FAIL because `Assets/Res/UI/UISprite/Buqi/Bazaar` and its four PNG files do not exist.

- [ ] **Step 3: Commit the red contract with the final implementation**

The repository should not be left with a failing contract between commits. Stage this test together with Tasks 2-4 after it turns green.

### Task 2: Generate and import four original PixAI sprites

**Files:**
- Create: `Unity/Assets/Res/UI/UISprite/Buqi.meta`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar.meta`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/bazaar-backdrop.png`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/bazaar-backdrop.png.meta`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/shop-shelf-panel.png`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/shop-shelf-panel.png.meta`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/player-board-panel.png`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/player-board-panel.png.meta`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/item-frame.png`
- Create: `Unity/Assets/Res/UI/UISprite/Buqi/Bazaar/item-frame.png.meta`

- [ ] **Step 1: Generate the backdrop through PixAI**

Use an original prompt for an empty, front-facing cultivation bazaar interior with obsidian, oxidized bronze, jade cloth, restrained cinnabar light, clear UI-safe center, and no characters, cards, text, logos, numbers, or copied game assets. Generate one `16:9` PNG.

- [ ] **Step 2: Generate the shelf and board panels through PixAI**

Generate two front-facing, empty, 9-slice-compatible `3:2` frames on transparent backgrounds. The shelf uses oxidized bronze and dark lacquer; the player board uses dark wood, pale jade inlay, and subtle cloth texture. Both must have uniform edges and quiet centers.

- [ ] **Step 3: Generate the item frame through PixAI**

Generate one front-facing `2:3` empty item frame on a transparent background, with consistent corner ornaments, a quiet central image well, and no text or symbols. Reuse it for both offer-card and detail-card framing.

- [ ] **Step 4: Export and normalize assets**

Export the successful PixAI results into the Unity sprite directory. Preserve PNG alpha. Use unique deterministic Unity GUIDs and `textureType: 8`; set `spriteBorder` to zero for the backdrop and non-zero for the three sliced frames.

- [ ] **Step 5: Run the image portion of the contract**

Run: `pwsh -NoProfile -File Tools/Tests/Verify-BuqiBazaarArt.ps1`

Expected: asset existence and importer checks pass; builder/prefab wiring checks still fail.

### Task 3: Wire sprites into editor builders

**Files:**
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBuildWidgetBuilder.cs`
- Modify: `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiPopupUIBuilder.cs`
- Test: `Tools/Tests/Verify-BuqiBazaarArt.ps1`

- [ ] **Step 1: Add fixed sprite paths and a loader**

Each builder that owns an affected prefab loads only the sprites it uses:

```csharp
private static Sprite LoadSprite(string path)
{
    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    if (sprite == null)
        throw new InvalidOperationException(string.Format("Missing Buqi UI sprite at {0}.", path));
    return sprite;
}
```

- [ ] **Step 2: Apply the shop sprites**

`BuqiFullUIBuilder` assigns the backdrop to the `ShopWidget` root and the shelf/board frames to `SellDropZone` and `PlayerBoard`. The backdrop uses `Image.Type.Simple`; frames use `Image.Type.Sliced`. Existing serialized widget bindings remain unchanged.

- [ ] **Step 3: Apply the item frame**

`BuqiBuildWidgetBuilder` assigns `item-frame.png` to the offer-card background. `BuqiPopupUIBuilder` assigns the same frame to the item-detail `ItemCard`. Retain existing tinting and raycast behavior.

- [ ] **Step 4: Run the builder portion of the contract**

Run: `pwsh -NoProfile -File Tools/Tests/Verify-BuqiBazaarArt.ps1`

Expected: builder checks pass; prefab GUID checks still fail.

### Task 4: Update checked-in prefabs and verify

**Files:**
- Modify: `Unity/Assets/Res/UI/UIPrefab/Buqi/Stages/ShopWidget.prefab`
- Modify: `Unity/Assets/Res/UI/UIPrefab/Buqi/OfferCardWidget.prefab`
- Modify: `Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiItemDetailForm.prefab`
- Test: `Tools/Tests/Verify-BuqiBazaarArt.ps1`

- [ ] **Step 1: Replace built-in sprite references**

Update only the affected `Image.m_Sprite`, `Image.m_Type`, and tint fields. Do not change MonoBehaviour bindings, hierarchy names, buttons, or trading scripts.

- [ ] **Step 2: Run the complete static contract**

Run: `pwsh -NoProfile -File Tools/Tests/Verify-BuqiBazaarArt.ps1`

Expected: PASS with four sprites, three builders, and three prefabs verified.

- [ ] **Step 3: Inspect generated pixels**

Open all four local PNGs and verify they are nonblank, original, free of visible text/logos/watermarks, correctly framed, and suitable for their declared aspect ratios.

- [ ] **Step 4: Review the Git diff**

Run: `git diff --check`

Run: `git status --short`

Expected: only the plan, contract, four sprite files and metadata, three builders, and three prefabs are changed. Existing untracked `output/imagegen` directories remain untouched.

- [ ] **Step 5: Commit**

```powershell
git add docs/superpowers/plans/2026-08-21-buqi-bazaar-ui-art-implementation.md Tools/Tests/Verify-BuqiBazaarArt.ps1 Unity/Assets/Res/UI/UISprite/Buqi Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBuildWidgetBuilder.cs Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiPopupUIBuilder.cs Unity/Assets/Res/UI/UIPrefab/Buqi/Stages/ShopWidget.prefab Unity/Assets/Res/UI/UIPrefab/Buqi/OfferCardWidget.prefab Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiItemDetailForm.prefab
git commit -m "feat(buqi): add original bazaar UI art"
```
