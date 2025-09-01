# UXTool 中文文档

本文档概述了UXTool库，这是一套用于在Unity中创建UI的综合性工具和组件。

## Runtime

`Runtime`目录包含UXTool库的核心组件和功能。

### Common

`Common`目录包含一组通用的实用程序类和扩展方法。

*   **UXTool.cs:** 这是库的主入口点。它提供了初始化和清除库资源的方法。
*   **ResourceManager.cs:** 这是`GameFramework.Resource.ResourceComponent`和`UnityGameFramework.Runtime.ResourceComponent`的包装器。它提供了一种加载和卸载资源的简单方法。
*   **UnityExtension:** 此目录包含一组用于各种Unity类（如`RectTransform`、`Transform`和`Array`）的扩展方法。

### Feature

`Feature`目录包含许多可用于增强UI的功能。

*   **Multi_Platform:** 此功能提供了一组用于处理特定平台逻辑的实用程序函数。
*   **Reddot:** 此功能提供了一个用于管理UI中红点通知的系统。
*   **UIAdapter:** 此功能提供了一个全面的系统，用于使您的UI适应不同的屏幕尺寸和宽高比。
*   **UIBeginnerGuide:** 此功能提供了一个用于创建和管理新手指南或教程的系统。
*   **UIColor:** 此功能提供了一个用于管理项目中颜色和渐变的系统。

### UXGUI

`UXGUI`目录包含一组可用于创建UI的自定义UI组件。

*   **Attributes:** 此目录包含许多可用于在检查器中自定义组件外观和行为的属性。
*   **Common:** 此目录包含一组用于UXGUI系统的通用实用程序类。
*   **Components:** 此目录包含许多自定义UI组件，例如`UXImage`、`UXText`和`UXScrollRect`。
*   **UIStateAnimator:** 此组件可用于创建复杂的UI动画。

## Editor

`Editor`目录包含一组用于UXTool库的自定义编辑器和工具。

### Common

`Common`目录包含一组用于编辑器的通用实用程序类和工具。

*   **Config:** 此目录包含UXTool的配置文件。
*   **Data:** 此目录包含UXTool的数据文件。
*   **EditorLocalization:** 此目录包含一个用于编辑器工具的综合性本地化系统。
*   **TableList:** 此目录包含一个功能强大且灵活的表列表系统，用于在检查器中显示列表和数组。
*   **Utils:** 此目录包含各种用于编辑器的实用程序函数。

### Feature

`Feature`目录包含一组用于`Runtime`目录中功能的自定义编辑器。

*   **Reddot:** 此目录包含一个用于`Reddot`组件的自定义编辑器。
*   **UIBeginnerGuideEditor:** 此目录包含一个用于创建和编辑新手指南的综合性编辑器。
*   **UIColor:** 此目录包含一组用于颜色和渐变系统的编辑器窗口和自定义编辑器。

### Tools

`Tools`目录包含一组用于UXTool的独立工具。

*   **_InHouse:** 此目录包含一个引用查找器工具，可用于查找对所选资源的所有引用。
*   **UXTools:** 此目录包含各种用于UI开发的工具，例如小部件生成器和用于管理设置的窗口。

### UXGUI

`UXGUI`目录包含一组用于UXGUI组件的自定义编辑器。

*   **Attributes:** 此目录包含一组用于NaughtyAttributes系统的自定义属性绘制器。
*   **Common:** 此目录包含一组用于UXGUI编辑器系统的通用实用程序类。
*   **Inspector:** 此目录包含许多用于各种UXGUI组件的自定义编辑器。
*   **Localization:** 此目录包含UXGUI编辑器系统的本地化数据。

## 如何操作

### 使用UIAdapter

1.  将`UIAdapter`组件添加到您的根Canvas上。
2.  根据需要调整`UIAdapter`组件的属性，例如`designAspectRatio`。
3.  对于需要忽略安全区域的元素（例如背景），请添加`IgnoreUIAdapter`组件。

### 使用Reddot

1.  将`Reddot`组件添加到您想要显示红点的GameObject上。
2.  设置`Reddot`组件的`path`属性。路径是一个字符串，表示红点在红点树中的位置。
3.  使用`ReddotManager`类来设置红点的状态。例如，要显示一个红点，您可以调用`ReddotManager.SetRedDotData(true, "path/to/reddot")`。

