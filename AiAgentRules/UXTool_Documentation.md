
# UXTool Documentation

This document provides an overview of the UXTool library, a comprehensive set of tools and components for creating UIs in Unity.

## Runtime

The `Runtime` directory contains the core components and features of the UXTool library.

### Common

The `Common` directory contains a set of common utility classes and extension methods.

*   **UXTool.cs:** This is the main entry point of the library. It provides methods to initialize and clear the library's resources.
*   **ResourceManager.cs:** This is a wrapper around the `GameFramework.Resource.ResourceComponent` and `UnityGameFramework.Runtime.ResourceComponent`. It provides a simple way to load and unload assets.
*   **UnityExtension:** This directory contains a set of extension methods for various Unity classes, such as `RectTransform`, `Transform`, and `Array`.

### Feature

The `Feature` directory contains a number of features that can be used to enhance your UIs.

*   **Multi_Platform:** This feature provides a set of utility functions for handling platform-specific logic.
*   **Reddot:** This feature provides a system for managing red dot notifications in the UI.
*   **UIAdapter:** This feature provides a comprehensive system for adapting your UI to different screen sizes and aspect ratios.
*   **UIBeginnerGuide:** This feature provides a system for creating and managing beginner guides or tutorials.
*   **UIColor:** This feature provides a system for managing colors and gradients in your project.

### UXGUI

The `UXGUI` directory contains a set of custom UI components that can be used to create your UIs.

*   **Attributes:** This directory contains a number of attributes that can be used to customize the appearance and behavior of your components in the inspector.
*   **Common:** This directory contains a set of common utility classes for the UXGUI system.
*   **Components:** This directory contains a number of custom UI components, such as `UXImage`, `UXText`, and `UXScrollRect`.
*   **UIStateAnimator:** This component can be used to create complex UI animations.

## Editor

The `Editor` directory contains a set of custom editors and tools for the UXTool library.

### Common

The `Common` directory contains a set of common utility classes and tools for the editor.

*   **Config:** This directory contains the configuration files for the UXTool.
*   **Data:** This directory contains the data files for the UXTool.
*   **EditorLocalization:** This directory contains a comprehensive localization system for the editor tools.
*   **TableList:** This directory contains a powerful and flexible table list system that is used to display lists and arrays in the inspector.
*   **Utils:** This directory contains a variety of utility functions for the editor.

### Feature

The `Feature` directory contains a set of custom editors for the features in the `Runtime` directory.

*   **Reddot:** This directory contains a custom editor for the `Reddot` component.
*   **UIBeginnerGuideEditor:** This directory contains a comprehensive editor for creating and editing beginner guides.
*   **UIColor:** This directory contains a set of editor windows and custom editors for the color and gradient system.

### Tools

The `Tools` directory contains a set of standalone tools for the UXTool.

*   **_InHouse:** This directory contains a reference finder tool that can be used to find all references to a selected asset.
*   **UXTools:** This directory contains a variety of tools for UI development, such as a widget generator and a window for managing settings.

### UXGUI

The `UXGUI` directory contains a set of custom editors for the UXGUI components.

*   **Attributes:** This directory contains a set of custom property drawers for the NaughtyAttributes system.
*   **Common:** This directory contains a set of common utility classes for the UXGUI editor system.
*   **Inspector:** This directory contains a number of custom editors for the various UXGUI components.
*   **Localization:** This directory contains the localization data for the UXGUI editor system.
