# 模块 23：UGF 与扩展库

> Catalog ID: `LIB-01`、`LIB-02`、`LIB-03`  
> 状态：`verified`  
> 最后核验：`2026-08-04`  
> 适用模式：GameHot / ET Client / Unity Runtime / Editor

## 模块定位

本模块是客户端公共底座的三层结构：`GameFramework` 提供不依赖 UnityEngine 的管理器、接口、引用池和任务模型；`UnityGameFramework.Runtime` 用 `GameFrameworkComponent`、Helper 和 Unity 事件把这些管理器接入场景与 MonoBehaviour 生命周期；`UnityGameFramework.Extension` 再提供本仓库需要的 UniTask 等待、AssetSet、代码入口、服务网络、启动加载、资源构建和屏幕适配。

它不包含 GameHot/ET 的业务流程、具体 UI/Entity 逻辑或 Luban 数据。项目能力优先放在 `Unity/Assets/Scripts/Game`；只有可跨业务复用且确实依赖 UGF 的能力才进入 Extension，避免直接修改 GF/UGF 核心后增加上游升级成本。

当前核对按 Catalog 拆开真实接入：`LIB-01` 的 GF 核心不是项目代码直接 new 出来的，而是由 `Unity/Assets/Scripts/Library/UGF/GameFramework.prefab` 提供 Base、Resource、Procedure、Entity、UI 等 Component，并作为嵌套 prefab 接入 `Unity/Assets/Res/GameEntry.prefab`；`LIB-02` 的 UGF Runtime 由 `GameFrameworkComponent.Awake` 注册组件、`BaseComponent.Update/OnDestroy` 驱动 GF 更新和关闭；`LIB-03` 的 Extension 由 `GameEntry.prefab` 直接挂载 CodeRunner、NetworkService、Screen、AssetSet，再经 `Game.GameEntry` 静态缓存和 Procedure 消费。目录存在或程序集引用只能证明代码可编译，不能单独证明运行时已接入。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| GF 核心 | `Unity/Assets/Scripts/Library/UGF/GameFramework` | `noEngineReferences=true` 的纯 C# 程序集；Base、DataNode、Debugger、Download、Entity、Event、FileSystem、Fsm、Localization、Network、ObjectPool、Procedure、Resource、Scene、Setting、Sound、UI、WebRequest |
| UGF Runtime | `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime` | `GameEntry`、`BaseComponent`、各管理器的 Unity Component/Helper、EditorResourceComponent |
| UGF Editor | `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Editor` | Inspector、Resource Collection/Editor/Builder/Analyzer/Pack/Sync 及构建配置路径机制 |
| Extension Runtime | `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime` | AssetCollection、AssetSet、Awaitable、Build、CodeRunner、Collection、Loader、NetworkService、Resource、Screen |
| Extension Editor | `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Editor` | 资源规则/优化/版本分析、AssetCollection 刷新、VFS 合并、构建和默认设置工具 |
| 项目桥接 | `Unity/Assets/Scripts/Game/Base/GameEntry*.cs` | 缓存 UGF 与 Extension 组件并向业务暴露静态入口 |
| 场景配置 | `Unity/Assets/Res/GameEntry.prefab`、`Unity/Assets/Scripts/Library/UGF/GameFramework.prefab` | 项目 prefab 挂载 Game/Extension 组件并嵌套 UGF 底座；UGF prefab 提供 Base、Resource、Procedure、Entity、UI 等内置组件 |
| 编辑器配置 | `Unity/Assets/Res/Editor/Config` | `ResourceCollection.xml`、`ResourceEditor.xml`、`ResourceBuilder.xml`、ResourceRule 与 VersionInfo 数据 |

程序集依赖方向固定为 `GameFramework <- UnityGameFramework.Runtime <- UnityGameFramework.Extension <- Game`；两个 Editor 程序集仅在 Editor 平台编译，运行时程序集不能反向引用 Editor。

## 依赖关系