### 使用UIBeginnerGuide

1.  创建一个`UIBeginnerGuideDataList` ScriptableObject来存储您的指南数据。
2.  对于每个指南步骤，创建一个`UIBeginnerGuideData`对象并配置其属性，例如指南ID、完成类型和模板预制件。
3.  使用`UIBeginnerGuideManager`类来显示指南。例如，要显示一个指南，您可以调用`UIBeginnerGuideManager.Instance.ShowGuideList(guideDataList)`。

### 使用UIColor

1.  创建一个`UIColorAsset`或`UIGradientAsset` ScriptableObject来存储您的颜色或渐变。
2.  使用`UIColorConfigWindow`编辑器窗口来编辑您的颜色和渐变。
3.  使用`UIColorUtils`类来获取项目中的颜色或渐变。例如，要获取颜色，您可以调用`UIColorUtils.GetDefColor(UIColorGenDef.UIColorConfigDef.Def_COLOR1)`。

## 更多细节

### UIAdapter

`UIAdapter`系统包含以下组件：

*   **UIAdapter:** 主组件，用于处理安全区域适应。
*   **IgnoreUIAdapter:** 忽略安全区域，用于背景等元素。
*   **UIAdapterAutoFit & UIAdapterAutoFitScale:** 自动调整`RectTransform`的大小或缩放以适应画布。
*   **UIAdapterMatchMode:** 根据屏幕宽高比调整`CanvasScaler`的`matchWidthOrHeight`属性。
*   **UIAdapterPlatform:** 允许您为不同平台（PC、移动设备、控制台）指定不同的UI缩放比例。
*   **UIAdapterRectByMode:** 允许您为移动设备和PC定义两组不同的`RectTransform`属性（锚点、轴心、缩放等）。
*   **UIAdapterScaleScreenRate:** 根据屏幕宽高比缩放`RectTransform`。
*   **UIDeviceSimulatorChangeController:** 编辑器专用脚本，与Unity设备模拟器集成，可在模拟器中更改设备时刷新UI适配器。

### UIBeginnerGuide

`UIBeginnerGuide`系统包含以下组件：

*   **UIBeginnerGuideManager:** 控制新手指南流程的单例管理器。
*   **UIBeginnerGuideDataList:** 用于对相关指南进行分组的`MonoBehaviour`。
*   **UIBeginnerGuideData:** 包含单个指南步骤所有数据的可序列化类。
*   **UIBeginnerGuideBase:** 所有新手指南预制件的基类。
*   **UIBeginnerGuide:** 新手指南的默认实现。
*   **UIBeginnerGuidePreviewLauncher:** 用于在编辑器中预览特定指南的仅编辑器脚本。

#### UIBeginnerGuide Widgets

*   **GuideArrowLine:** 显示带可选线条的箭头。
*   **GuideGesture:** 显示手势动画，例如单击、拖动或轻扫。
*   **GuideHighLight:** 在特定UI元素上创建高亮效果。
*   **GuideHighLightButton:** 可与`GuideHighLight`小部件一起使用的自定义按钮。
*   **GuideSelfDefined:** 允许您向新手指南添加自定义UI元素。
*   **GuideTargetStroke:** 在目标UI元素周围显示描边。
*   **GuideText:** 显示带标题和内容的文本框。
*   **GuideTransformData:** 可用于存储任何UI元素的变换数据的通用数据类。
*   **SmallArrowData:** 存储`GuideArrowLine`末尾小箭头变换数据的数据类。
*   **GamePad:** 用于在新手指南中显示与手柄相关的说明的系统。

### UIColor

`UIColor`系统包含以下组件：

*   **UIColorAsset & UIGradientAsset:** 用于存储颜色和渐变列表的`ScriptableObject`。
*   **UIColorConfig:** 包含指向颜色和渐变资源的路径。
*   **UIColorGenDef.cs & UIGradientGenDef.cs:** 包含颜色和渐变枚举的生成脚本。
*   **UIColorUtils:** 提供加载颜色和渐变资源以及按枚举值获取特定颜色或渐变的方法的静态类。