# 模块 21：DotNet 服务端

> Catalog ID: `SERVER-01`、`SERVER-02`、`SERVER-03`  
> 状态：`verified`  
> 最后核验：`2026-08-05`  
> 适用模式：ET Server

## 模块定位

`DotNet/` 是与 Unity 客户端共用 ET Core、Model 与 Hotfix 源码的 .NET 8 服务端构建面。`App` 提供进程入口和常驻循环，`Loader` 负责宿主适配与程序集装载，`Model` 保存数据和稳定入口，`Hotfix` 保存可重载业务 System；`Share/Aspire` 则按 Luban 启动配置尝试编排多个 `App.dll` 进程。

它不是另一套服务端业务源码：`DotNet.Core.csproj`、`DotNet.Model.csproj`、`DotNet.Hotfix.csproj` 通过 `<Compile Include>` 链接 Unity 工程中的 ET 源码，因此修改共享文件会同时影响客户端与服务端编译。

## 源码边界

| 项目或目录 | 编译/运行职责 | 主要边界 |
| --- | --- | --- |
| `DotNet/App` | 输出 `Bin/App.dll`，执行 `Program.Main` 和主循环 | 直接引用 Loader、Model；`Entry.Init()` 仅用于防止 Model 被裁剪 |
| `DotNet/Loader` | 参数解析、NLog、时间、配置读取、Hotfix 装载 | 稳定宿主层；不应放需要热重载的 System |
| `DotNet/Core` | 链接 `Library/ET/Core/Runtime` | 客户端/服务端共享且不热更的 ET 基础设施 |
| `DotNet/Model` | 链接 ET `Model/Server`、`Client`、`Share` 和 ClientServer 生成代码 | 输出 `Model.dll`；定义实体、组件、消息和 `ET.Entry` |
| `DotNet/Hotfix` | 链接 ET `Hotfix/Client`、`Server`、`Share` | 输出 `Hotfix.dll`；包含业务 System，也承载 Admin Razor/ASP.NET Core 内容 |
| `DotNet/ThirdParty` | 链接 ET ThirdParty、LubanLib、UniTask 扩展、Unity.Mathematics 与 ReactiveBinding 运行时代码 | 隔离 NuGet 与跨端第三方依赖 |
| `Share/Aspire` | Aspire AppHost | 读取 `Config/Luban`，生成多个 `dotnet App.dll` 资源 |
| `Config/Luban` | 服务端运行配置产物 | Loader 以 `../Config/Luban/*.bytes` 读取；不要手改生成物 |
| Linux 发布脚本 | `Publish-linux-x64.ps1` | 从仓库根清理并重建 `Publish/linux-x64`，发布 App 并复制配置；属于破坏性发布入口 |

`DotNet/DotNet.sln` 只包含上述六个服务端项目；根目录 `Kit.sln` 还包含 Share 工具与 Aspire。所有服务端项目将输出目录指向仓库根部 `Bin/`。

## 依赖关系

```text
App -> Loader -> Core -> ThirdParty
  |       |                  ^
  +----> Model -> Core ------+

Hotfix -> Loader + Model
Aspire -> Model（IsAspireProjectResource=false）

Core  --link--> Unity/Assets/Scripts/Library/ET/Core/Runtime
Model --link--> Unity/Assets/Scripts/Game/ET/Code/Model/*
Hotfix--link--> Unity/Assets/Scripts/Game/ET/Code/Hotfix/*
```

`App` 必须引用 Model，虽然正常启动由 Loader 反射 `ET.Entry.Start`：否则发布裁剪可能认为 Model 未使用。Hotfix 反向依赖 Loader 和 Model，所以可卸载程序集不能成为稳定层类型定义的唯一来源。

## 入口与调用链

直接服务端启动链：