- `GameFramework.asmdef` 禁用 Engine 引用；UGF Runtime 引用 GF、UnityWebSocket 和 Input System。
- Extension Runtime 引用 GF、UGF Runtime、UniTask 和 UnityWebSocket；源码还使用 Odin 序列化/Inspector，工程必须存在 Odin 插件。
- UGF Editor 引用 Runtime、GF 和 UnityWebSocket；Extension Editor 再引用 UGF Editor、Runtime 和 Extension Runtime。
- `Game.asmdef` 同时引用上述三层；`GameEntry.Builtin.cs` 暴露 18 个内置组件，`GameEntry.Extension.cs` 暴露 AssetSet、CodeRunner、NetworkService、Screen。
- Entity、UI、Scene、Sound、Localization 依赖 `IResourceManager`；Entity/UI 还依赖 `IObjectPoolManager`；Resource 依赖 ObjectPool、FileSystem、Download。组件在 `Start` 中注入这些关系。
- 下游真实调用包括 `ProcedureLaunch` 的 Awaitable 注册、`ProcedureET`/`ProcedureGameHot` 的 CodeRunner、`ProcedureGame` 的 NetworkService，以及项目 UI/Entity/Scene/Resource 封装。

## 入口与调用链

### 注册、更新与关闭

1. 场景实例化 `GameEntry.prefab`；项目层 GameObject 挂 `Game.GameEntry`，其子节点挂 CodeRunner、NetworkService、Screen、AssetSet、Tables、Builtin 等组件，同时嵌套 `GameFramework.prefab` 承载 UGF 内置组件。每个 `GameFrameworkComponent.Awake` 调 `UnityGameFramework.Runtime.GameEntry.RegisterComponent(this)`，同一具体类型只能注册一次。
2. 各内置组件在 `Awake` 调 `GameFrameworkEntry.GetModule<I...Manager>()`。GF 由接口命名约定反射出同命名空间的实现类，例如 `IEventManager -> EventManager`，首次获取时创建。
3. GF 模块按 Priority 降序插入：Event 7、ObjectPool 6、Download 5、FileSystem 4、Resource 3、Scene 2、Fsm 1、普通模块 0、Debugger -1、Procedure -2。
4. Unity `Start` 阶段创建 Helper、注入管理器依赖并读取序列化参数。项目 `Game.GameEntry.Start` 先执行 `InitBuiltinComponents`、`InitExtensionComponents`、`InitGameComponents` 缓存 UGF、Extension 与项目组件，最后调用 Runtime `GameEntry.Initialize()`。
5. 只有 `BaseComponent.Update` 调 `GameFrameworkEntry.Update(Time.deltaTime, Time.unscaledDeltaTime)`；GF 按上述顺序更新。个别 Unity Helper/Extension 另有自己的 MonoBehaviour `Update`。
6. `GameEntry.Shutdown` 销毁 Base 所在 GameObject；`BaseComponent.OnDestroy` 使 GF 按更新顺序的逆序 Shutdown，再清空模块、ReferencePool、Marshal 缓存和日志 Helper。Restart 随后重载场景 0，Quit 退出 Player/Play Mode。

### 资源与编辑器模式

`ResourceComponent.Start` 在普通模式取得 `IResourceManager`，设置只读/读写路径、资源模式、ObjectPool/FileSystem/Download、版本更新参数和加载代理。Editor Resource Mode 则使用 `EditorResourceComponent` 实现的 `IResourceManager` 直接从 AssetDatabase/场景加载；该替身有多项版本与更新 API 不支持，不能用 Editor 模式证明 Package/Updatable 流程正确。

`GameEntryLoader` 是 `GameEntry.prefab` 之前的壳：Editor 模式或无 Launcher 路径时实例化默认 prefab，否则从 Launcher AssetBundle 加载 prefab，卸载 bundle 但保留已加载资源，设置 Editor Resource/CodeBytes 开关后销毁自身。

### Extension 入口

