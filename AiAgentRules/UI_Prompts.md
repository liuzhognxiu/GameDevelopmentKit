### UI Framework Core Specifications

Before generating any UI code, it's crucial to understand and adhere to the following framework rules. All generated code must comply with these specifications.

1.  **UI Forms (`AUGuiForm`)**: 
    *   All full-screen UI windows or pop-ups must inherit from the `Game.UI.AUGuiForm` base class.
    *   Each form must have a unique integer ID registered in the `Game.UI.UIFormId` static class. This ID is essential for the `UIManager` to manage the form's lifecycle.
    *   Core logic should be placed within the overridden lifecycle methods: `OnOpen`, `OnClose`, `OnPause`, `OnResume`, and `OnUpdate`.
    *   **Namespace Requirement**: Subclasses of `AUGuiForm` must include `using Game;` but should **not** include `using Game.UI;`.

2.  **UI Widgets (`AUIWidget`)**:
    *   Reusable UI components, such as list items, icons, or complex controls, must inherit from the `Game.UI.AUIWidget` base class.
    *   Widgets are typically managed by a parent `AUGuiForm` and should not contain complex game logic themselves.

3.  **UI Binding (`MonoCodeBind`)**:
    *   The framework uses a source generator system called `MonoCodeBind` to automatically inject UI component references.
    *   To enable this for a form or widget, add the `[MonoCodeBind('_')]` attribute to the class.
    *   Declare private fields for your UI components (e.g., `Button`, `Image`, `TextMeshProUGUI`) with a `_` prefix (e.g., `_closeButton`). The source generator will find these and assign them automatically.
    *   You must **never** use `GetComponent` or `FindObjectOfType` manually. The binding is handled automatically by the `MonoCodeBind` attribute.
    *   `.Bind.cs` files are no longer required and should not be generated.

4.  **Event Handling**:
    *   For button clicks, subscribe to the `onClick` event within the `OnOpen` method.
    *   For more complex, asynchronous UI interactions (e.g., waiting for a button click before proceeding), use the `UIExtension.Awaitable` extensions.

5.  **Data-Driven Approach**:
    *   UI Forms should be data-driven. Instead of polling for state changes in their `OnUpdate` method, they should be updated by external systems. 
    *   Use a custom data class passed into the `OnOpen` method (`object userData`) to initialize the UI state.

---

### AI-Assisted UI Development Prompts

Here are example prompts you could use with a coding AI to accelerate UI development within this specific framework.

---

#### **Prompt 1: Create a New UI Form (Basic)**

"Create a new UI form named `UIBackpackForm`. It should:
1.  Inherit from the `AUGuiForm` base class and use the `[MonoCodeBind('_')]` attribute.
2.  Have a new ID `Backpack` with a value of `101` added to the `UIFormId` class.
3.  Include the basic lifecycle methods: `OnOpen`, `OnClose`, `OnUpdate`.
4.  Declare private fields for a `_closeButton` (Button) and an `_itemList` (GameObject)."

---

#### **Prompt 2: Create a Reusable UI Widget**

"Generate a new UI widget named `ItemIconWidget` that inherits from `AUIWidget` and uses `[MonoCodeBind('_')]`. It should contain:
1.  A public method `SetData(ItemData itemData)` that populates the widget.
2.  Declare private fields for a `_icon` (UnityEngine.UI.Image) and `_itemCount` (TMPro.TextMeshProUGUI)."

---

#### **Prompt 3: Refactor a MonoBehaviour into a UI Form**

"Analyze the existing MonoBehaviour script `PlayerHUD.cs`. Refactor its functionality into a new UI Form named `UIPlayerStatusForm` that complies with our framework. The new form should:
1.  Inherit from `AUGuiForm`, use `[MonoCodeBind('_')]`, and get a new ID `PlayerStatus` in `UIFormId`.
2.  Replace the health bar logic. Create a public method `UpdateHealth(float currentHealth, float maxHealth)` to be called by the player's health component.
3.  Replace the crosshair logic. The `OnUpdate` method should manage the crosshair's visibility and accuracy based on player state.
4.  Declare all necessary private UI component fields (e.g., `_healthBarImage`, `_healthText`, `_crosshairImages`)."

---

#### **Prompt 4: Implement a Tabbed Menu**

"Create a `UISettingsForm` that includes a tabbed menu system. The implementation should:
1.  Create a reusable `UITabGroupWidget` that manages a list of tab buttons and their corresponding content pages (GameObjects).
2.  The `UISettingsForm` will contain this `UITabGroupWidget`.
3.  The form should have three tabs: 'Controls', 'Graphics', and 'Audio'.

---

#### **Prompt 5: Implement an EnhancedScroller List**

"In `UIBackpackForm.cs`, implement the `IEnhancedScrollerDelegate` interface to populate an `EnhancedScroller` instance named `_itemListScroller`.
1.  The scroller should display a list of items using the `ItemIconWidget` as its cell view.
2.  Generate the methods `GetNumberOfCells`, `GetCellViewSize`, and `GetCellView`.
3.  Add a `Refresh(List<ItemData> items)` method that reloads the scroller with new data."

---

#### **Prompt 6: Create an Awaitable Confirmation Dialog**

"Using the `UIExtension.Awaitable` extensions, create a new form `UIDialogForm`. Add an async method `ShowAndWaitForConfirm(string message)`. This method should:
1.  Open the dialog form and display the message.
2.  Asynchronously wait for either the `_confirmButton` or `_cancelButton` to be clicked.
3.  Close the form and return a `bool` indicating whether the confirm button was clicked.
4.  Register a new ID `Dialog` in `UIFormId`.
5.  Declare private fields for the message text and the two buttons."