```text
dotnet Bin/App.dll [Options]
  -> Program.Main
  -> RunAsync().Forget()
  -> Entry.Init()                         // 空方法，保留 Model 引用
  -> Init.StartAsync()
     -> 注册 UnhandledException
     -> CommandLineParser 解析 Options
     -> World.AddSingleton: Options / Logger / TimeInfo / FiberManager
                            ConfigComponent / CodeLoaderComponent
     -> CodeLoader.StartAsync()
        -> 从 AppDomain 查找 Model.dll
        -> 从当前目录读取 Hotfix.dll + Hotfix.pdb
        -> 创建可回收 AssemblyLoadContext("Hotfix", true)
        -> 注册 CodeTypes(Core, Loader, Model, Hotfix)
        -> 反射同步 ET.Entry.Start
           -> StartAsync().Forget() 后返回
              -> 注册运行时 Singleton
              -> CodeTypes.CreateCode()
              -> ConfigComponent.LoadAllAsync()
              -> 创建 Main Fiber
                 -> FiberInit_Main
                 -> EntryEvent1 -> EntryEvent2 -> EntryEvent3
  -> 永久循环: Sleep(1) -> Init.Update() -> Init.LateUpdate()
```

`Init.StartAsync()` 在注册 `UnhandledException` 后先解析命令行，再创建 `NLogger` 并注册 `Logger`。因此 `Options` 解析失败、未知参数、`Options.Instance` 缺失或 Logger 注册前的异常，都会进入 catch/UnhandledException 中的 `Log.Error`，但 `Log.GetLog()` 在没有 Fiber 时直接访问 `Logger.Instance.Log`；`Singleton<T>.Instance` 默认是 null，没有控制台或 NLog fallback。文档中“启动异常可记录”只适用于 `World.Instance.AddSingleton<Logger, ILog>(...)` 成功之后。

`ET.Entry.Start()` 不等待其私有 `StartAsync()`：`CodeLoader.StartAsync()` 和 `Init.StartAsync()` 返回时，配置与 Main Fiber 不一定已就绪。宿主进入 Update 循环不能作为 readiness 信号；应以启动事件、健康检查或目标 Scene 可查询状态验收。

`EntryEvent1_InitShare` 给 Main Scene 添加 Timer、CoroutineLock、ObjectWait、Mailbox 和 ProcessInnerSender。`EntryEvent2_InitServer` 对 `Server/Admin/Agent` 根据 `DTStartProcessConfig` 创建 NetInner Fiber，并按 `DTStartSceneConfig.GetByProcess` 创建业务 Fiber；`--Console=1` 才添加控制台组件。

Aspire 路径：

```text
Tools/Shell/start aspire.bat
  -> dotnet run --launch-profile http (Share/Aspire)
  -> 向上寻找含 Config/ 的仓库根目录
  -> 加载 Config/Luban/*.bytes
  -> 过滤 DTStartProcessConfig.StartConfig
  -> 每个进程 AddExecutable("dotnet", Bin, "App.dll")
     -> WithArgs: --Process / --ReplicaIndex / --SceneName / --StartConfig / --SingleThread
     -> 未传 --AppType
  -> Aspire dashboard: http://localhost:15088
```

普通本地入口为 `Tools/Shell/start et server.bat`，从 `Bin` 执行 `dotnet App.dll --Process=1 --StartConfig=Localhost --Console=1`，依赖 `Options.AppType` 的默认 `Server`。Admin 和 Agent 分别有独立脚本，使用进程 `100002`、`100001`，并显式传入 `--AppType=Admin`、`--AppType=Agent`。

## 核心类型与 API

| 类型/API | 用途 |
| --- | --- |
| `ET.Program.Main()` | 服务端唯一进程入口；启动 fire-and-forget 异步流程 |
| `ET.Init.StartAsync()` | 构建宿主 Singleton 并启动 CodeLoader |
| `ET.Init.Update()/LateUpdate()` | 驱动 `TimeInfo` 和 `FiberManager` |
| `ET.Options` | 支持 `AppType`、`StartConfig`、`Process`、`Develop`、`LogLevel`、`Console`、`Customs`；`AppType` 默认 `Server` |
| `ET.CodeLoader.StartAsync()` | 定位 Model，加载 Hotfix，创建 CodeTypes，反射业务入口 |
| `ET.CodeLoader.ReloadAsync()` | 卸载并重新加载 Hotfix，只重建 CodeTypes 和 Code Singleton |
| `ET.ConfigReader` | 从工作目录相对路径 `../Config/Luban` 同步读取 bytes/json |
| `ET.Entry.Start()` | 初始化序列化、网络、对象池、配置和 Main Fiber |
| `FiberInit_Main` | 串行发布三阶段启动事件 |
| `DistributedApplicationBuilder.AddExecutable` | 把每个启动进程注册为 Aspire 外部可执行资源 |

