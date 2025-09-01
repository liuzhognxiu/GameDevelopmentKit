# UXTool Features for UnityMCP Integration

This document outlines the features of the UXTool library that can be integrated into UnityMCP to automate the process of UI assembly.

## UI Adaptation

The `UIAdapter` feature provides a comprehensive system for adapting your UI to different screen sizes and aspect ratios. This feature can be integrated into UnityMCP to automatically adapt the UI of your prefabs to different screen sizes.

### Integration Steps

1.  **Add UIAdapter Component:** Add the `UIAdapter` component to the root canvas of your prefabs.
2.  **Configure UIAdapter:** Configure the `UIAdapter` component with the desired design aspect ratio and other settings.
3.  **Add IgnoreUIAdapter Component:** For elements that need to ignore the safe area, such as backgrounds, add the `IgnoreUIAdapter` component.

## Color and Gradient Management

The `UIColor` feature provides a system for managing colors and gradients in your project. This feature can be integrated into UnityMCP to automatically apply the correct colors and gradients to your UI elements.

### Integration Steps

1.  **Create Color and Gradient Assets:** Create `UIColorAsset` and `UIGradientAsset` ScriptableObjects to store your color and gradient palettes.
2.  **Use UIColorUtils:** Use the `UIColorUtils` class to get the colors and gradients from your assets and apply them to your UI elements.

## Widget Generation

The `UXTools` directory contains a widget generator that can be used to generate custom UI widgets. This feature can be integrated into UnityMCP to automate the process of creating new UI widgets.

### Integration Steps

1.  **Define Widget Templates:** Define a set of widget templates that can be used to generate new widgets.
2.  **Use WidgetGenerator:** Use the `WidgetGenerator` to generate new widgets from your templates.

## Localization

The `EditorLocalization` feature provides a comprehensive localization system for the editor tools. This feature can be integrated into UnityMCP to localize the UI of your editor tools.

### Integration Steps

1.  **Create Localization Data:** Create JSON files with the localization data for each language.
2.  **Use EditorLocalization:** Use the `EditorLocalization` class to get the localized strings for your editor tools.

## Automatic UI Assembly

Based on the analysis of the UXTool library, the following features can be integrated into UnityMCP to achieve automatic UI assembly:

*   **UI Adaptation:** The `UIAdapter` system can be used to automatically adapt the UI to different screen sizes and aspect ratios.
*   **Color and Gradient Management:** The `UIColor` system can be used to automatically apply the correct colors and gradients to the UI elements.
*   **Widget Generation:** The `WidgetGenerator` can be used to automatically generate custom UI widgets from a set of predefined templates.
*   **Localization:** The `EditorLocalization` system can be used to automatically localize the UI of the editor tools.

By integrating these features into UnityMCP, you can create a powerful and flexible system for automatically assembling UIs. This will save you a lot of time and effort, and it will also help you to create more consistent and professional-looking UIs.