- `ProcedureLaunch.OnEnter` 调 `Awaitable.SubscribeEvent()` 后，Resource/UI/Entity/Scene/Download/WebRequest/Localization 的扩展方法才可等待；框架销毁时 `UnsubscribeEvent()` 仅将 Awaitable 置为无效。
- `CodeRunnerComponent.StartRun(typeName)` 反射 MonoBehaviour 类型并挂到自身 GameObject；`ProcedureET` 只在 `UNITY_ET` 下启动 `ET.Init`，`ProcedureGameHot` 只在 `UNITY_GAMEHOT` 下启动 `Game.Hot.Init`，离开 Procedure 时 `StopRun()` 立即销毁入口组件。随后 ET 的 `CodeLoader` 和 GameHot 的 `Init` 再根据热更开关加载 bytes 或现有程序集。
- `NetworkServiceComponent` 等到首帧末订阅 UGF 网络事件；GameHot 的 `ProcedureGame` 先 `InitServiceNetworkHelper(new NetworkServiceHelper())`，再 `Connect()`，销毁时 `DestroyServiceNetworkHelper()`。`NetworkServiceHelper` 自己创建名为 `WebSocket` 的 UGF Channel、订阅包处理事件，并维护请求响应等待表。
- `AssetSetComponent.Start` 创建多引用对象池并初始化 Resource、私有 FileSystem、Web 三种来源；`Update` 按间隔释放目标已销毁或已换资源的记录。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `GameFrameworkEntry.GetModule<T>()` | 按接口获取或创建唯一 GF 模块 | 只接受 `GameFramework.*` 接口；实现名必须符合 `IName -> Name` |
| `GameFrameworkModule` | 定义 Priority、Update、Shutdown | 内部类型；高优先级先更新、后关闭 |
| `ReferencePool.Acquire/Release<T>` | 复用实现 `IReference` 的小对象 | `Clear` 必须清空全部外部引用；框架关闭时 `ClearAll` |
| `GameEntry.GetComponent<T>()` | 获取已在 Awake 注册的精确组件类型 | 找不到返回 null，不会自动 AddComponent；重复具体类型只记录错误 |
| `BaseComponent` | 初始化 Helper、Unity 全局参数并驱动 GF | `deltaTime` 受 timeScale 影响，`unscaledDeltaTime` 不受影响 |
| `ResourceComponent.LoadAsset/UnloadAsset` | 加载并释放 GF 资源引用 | 成功返回的资源由调用方负责配对 Unload；Unity 主线程使用 |
| `EditorResourceComponent` | Editor 下的 `IResourceManager` 替身 | 仅编辑器直读资源；更新/版本 API 不能等价替代正式资源模式 |
| `Awaitable.*Async` | 将 UGF 回调/事件封装为 UniTask | 必须先 SubscribeEvent；取消语义因底层任务类型而异 |
| `AssetCollection.GetAsset<T>` | 从 Odin 序列化路径字典取直接资源引用 | ScriptableObject 自身被加载时资源引用随之可用，不走 GF Unload |
| `AssetSetComponent.SetBy*` | 合并请求、设置目标并延迟释放 Sprite/Texture | Resource 来源最终 UnloadAsset；File/Web 创建对象最终 Destroy |
| `CodeRunnerComponent` | ET/GameHot 非热更 Mono 入口承载器 | 禁止重复 StartRun/空闲 StopRun；Player 强制 CodeBytesMode=true |
| `NetworkServiceComponent` | 把 UGF Channel 事件转给业务 Helper | Helper 初始化前访问 State/Connect/Send 会失败；事件订阅延后一帧 |
| `ScreenComponent.Set` | 更新安全区、设计分辨率和 CanvasScaler | 当前仅面向 UGUI；屏幕变化后需显式再次 Set |
| `ResourceRuleEditorUtility` / `ResourceBuildHelper` | 刷新资源集合、优化并驱动资源构建 | Editor-only；实际配置路径由 `GameFrameworkConfigs` 特性提供 |

## 数据与生命周期

- GF Manager 归 `GameFrameworkEntry` 所有；UGF Component 归 `GameEntry.prefab` 的 GameObject 所有；业务只缓存引用，不自行 new Manager 或重复挂 Component。
- GF 更新和绝大多数 UGF/Extension API 运行在 Unity 主线程。Download/Web/AssetBundle 的异步完成仍由组件或 GF 每帧轮询、再派发事件。
- `LoadAssetAsync<T>` 成功后不自动卸载；类型不匹配会立即 Unload。取消发生在加载完成后时也会 Unload，但底层 LoadAsset 不会因 Token 立即中止。
- Entity/UI 的 GF Manager 管理实例池；默认 Unity Helper 在池对象真正释放时同时 `UnloadAsset` 和 `Destroy(instance)`。Close/Hide 通常只是进入池，不代表资源立即卸载。
- Scene 通过 Resource Manager 装载，卸载由 Scene API 配对；不要对 Scene 返回值调用普通 `UnloadAsset`。
- AssetSet 对每个活跃目标 Spawn 一次多引用池对象；目标失效或换图后 Unspawn，池对象最终释放时按来源选择 `ResourceComponent.UnloadAsset` 或 `UnityEngine.Object.Destroy`。
- `AssetCollection.Pack` 在 Editor 按目录和通配符重建序列化字典；运行时 `GetAsset<T>` 只查表，不动态扫描，也不拥有这些资源的卸载权。
- 低内存回调会释放全部未使用对象池对象，并请求 `Resources.UnloadUnusedAssets`；这不能代替业务正常的 Close/Hide/Unload 配对。