常用命令行基线：

```powershell
Push-Location .\Bin
dotnet .\App.dll --AppType=Server --Process=1 --StartConfig=Localhost --Console=1
dotnet .\App.dll --AppType=Admin --Process=100002 --StartConfig=Localhost --Console=1
dotnet .\App.dll --AppType=Agent --Process=100001 --StartConfig=Localhost --Console=1
Pop-Location
```

## 数据与生命周期

1. `Options`、Logger、TimeInfo、FiberManager、ConfigComponent 和 CodeLoaderComponent 进入全局 `World`；其中 `Options` 解析成功后才注册 Logger，Logger 前失败没有可靠日志落点。
2. Model 程序集使用默认加载上下文，进程期间不卸载；Hotfix 位于 collectible `AssemblyLoadContext`。
3. `ET.Entry` 的私有 `StartAsync` 内部在 Main Fiber 前完成配置加载，业务 Fiber 的数量、SceneType、Zone 和名称由 Luban 启动表决定；但公开 `Entry.Start()` 使用 `Forget()`，调用方不会等待该顺序执行完成。
4. 主线程永久调用 FiberManager 的 Update/LateUpdate；业务 Scene 通常由 ThreadPool Scheduler 创建。
5. 热重载先 `Unload()` 旧上下文并 `GC.Collect()`，再加载新 DLL，替换 `CodeTypes` 并执行 `CreateCode()`；已有 Entity/Fiber 不会重建，`ET.Entry.Start` 也不会重跑。
6. 当前 `Program` 没有正常退出、取消令牌或 `World.Dispose` 路径，进程生命周期依赖外部终止。

## 开发扩展步骤

1. 先判断代码边界：跨端且稳定的基础设施放 Core；服务端稳定宿主适配放 Loader；数据/组件/消息放 Model；可重载 System 放 Hotfix。
2. 新增服务端 Scene 时，在 Luban 启动配置源中增加 Process/Scene 记录并重新导出到 `Config/Luban`，不要直接编辑 `.bytes`。
3. 为新 SceneType 创建对应 `[Invoke((long)SceneType.X)]` 的 FiberInit handler，在其中组装组件和网络监听。
4. 新增命令行参数时先扩展 `Options`，再同步所有 bat、Aspire `.WithArgs` 和运维脚本；非 Server 进程必须显式传 `--AppType`，不能依赖默认值。
5. 新增热重载代码时避免持有旧 Hotfix Type、委托或静态对象引用，否则 collectible AssemblyLoadContext 不能真正回收。
6. 从仓库根构建 `Kit.sln` 或 `DotNet/DotNet.sln`，确认 `Bin/App.dll`、`Model.dll`、`Hotfix.dll`、`Hotfix.pdb` 和配置目录关系完整。

## 约束与常见错误

