# 模块 12：平台与 SDK 适配

> Catalog ID: `UNITY-12`  
> 状态：`verified`  
> 最后核验：`2026-08-04`  
> 适用模式：Unity Editor / Android / iOS（当前为未完成适配骨架）

## 模块定位

平台目录定义广告、埋点、设备标识和评分等能力的策略接口，并按 Unity 编译平台选择 Editor/Android/iOS 实现。当前对业务公开的 `PlatformComponent` 只转发了一个有缺陷的 TrackEvent 重载，因此本模块应视为项目接 SDK 的骨架，而不是可直接使用的完整平台服务。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| 接口 | `Unity/Assets/Scripts/Game/Platform/IPlatform.cs` | 平台能力契约 |
| 组件 | `Unity/Assets/Scripts/Game/Platform/PlatformComponent.cs` | 编译期选择实现，业务经 `GameEntry.Platform` 访问 |
| Editor | `Unity/Assets/Scripts/Game/Platform/PlatformEditor.cs` | 日志模拟与多数空实现 |
| Android | `Unity/Assets/Scripts/Game/Platform/PlatformAndroid.cs` | Activity Java 调用骨架 |
| iOS | `Unity/Assets/Scripts/Game/Platform/PlatformIOS.cs` | `__Internal` 和 ThinkingAnalytics 骨架 |
| 注册 | `Unity/Assets/Scripts/Game/Base/GameEntry.Game.cs` | 暴露 PlatformComponent |
| 挂载 | `Unity/Assets/Res/GameEntry.prefab` | 当前组件实例 |

## 依赖关系

- 编译选择顺序：`UNITY_EDITOR` -> Editor；`UNITY_ANDROID` -> Android；`UNITY_IOS` -> iOS；其他平台回退 Editor。
- Android 实现依赖 `UnityPlayer.currentActivity` 上的项目自定义 Java 方法。
- iOS 实现依赖 `__Internal` 原生符号和 `ThinkingAnalytics` 命名空间。
- 当前仓库未找到对应 Android/iOS 原生插件源码或 ThinkingAnalytics 资产，平台构建依赖外部注入或尚未完成。
- 目前仓库内没有 `GameEntry.Platform` 的业务调用点。

## 入口与调用链

组件构造时按条件编译创建一个只读 `IPlatform`。当前唯一公开链路是：

```text
GameEntry.Platform.TrackEvent(eventName, key, value)
  -> new Dictionary<string, object>()
  -> m_Platform.TrackEvent(eventName, emptyDictionary)
```

该实现没有把 `key` 和 `value` 放进字典。接口中的 Init、广告、返回键、包 ID、设备 ID 和评分方法没有 PlatformComponent 转发，业务代码不能通过 `GameEntry.Platform` 调用它们。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `IPlatform` | 定义 Init、广告、埋点、设备和评分能力 | 只是契约，不等于组件已公开 |
| `PlatformComponent.TrackEvent` | 当前唯一公开转发 | 丢弃 key/value，是现有缺陷 |
| `PlatformEditor` | `CanShowRewardAd=true`、设备名、埋点日志 | `Init` 前 StringBuilder 为 null；ShowRewardAd 不回调 |
| `PlatformAndroid` | 调用 Activity 的同名 Java 方法 | 仅 Android Player 编译；Init 失败后成员可能为 null |
| `PlatformIOS` | 调用 C 函数、ThinkingAnalytics | 仅 iOS Player 编译；当前源码依赖不闭合 |

## 数据与生命周期

- `PlatformComponent` 没有 `Awake/Start` 调用 `m_Platform.Init()`，因此现状下内部实现不会自动初始化。
- Editor `TrackEvent` 依赖 Init 创建 StringBuilder，但组件没有调用 Init；若公开转发被调用，可能触发 NullReferenceException。
- Android Init 捕获 Java 获取异常并记录日志，但之后的方法不检查 Activity 是否为空。
- Android TrackEvent 实际 SDK 调用已注释；iOS TrackEvent 复制属性后调用 ThinkingAnalytics。
- Editor 的延迟激励回调协程没有被 `ShowRewardAd` 启动；iOS 激励回调也被注释。

## 开发扩展步骤

1. 明确目标 SDK 和原生契约，为接口能力定义成功、失败、取消回调，不要只传广告 tag。
2. 在 `PlatformComponent` 增加明确的初始化生命周期与全部必要转发；TrackEvent 应实际填充属性字典或直接接受字典。
3. 为 Editor 实现可配置模拟结果，确保回调链在无 SDK 时也可测试。
4. 添加 Android AAR/Java 或 iOS `.mm/.framework`，并验证导出工程中原生符号和方法名。
5. 清理 iOS 的陈旧依赖，再分别执行 Editor、Android、iOS 编译验证。

## 约束与常见错误

- 旧草稿中的 `GameEntry.Platform.Init()`、`ShowRewardAd()`、`GetDeviceId()` 等调用当前均不存在于 PlatformComponent，不能编译。
- 当前 `TrackEvent(eventName,key,value)` 会丢弃属性，且未初始化实现；不要用于生产埋点。
- `PlatformIOS.CanAppRate` 调用不存在的 `GameEntry.Platform.GetPkgId()`，并引用仓库中不存在的 `GameEntry.DataTable`、`DRAuthenticationSwitch`；iOS 条件编译当前不能通过源码闭包验证。
- 仓库未发现 `ThinkingAnalytics` 或对应原生函数实现；iOS 构建前必须补依赖或移除代码。
- Android 代码假设 currentActivity 直接实现 PascalCase 方法，普通 Unity Activity 不提供这些方法。
- `IPlatform` 修改会要求三个实现同步更新；必须分别做平台编译，而不能只以 Editor 编译通过为准。

## 验证方法

1. Editor 中先补齐组件 Init/转发后，验证模拟广告成功/失败/取消和埋点属性。
2. 用 Android Player 构建并在真机核对 currentActivity 类、Java 方法签名和空值处理。
3. 用 iOS Player 编译，确认 ThinkingAnalytics 引用、`__Internal` 符号及缺失类型问题全部解决。
4. 搜索业务侧确保只调用 PlatformComponent 公开 API，不直接 new 平台实现。
5. 当前核验仅证明骨架和缺陷边界；未执行移动平台构建，不得据此宣称 SDK 已接通。

## 源码证据

- `Unity/Assets/Scripts/Game/Platform/IPlatform.cs`：完整策略契约。
- `Unity/Assets/Scripts/Game/Platform/PlatformComponent.cs`：条件选择和唯一公开转发。
- `Unity/Assets/Scripts/Game/Platform/PlatformEditor.cs`：未自动初始化和空模拟行为。
- `Unity/Assets/Scripts/Game/Platform/PlatformAndroid.cs`：Activity 方法约定及被注释埋点。
- `Unity/Assets/Scripts/Game/Platform/PlatformIOS.cs`：缺失组件 API、旧 DataTable 类型和外部 SDK 依赖。
- `Unity/Assets/Res/GameEntry.prefab`：PlatformComponent 的真实挂载。

## 关联知识

- 上游：`UNITY-01`、`PACKAGE-01`
- 下游：后续具体渠道/SDK 业务（当前无仓库调用者）
