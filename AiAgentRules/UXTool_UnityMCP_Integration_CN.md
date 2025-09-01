
# 用于UnityMCP集成的UXTool功能

本文档概述了可集成到UnityMCP中以自动化UI组装过程的UXTool库的功能。

## UI适配

`UIAdapter`功能提供了一个全面的系统，用于使您的UI适应不同的屏幕尺寸和宽高比。此功能可以集成到UnityMCP中，以自动使您的预制件的UI适应不同的屏幕尺寸。

### 集成步骤

1.  **添加UIAdapter组件：** 将`UIAdapter`组件添加到您的预制件的根画布上。
2.  **配置UIAdapter：** 使用所需的设计宽高比和其他设置配置`UIAdapter`组件。
3.  **添加IgnoreUIAdapter组件：** 对于需要忽略安全区域的元素（例如背景），请添加`IgnoreUIAdapter`组件。

## 颜色和渐变管理

`UIColor`功能提供了一个用于管理项目中颜色和渐变的系统。此功能可以集成到UnityMCP中，以自动将正确的颜色和渐变应用于您的UI元素。

### 集成步骤

1.  **创建颜色和渐变资源：** 创建`UIColorAsset`和`UIGradientAsset` ScriptableObject来存储您的颜色和渐变调色板。
2.  **使用UIColorUtils：** 使用`UIColorUtils`类从您的资源中获取颜色和渐变，并将其应用于您的UI元素。

## 小部件生成

`UXTools`目录包含一个小部件生成器，可用于生成自定义UI小部件。此功能可以集成到UnityMCP中，以自动化创建新UI小部件的过程。

### 集成步骤

1.  **定义小部件模板：** 定义一组可用于生成新小部件的小部件模板。
2.  **使用WidgetGenerator：** 使用`WidgetGenerator`从您的模板中生成新的小部件。

## 本地化

`EditorLocalization`功能为编辑器工具提供了一个全面的本地化系统。此功能可以集成到UnityMCP中，以本地化您的编辑器工具的UI。

### 集成步骤

1.  **创建本地化数据：** 为每种语言创建包含本地化数据的JSON文件。
2.  **使用EditorLocalization：** 使用`EditorLocalization`类获取您的编辑器工具的本地化字符串。

## 自动UI组装

根据对UXTool库的分析，以下功能可以集成到UnityMCP中以实现自动UI组装：

*   **UI适配：** `UIAdapter`系统可用于自动使UI适应不同的屏幕尺寸和宽高比。
*   **颜色和渐变管理：** `UIColor`系统可用于自动将正确的颜色和渐变应用于UI元素。
*   **小部件生成：** `WidgetGenerator`可用于从一组预定义的模板中自动生成自定义UI小部件。
*   **本地化：** `EditorLocalization`系统可用于自动本地化编辑器工具的UI。

通过将这些功能集成到UnityMCP中，您可以创建一个功能强大且灵活的系统，用于自动组装UI。这将为您节省大量时间和精力，还将帮助您创建更一致、更专业的UI。