- `Init.StartAsync` 的 catch 不能保证记录所有启动异常：命令行解析和 Logger 注册发生在同一个 try 内，Logger 注册前的参数错误或 Singleton 缺失会让 catch 中的 `Log.Error` 再次依赖空 `Logger.Instance`。Logger 注册成功后的 DLL、配置或 Hotfix 异常才预期进入 NLog。
- `AppDomain.CurrentDomain.UnhandledException` handler 也在 Logger 注册前安装，并同样调用 `Log.Error`；它不是 Logger 前失败的可靠兜底。
- 即使 `StartAsync` 吞掉异常后返回，`Program.RunAsync` 仍进入永久 Update 循环；若 TimeInfo/FiberManager 等 Singleton 未注册，循环中的 `init.Update()` 还会继续抛错。
- `CodeLoader` 硬编码从当前工作目录读取 `./Hotfix.dll` 和 `./Hotfix.pdb`；从仓库根直接运行 `dotnet Bin/App.dll` 会使相对路径错误，脚本因此先 `cd Bin`。
- `ConfigReader` 的异步 API 内部执行同步 `File.ReadAllBytes/ReadAllText`，不能假定其具备异步文件 IO。
- `CodeLoader.StartAsync` 未显式检查 Model 是否找到；程序集名必须保持 `Model`，否则反射入口失败。
- 热重载不是完整重启：它不会重新加载配置、创建 Main Fiber或重新发布 EntryEvent；结构性初始化修改需要重启进程。
- `Program.Main` 用 `RunAsync().Forget()` 启动；启动阶段异常由内部日志处理，不会以非零退出码自然暴露给进程编排器。
- **当前 Aspire 参数不匹配**：它传入 `--Process`、`--ReplicaIndex`、`--SceneName`、`--StartConfig`、`--SingleThread`，但 `Options` 只声明 `AppType/StartConfig/Process/Develop/LogLevel/Console/Customs`。CommandLineParser 会把未知参数送入 `WithNotParsed` 并抛出“命令行格式错误”，且该异常发生在 Logger 注册前，因此 Aspire 启动链在当前源码下不可视为可用。
- Aspire 子进程没有传 `--AppType`；如果先移除或接收未知参数但仍不补 `--AppType`，`App.dll` 会按 `Options.AppType` 默认值作为 `Server` 启动。Admin/Agent bat 则显式传入 `Admin`/`Agent`，这是与 Aspire 路径的实际差异。
- Aspire 设置 `InnerIP/InnerPort/OuterIP/OuterPort/ASPIRE_MANAGED` 环境变量，但服务端源码没有读取这些变量；实际地址仍来自 Luban 配置。它还把副本数固定为 `1`。
- `start aspire.bat` 将 `NUGET_PACKAGES` 硬编码为 `D:\AppData\.nuget\packages`，换机后可能不存在。
- Model 和 Hotfix 项目同时链接 Client/Server/Share 目录；必须依赖程序集和条件编译约束隔离平台 API，不能凭目录名假设服务端不会编译 Client 文件。

## 验证方法

静态核验：

```powershell
rg -n "AssemblyName|ProjectReference|Compile Include" DotNet -g "*.csproj"
rg -n "Option\(|WithArgs|GetEnvironmentVariable" `
  Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Options `
  Share/Aspire DotNet
rg -n "GetLog|Logger.Instance|class Logger|class Singleton|ParseArguments|AddSingleton<Logger|Log.Error" `
  Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Log `
  Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Singleton.cs `
  DotNet/Loader Share/Tool/Loader
rg -n "AppType|ReplicaIndex|SceneName|SingleThread" Share/Aspire Tools/Shell -g "*.cs" -g "*.bat"
rg -n "LoadHotfix|ReloadAsync|CreateCode|EntryEvent[123]" `
  DotNet Unity/Assets/Scripts/Game/ET/Code
```

具备 .NET 8 SDK 后，先用项目要求的 Unity 版本打开一次 `Unity` 工程并完成 Package 解析，确认 `Unity/Library/PackageCache/com.unity.mathematics*` 与 `me.xw.reactivebinding@*` 各有可用目录；`DotNet.ThirdParty.csproj` 直接编译其中源码，并排除 Mathematics `Forwarders.cs` 以及 ReactiveBinding 的 `Runtime/Plugins`、`Samples~`，干净检出不能只靠 NuGet 构建。随后运行：

```powershell
dotnet build .\DotNet\DotNet.sln
Push-Location .\Bin
dotnet .\App.dll --Process=1 --StartConfig=Localhost --Console=1
Pop-Location
```