## 开发扩展步骤

1. 先判断能力属于纯 C# Manager、Unity Component/Helper 还是项目 Extension；业务专用逻辑放 `Game/`，不要为项目需求修改 GF 核心。
2. 新 GF 模块需提供 `GameFramework.X.IXManager` 与同命名空间 `XManager : GameFrameworkModule`，明确 Priority、Update、Shutdown；只能通过接口取得。
3. 新 Unity 桥接组件继承 `GameFrameworkComponent`，`Awake` 必须调用 `base.Awake()`；在 `Start` 获取其它组件、创建 Helper 和注入依赖，并在项目 `GameEntry` 增加缓存入口。
4. 新资源 Helper 实现对应 GF 接口，序列化到 `GameEntry.prefab`；实例化、回收、资源卸载必须形成闭环。
5. 新异步封装要定义成功、失败、取消、框架关闭四种结果，清理事件数据/引用池对象，并确认取消是否真的能停止底层任务。
6. 新 Editor 工具放 Editor asmdef，通过现有 ConfigPath Attribute 和 `Assets/Res/Editor/Config` 读写，不让 Runtime 引用 `UnityEditor`。
7. 真实调用至少在 GameHot 或 ET Client 跑通一次，并验证退出 Procedure/Restart 后无重复订阅、悬挂资源和静态状态残留。
8. 只看到 `Unity/Assets/Scripts/Library/UGF/**` 或 Extension 目录新增文件时，先补 `GameEntry.prefab`/项目 `GameEntry`/Procedure 或其它消费者；否则只能记录为库能力，不能写成项目运行链路。

## 约束与常见错误

- `GetModule<T>` 不是通用 DI：非接口、非 `GameFramework.*` 接口或不符合命名约定都会抛异常；GF Manager 也不能直接从项目程序集扩展为另一命名空间实现。
- UGF `GetComponent<T>` 按精确类型匹配且找不到返回 null。组件依赖多在 `Start` 才就绪，虽然 Awake 已注册，也不要在其它 Awake 中调用需要 Manager/Helper 完成初始化的 API。
- Awaitable 必须在每次框架启动后注册。`UnsubscribeEvent()` 不实际移除 EventComponent 处理器，只置 `IsValid=false`；同一 EventComponent 生命周期内再次 Subscribe 会形成重复订阅，不应把它当作可反复开关。
- 当前 `LoadAssetAsync` 在 `updateEvent` 与 `dependencyAssetEvent` 同时非 null 时只选择 Update callbacks，`s_LoadAssetAllCallbacks` 分支不可达，依赖资源进度回调不会触发；需要两类进度时应先修正实现并补测试。
- Awaitable 关闭后，轮询中的任务会以 `GameFrameworkException("Awaitable is not valid.")` 结束；调用方仍需处理退出时异常/取消，不能遗留 fire-and-forget。
- `NetworkServiceComponent.State` 直接访问 Helper，初始化前会空引用；组件订阅 UGF 网络事件又延迟到首帧末，启动首帧内连接可能漏掉事件。业务应在正常 Procedure 时序初始化和连接，并在退出时销毁 Helper。
- `CodeRunnerComponent.StopRun` 使用 `DestroyImmediate`，且只有 Running 状态可调用；入口组件自己的销毁逻辑必须可同步完成。
- Editor Resource Mode 不支持正式版本清单、校验、更新和 AssetBundle 生命周期；资源修改至少还要在 Package 或 Updatable 模式验证。
- Extension Runtime 依赖 Odin 类型但 asmdef 未显式列 Odin 引用，依赖插件自动引用；缺少付费插件时程序集不能编译。
- WebGL 使用专用 Resource/LoadResourceAgent Helper；桌面 Editor 测试不能覆盖 URI 读取、AssetBundle 和场景加载差异。

## 验证方法