启动验收还应确认 `Bin/Hotfix.dll`、`Bin/Hotfix.pdb`、`Config/Luban` 存在，并等待 Main Fiber/目标 Scene readiness；仅看到进程未退出不算成功。执行 `Publish-linux-x64.ps1` 前必须确认 `Publish/linux-x64` 可删除，并在隔离工作树检查发布产物与配置复制结果。

再执行 `Tools/Shell/start aspire.bat`，当前预期应首先验证未知参数问题；修正参数契约后，检查 dashboard 是否按配置展示全部进程、进程日志是否完成三阶段 EntryEvent、热重载后已有 Fiber 是否保持运行。

本轮完成源码、项目文件、脚本和配置调用链静态核验；未执行 `dotnet` 编译及进程级验证。

## 源码证据

- `DotNet/App/Program.cs`：`Main()` 以 `RunAsync().Forget()` 启动，`StartAsync()` 返回后无条件进入永久 Update/LateUpdate 循环，循环异常继续走 `Log.Error`。
- `DotNet/App/DotNet.App.csproj`
- `DotNet/Loader/Init.cs`：先注册 `UnhandledException`，再解析 `Options`，随后才创建 `NLogger` 并注册 `Logger`；catch 中仍调用 `Log.Error`。
- `DotNet/Loader/CodeLoader.cs`
- `DotNet/Loader/ConfigReader.cs`
- `DotNet/ThirdParty/DotNet.ThirdParty.csproj`：服务端对 Unity Mathematics 与 ReactiveBinding PackageCache 的构建期依赖，以及 Forwarders/Plugins/Samples 排除边界。
- `Publish-linux-x64.ps1`：Linux x64 发布目录清理、App 发布和配置复制入口。
- `DotNet/Core/DotNet.Core.csproj`
- `DotNet/Model/DotNet.Model.csproj`
- `DotNet/Hotfix/DotNet.Hotfix.csproj`
- `DotNet/ThirdParty/DotNet.ThirdParty.csproj`
- `Unity/Assets/Scripts/Game/ET/Code/Model/Share/Entry.cs`
- `Unity/Assets/Scripts/Game/ET/Code/Hotfix/Share/FiberInit_Main.cs`
- `Unity/Assets/Scripts/Game/ET/Code/Hotfix/Share/Demo/EntryEvent1_InitShare.cs`
- `Unity/Assets/Scripts/Game/ET/Code/Hotfix/Server/Demo/EntryEvent2_InitServer.cs`
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Log/Log.cs`：`GetLog()` 在无 Fiber 时直接返回 `Logger.Instance.Log`，没有 Logger 前 fallback。
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Log/Logger.cs`：`Logger` 只有 `Awake(ILog)` 后才持有可用 `ILog`。
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Singleton.cs`：`Singleton<T>.Instance` 默认静态字段为 null，只有 `Register()` 后才赋值。
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Options/Options.cs`：`AppType` 默认 `Server`；未声明 `ReplicaIndex`、`SceneName`、`SingleThread`。
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/World/Module/Code/CodeTypes.cs`
- `Share/Aspire/Program.cs`：`AddExecutable(..., "dotnet", binDir, "App.dll")` 只传 `Process/ReplicaIndex/SceneName/StartConfig/SingleThread`，未传 `AppType`；环境变量地址也不是服务端 Options。
- `Share/Aspire/Properties/launchSettings.json`
- `Tools/Shell/start et server.bat`：不传 `AppType`，依赖 `Options` 的 Server 默认值。
- `Tools/Shell/start aspire.bat`
- `Tools/Shell/start admin.bat`：显式传 `--AppType=Admin --Process=100002`。
- `Tools/Shell/start agent.bat`：显式传 `--AppType=Agent --Process=100001`。

## 关联知识

- [模块 15：ET 模块](15-ET模块.md)
- [模块 17：Luban 配置表](17-Luban配置表.md)
- [模块 18：Proto 协议生成](18-Proto协议生成.md)
- [模块 25：ET 网络与锁步](25-ET网络与锁步.md)
- [模块 26：管理后台与动态扩容](26-管理后台与动态扩容.md)