1. 在 Unity 6000.3.21f1 打开工程并编译，检查 `GameEntry.prefab` 上内置与 Extension 组件各只有一个，所有 Helper 类型可解析。
2. Play 后确认 `Game.GameEntry.Start` 缓存的 18 个内置组件和 4 个 Extension 组件非 null；观察 Procedure 正常启动。
3. 给 GF 模块记录 Update/Shutdown 顺序，确认按 Priority 降序更新、逆序关闭；执行 Restart 后确认模块和组件只注册一次。
4. 分别在 Editor Resource、Package、Updatable 模式加载/卸载资源、UI、Entity 和 Scene，确认实例池与资源引用最终释放。
5. 测试 Awaitable 成功、失败、预取消、完成前取消和框架退出；额外确认同时传 update/dependency 回调时当前只收到 update，以锁定已记录边界。
6. 进入/退出 ET 与 GameHot Procedure，验证 CodeRunner 的 Init 组件创建/销毁；进入/退出联网流程，验证 Helper 初始化、连接、断开和销毁。
7. 从 `Game Framework/Resource Tools` 运行资源规则刷新、Editor/Builder/Analyzer，并确认读写 `Unity/Assets/Res/Editor/Config`；WebGL Helper 需在 WebGL Player 单独验证。
8. 在仓库根目录运行 `powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1`。

本文只把源码与 prefab YAML 能支撑的内容写为静态结论；未通过 Unity Agent Bridge 打开或运行 Unity Editor，也未执行 Package/Updatable 资源模式、Shader build、ET/GameHot 真实启动或网络连接。

## 源码证据

- `Unity/Assets/Scripts/Library/UGF/GameFramework/Base/GameFrameworkEntry.cs`：接口命名反射、模块唯一创建、优先级更新与逆序关闭。
- `Unity/Assets/Scripts/Library/UGF/GameFramework/Base/GameFrameworkModule.cs`：Priority、Update、Shutdown 契约。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Base/GameFrameworkComponent.cs`：所有 UGF/Extension Component 的 Awake 注册入口。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Base/GameEntry.cs`：Unity 组件注册、Initialize 与 Restart/Quit。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Base/BaseComponent.cs`：Helper 初始化、帧驱动、低内存和销毁链。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Resource/ResourceComponent.cs`：Editor/正式资源管理器切换、依赖注入和 Unity UnloadUnusedAssets。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Resource/EditorResourceComponent.cs`：Editor 下 `IResourceManager` 替身及不支持边界。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/Entity/DefaultEntityHelper.cs`、`Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/UI/DefaultUIFormHelper.cs`：实例与资源的最终释放。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/Awaitable/Awaitable.cs`、`Awaitable.ResourceComponent.cs`：事件有效期、取消、资源所有权及双进度回调分支。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet/AssetSetComponent.cs`、`AssetSetComponent.AssetSetObject.cs`：多引用池与按来源释放。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/CodeRunner/CodeRunnerComponent.cs`、`NetworkService/NetworkServiceComponent.cs`、`Loader/GameEntryLoader.cs`：扩展入口和时序。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Editor/Build/GameFrameworkConfigs.cs`：工程实际编辑器配置路径。
- `Unity/Assets/Scripts/Library/UGF/GameFramework.prefab`、`Unity/Assets/Res/GameEntry.prefab`：UGF 内置组件、项目 GameEntry、Extension 组件、Procedure 列表和资源/UI/Entity 组的序列化接入证据。
- `Unity/Assets/Scripts/Game/Base/GameEntry.cs`、`GameEntry.Builtin.cs`、`GameEntry.Extension.cs`、`GameEntry.Game.cs`：项目真实组件缓存入口。
- `Unity/Assets/Scripts/Game/Procedure/ProcedureLaunch.cs`、`ProcedureET.cs`、`ProcedureGameHot.cs`、`Unity/Assets/Scripts/Game/Hot/Code/Procedure/ProcedureGame.cs`：Awaitable、CodeRunner 与 NetworkService 的真实调用点。
- `Unity/Assets/Scripts/Game/ET/Loader/CodeLoader.cs`、`Unity/Assets/Scripts/Game/Hot/Loader/Init.cs`、`Unity/Assets/Scripts/Game/Hot/Loader/Network/NetworkServiceHelper.cs`：ET/GameHot 入口组件之后的程序集/资源加载与网络 Helper 消费。

## 关联知识

- 上游：`ARCH-01`、`ARCH-02`、`PACKAGE-01`
- 下游：`UNITY-01`、`UNITY-03`、`UNITY-04`、`UNITY-06`、`UNITY-07`、`UNITY-08`、`UNITY-09`、`UNITY-10`、`UNITY-11`、`ET-02`、`ET-04`、`ET-05`、`BUILD-01`、`LIB-04`
