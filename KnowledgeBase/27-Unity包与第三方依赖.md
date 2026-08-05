# 模块 27：Unity Package 与第三方依赖

> Catalog ID: `PACKAGE-01`  
> 状态：`verified`  
> 最后核验：`2026-08-05`  
> 适用模式：Unity Client / Editor / ET Shared / .NET Tools

## 模块定位

本模块记录 Unity 依赖的四个并行来源：UPM 直接/传递包、`Assets/Plugins` 预编译插件、`Assets/Scripts/Library` 仓库内源码程序集，以及共享源码对应的 .NET `csproj`/NuGet 依赖。它用于判断“包从哪里来、哪个文件锁版本、哪个程序集消费它、升级要验证什么”，不替代各上游许可证、漏洞公告或平台兼容矩阵。

本次静态核验对应 Unity `6000.3.21f1 (c02631ffc030)`。`verified` 只表示清单、锁文件、插件元数据、asmdef 和 csproj 关系已交叉核对；未在 Unity Editor 中重新解析、导入、编译或构建任何平台。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| UPM 声明 | `Unity/Packages/manifest.json` | 58 个直接条目：29 个 Unity 内置 modules，20 个 Git 包，9 个其他 Registry/Builtin 包 |
| UPM 锁定 | `Unity/Packages/packages-lock.json` | 记录直接/传递深度、解析版本、来源、依赖和 Git commit hash |
| 编辑器版本 | `Unity/ProjectSettings/ProjectVersion.txt` | 当前项目固定为 Unity 6000.3.21f1 |
| 预编译插件 | `Unity/Assets/Plugins` | CommandLine、SharpZipLib、MongoDB、NPOI、Share Analyzer/Generator、Sirenix/Odin 二进制及 native runtime |
| Unity 程序集 | `Unity/Assets/**/*.asmdef`、`*.asmref` | 程序集名引用、平台、define constraints 与预编译引用策略 |
| 仓库内源码库 | `Unity/Assets/Scripts/Library` | ET、UGF、UniTask.Extension、LubanLib、UXTool 等，不由 UPM lock 管理 |
| .NET 依赖 | `DotNet/**/*.csproj`、`Share/**/*.csproj` | 服务端/工具对共享源码的另一套 NuGet、项目引用图，以及 Buqi headless 验证项目 |
| .NET 热更接入 | `DotNet/Hotfix/DotNet.Hotfix.csproj` | Razor SDK、`Microsoft.AspNetCore.App`、链接 Unity ET Hotfix Client/Server/Share 源码，编译 Admin/Agent |
| Agent 规则 | `AGENTS.md` | Unity Agent Bridge 的已安装包 `AGENT.md`、固定槽位、ack、`list_commands` 和 `commandsVersion` 运行约束 |
| 本地生成缓存 | `Unity/Library/PackageCache` | UPM 解析后的本地安装态；可被 `DotNet.ThirdParty.csproj` 引用为构建前置，但不是仓库必存在 source，也不应作为 Catalog 权威来源 |

## 依赖关系

### UPM 直接依赖

除 29 个 `com.unity.modules.* = 1.0.0` 外，直接非模块包为：

| 分组 | 包与已声明/已解析版本 |
| --- | --- |
| Unity 官方 | 2D Sprite 1.0.0、Rider 3.0.40、Input System 1.20.0、Memory Profiler 1.1.12、Newtonsoft Json 3.2.2、URP 17.3.0、Scriptable Build Pipeline 3.1.1、Searcher 4.9.5、UGUI 2.0.0 |
| 热更/基础 | HybridCLR `e4def761...`、UniTask `2.5.10 / 7c0f199...`、MemoryPack Unity 扩展 `27409d45...`、ZString `e2b86f7a...`、ProtobufUnity `37802570...` |
| 动画/UI | LitMotion 与 Animation `ab6e92bf...`、SoftMask `2f4016c3...`、UIEffect `1f4427c3...`、UIParticle `5a305705...`、Unmask `33356da2...`、LoopScrollRect `cb189061...` |
| 网络/检查 | UnityWebSocket `369f2a56...`、InspectPlus `04f6baf3...`、RuntimeInspector `ec8421f7...` |
| 工作流 | CodeBind `de687711...`、ReactiveBinding `2d7095fb...`、StateController `87c612de...`、ToolbarExtension `ecb4645d...`、UnityAgentBridge `99933a48...` |

Git 短 hash 来自 `packages-lock.json` 的 40 位 `hash` 字段，文中缩写仅用于识别，精确 revision 始终以锁文件为准。只有 UniTask URL 明确带 `#2.5.10`；UnityWebSocket 使用 `#upm` 分支，其余 Git URL 大多未在 `manifest.json` 固定 tag/commit。现有 lock 可复现当前解析结果，但删除/重建锁文件或主动更新仍可能跟随上游变化。

锁文件还把 Burst 解析为 1.8.30、Collections 2.6.8、Mathematics 1.3.3、Mono Cecil 1.11.6 等传递依赖；这些版本可能高于上游包声明的最低版本，不能只看 `manifest.json` 推断实际编译版本。

### asmdef 与插件依赖

- `Game.asmdef` 直接引用 GameFramework、UGF Runtime/Extension、LubanLib、HybridCLR.Runtime、UniTask、CodeBind、Coffee UI 系列、ZString、MemoryPack、StateController、LitMotion、LoopScrollRect 和 URP 等；也引用 EnhancedScroller、UXTool、SocoTool、R3、ReplaceComponent 等仓库内/Assets 依赖。
- `Game.Editor.asmdef` 额外依赖 HybridCLR.Editor、LubanLib.Editor、UGF Editor、ToolbarExtension.Editor、CodeBind.Editor、SocoTool.Editor、StateController，以及 `AgentBridge.Editor`；该引用和 `Unity/Packages/manifest.json` 中的 `me.xw.unityagentbridge` 共同证明 Bridge 是编辑器工作流依赖。
- `ET.Core.asmdef` 在 `UNITY_ET` 下依赖 Unity.Mathematics、UniTask、MemoryPack、ET.ThirdParty、UniTask.Extension、UnityWebSocket，并允许 unsafe。ET Loader 仅受 `UNITY_ET` 控制；Model、ModelView、Hotfix、HotfixView 四个 Code asmdef 另使用 `!UNITY_HOTFIX || UNITY_COMPILE || UNITY_EDITOR` 复合约束，当前 Model 还直接引用 `ReactiveBinding`。
- `Game.ET.Code.Model.asmdef` 与 `Game.ET.Code.Hotfix.asmdef` 都设置 `noEngineReferences=true`，分别承载可被 ET 共享/热更链复用的模型与逻辑；ModelView/HotfixView 则显式引用 UGUI、TextMeshPro、CodeBind、Coffee UI、Game/UGF 等表现依赖。
- `UnityGameFramework.Extension.asmdef` 依赖 GameFramework、UGF Runtime、UniTask 和 UnityWebSocket。
- 多数业务 asmdef 的 `overrideReferences=false`；`Assets/Plugins` DLL 又通常 `isExplicitlyReferenced=0`，因此是否可见主要由 PluginImporter 的自动引用、平台和 define 设置决定，而不是 asmdef 中显式列出 DLL 名。

### Unity 与 .NET 的双依赖图

`DotNet/ThirdParty/DotNet.ThirdParty.csproj` 为服务端单独引用 CommandLineParser 2.8.0、MemoryPack 1.10.0、MongoDB.Driver 2.17.1、NLog 4.7.15、SharpZipLib 1.3.3、UniTask 2.5.10、ZString 2.6.0、LiteDB 5.0.21、MudBlazor 7.15.0 等；它还从 Unity PackageCache 编译 `com.unity.mathematics*`（排除 `Forwarders.cs`）和 `me.xw.reactivebinding@*` Runtime（排除 Plugins/Samples）。`DotNet/Hotfix/DotNet.Hotfix.csproj` 经 Loader/Model 间接使用这些依赖，额外引用 `Microsoft.AspNetCore.App`，并通过 `Compile Include` 链接 Unity 目录下的 ET Hotfix Client/Server/Share 源码。

`Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj` 是新增的 .NET 8 headless 验证入口，使用 `BUQI_HEADLESS`、C# 9、warnings-as-errors，并以 `Compile Include` 链接 `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/**/*.cs`。它刻意不引用 Unity/UGF/ET/资源/网络依赖，用于让 Buqi 纯 C# 战斗内核在非 Unity 环境下暴露编译和契约问题。现有 catalog 尚未为 Buqi 玩法创建独立知识模块；本页只记录依赖边界和覆盖缺口。

这套 NuGet 图与 Unity UPM/`Assets/Plugins` 图互不锁定。共享源码能同时编译不代表二进制版本相同：例如 Unity 中 CommandLine DLL 为 2.8.0，与 NuGet 对齐；Unity SharpZipLib DLL 的文件版本是 0.86.0.518，而 .NET 使用 1.3.3；Unity MongoDB Driver DLL 标记为本地 `0.0.0-local`，而 .NET 使用 2.17.1；MemoryPack Unity 来自扩展 Git 包，.NET 则是 1.10.0。

## 入口与调用链

1. Unity 打开项目时读取 `ProjectVersion.txt` 和 `manifest.json`。
2. Package Manager 按 `packages-lock.json` 解析直接/传递包，Git 包检出到锁定 hash，内容落入 `Library/PackageCache`。
3. Unity 导入 `Assets/Plugins` 并应用每个 `.dll.meta`/native plugin meta 的平台、CPU、define 和自动引用设置。
4. Script Compilation 根据 asmdef 名称引用和 define constraints 生成程序集；业务 asmdef 再消费 UPM、插件和仓库内 Library 程序集。
5. 打开 `Kit.sln` 或 `DotNet/DotNet.sln` 时，MSBuild 独立按 csproj 还原 NuGet；部分项目链接 Unity 目录源码，`DotNet.ThirdParty` 还依赖本机已解析出的 `Unity/Library/PackageCache/com.unity.mathematics*` 与 `me.xw.reactivebinding@*`。全新 checkout 或未让 Unity 解析包时，这些目录可以不存在，不能作为仓库内必然存在的源码目录引用。

升级任何共享依赖时必须沿 Unity 和 .NET 两条链分别验证，不能把 UPM lock 的成功解析当成服务端 NuGet 兼容证明。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `manifest.json/dependencies` | 声明直接 UPM 依赖 | Package Manager 输入；Git URL 多数未固定 ref |
| `packages-lock.json/dependencies` | 固化解析来源、深度、版本/hash | 应与 manifest 一起评审和提交 |
| `PluginImporter`（`*.dll.meta`） | 控制 DLL/native plugin 的平台、CPU、define 和自动引用 | 导入/切平台时重新评估 |
| `Assembly Definition`（asmdef） | 定义 Unity 编译边界和程序集名依赖 | 名称必须与已导入程序集一致；受 define/platform 约束 |
| `DotNet.ThirdParty.csproj` | 服务端共享源码的 NuGet 汇聚层 | MSBuild/NuGet 生命周期，独立于 UPM |
| `Game.asmdef` / `ET.Core.asmdef` | 主要业务与 ET 基础依赖入口 | Unity 脚本编译；后者要求 `UNITY_ET` |
| `UnityAgentBridge` | 通过固定槽位协议驱动 Unity Editor | 使用前必须读取包内 `AGENT.md` 并运行时发现命令 |

## 数据与生命周期

- `manifest.json` 是意图，`packages-lock.json` 是当前解析快照；升级后两者的 diff 都是评审对象。不要手改 `Library/PackageCache` 代替升级。
- `Unity/Library/PackageCache` 是 Unity Package Manager 在本机工作区生成/复用的安装态缓存，不受 Git 跟踪。`DotNet.ThirdParty.csproj` 对 `com.unity.mathematics*` 与 `me.xw.reactivebinding@*` 的源码引用是构建前置条件说明，不是知识库 `sources` 应收录的权威仓库文件；缺失时应先让 Unity/UPM 完成解析，再构建 .NET 方案。
- Buqi headless 项目链接 Unity/GameHot 纯 C# 源码以形成跨端编译防线；它不是独立包来源。后续若 Buqi 内核稳定，应新增玩法/仿真知识模块、验收命令和指纹边界，而不是继续只靠 `PACKAGE-01` 的宽泛 `Share` 覆盖。
- `Assets/Plugins` 及其 `.meta` 受版本控制。DLL 文件版本只能作为线索，不等于供应商发行版本或许可证版本；Sirenix 多数 DLL 报告 `1.0.0.0`，不能据此判断 Odin 实际版本。
- MongoDB 目录同时包含 managed DLL 与 Windows native `mongocrypt`、zstd、snappy；MongoDB.Driver importer 排除了 Android/iOS。NPOI 2.2.1 的 importer 为 Editor-only。目标平台兼容性由整套 importer 元数据共同决定。
- `Share.Analyzer.dll` 与 `Share.SourceGenerator.dll` 是仓库工具项目的预编译产物；它们的 ProductVersion 带源码 commit，但升级应由对应项目重新构建，而不是替换单个 DLL。
- HybridCLR、UniTask、MemoryPack、网络和序列化包会影响热更 DLL/API/代码生成格式。升级后旧热更包、AOT 元数据、序列化数据和协议兼容性都需要重新验证。

## 开发扩展步骤

新增或升级依赖时：

1. 先确定来源：优先 UPM Registry/固定 commit；只有无法 UPM 化的供应商二进制放 `Assets/Plugins`；仓库内源码库需有明确 asmdef。
2. 在独立分支修改 `manifest.json`，让 Unity Package Manager 生成 `packages-lock.json`；确认 Git `hash`、传递依赖和版本 diff，不手工伪造 lock。
3. 为消费方 asmdef 增加程序集名引用，或为预编译 DLL设置最小平台/define；检查 Editor 与 Player 代码是否被正确隔离。
4. 如果依赖被 ET/工具共享，独立更新相关 csproj/NuGet，并核对公共 API、条件编译和序列化格式；不要假设 UPM 与 NuGet 同版本。
5. 执行 Unity 全量脚本编译、目标平台构建、Launcher 关键流程、HybridCLR 生成/热更回归及对应 .NET solution 构建。
6. 记录许可证、来源、版本/hash、升级理由和回滚方式。供应商二进制还应保存采购/授权证明及可验证的校验值。

UnityAgentBridge 的扩展不直接猜命令：先按仓库 `AGENTS.md` 找 Bridge root，读取已安装包 `AGENT.md`，session 首次调用 `list_commands` 并按 `commandsVersion` 刷新 schema；请求通过固定 `request.json`、`processing.json`、`response.json` 槽位单飞交换，响应 ack 要先等 `processing.json` 消失再删除；不存在 Bridge root 或已安装包 `AGENT.md` 时停止，不自行创建目录或硬编码命令清单。

## 约束与常见错误

- **Odin 付费依赖**：`Unity/Assets/Plugins/Sirenix` 是 Odin Inspector/Validator 的供应商资产，仓库源码大量引用 `Sirenix.OdinInspector`、`Sirenix.Serialization` 和 Editor API。新环境必须具备合法席位/授权；仓库存在 DLL 不代表具备再分发或使用许可。
- **版本不可辨识**：Sirenix DLL 的程序集文件版本不足以识别产品版本；`Modules/Unity.Mathematics/manifest.txt` 的 1.0.1.0 是模块版本，不是 Odin 产品版本。
- **Git 漂移/供应链**：20 个直接 Git 包中只有 UniTask 固定版本 tag，多数 manifest URL 跟随默认分支，UnityWebSocket 跟随 `upm` 分支。必须保留并审查 lock hash；重要依赖宜改为不可变 commit，并做来源、许可证和漏洞审计。
- **二进制来源**：仓库没有为所有 `Assets/Plugins` DLL 提供统一的来源清单、许可证或 checksum。不能仅凭文件名/VersionInfo 证明完整性和再分发权。
- **平台差异**：MongoDB native 库、NPOI Editor-only 设置、Odin Runtime/Editor DLL 变体都可能在切换 Android、iOS、Linux、macOS、Windows 或 IL2CPP 时暴露问题。
- **双端版本偏差**：SharpZipLib、MongoDB、MemoryPack 等 Unity/.NET 版本不同。共享代码变更必须同时编译两端并做数据兼容测试。
- **asmdef 名称误判**：`manifest.json` 的 package id 不一定等于 asmdef 名称；删除 package 前先用 asmdef 和源码调用反查真实消费者。
- **缓存前提**：`DotNet.ThirdParty.csproj` 编译 Unity.Mathematics 和 ReactiveBinding 时依赖 `Unity/Library/PackageCache`。全新 checkout 若尚未让 Unity 解析包，.NET 构建可能缺源码。
- **PackageCache 不是 source**：`Unity/Library/PackageCache` 可能因本机、Unity 版本、缓存清理或未打开项目而缺失。文档和 Catalog 只能把它写成构建前置或本地安装态，不能要求仓库 checkout 必有该目录。
- **AgentBridge 规则是运行契约**：`me.xw.unityagentbridge` 的包声明、lock hash 和 `AgentBridge.Editor` asmdef 引用只证明依赖存在；实际 Unity 查询/修改仍必须按 `AGENTS.md` 读取已安装包 `AGENT.md` 并运行时发现命令。
- **生成/热更**：升级 HybridCLR、MemoryPack、Protobuf、UniTask 后不能只看 Console 无错误；必须重做生成步骤并验证旧数据/旧客户端边界。

## 验证方法

本次已完成以下静态验证：

- 以 PowerShell `ConvertFrom-Json` 成功解析 `manifest.json` 和 `packages-lock.json`，核对直接/传递深度、Git hash 和 Unity 官方解析版本。
- 枚举 `Assets/Plugins` 的 managed/native 文件及 FileVersionInfo，读取 CommandLine、MongoDB、NPOI、Sirenix 的 PluginImporter 元数据。
- 用 `rg` 反查相关 asmdef、源码调用和全部 csproj 的 PackageReference/ProjectReference，确认 Unity 与 .NET 是两套依赖图。
- 运行知识库结构/来源校验：`powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1`。

依赖升级后的可重复验收应包括：用 Unity 6000.3.21f1 打开项目并等待无错误导入；检查 Package Manager 与 lock revision；编译全部 asmdef；运行 EditMode/PlayMode 测试与 Launcher；执行 HybridCLR 准备和目标平台 Player 构建；再构建 `Kit.sln`、`DotNet/DotNet.sln` 并覆盖共享序列化/网络流程。

**未运行边界**：本次未启动 Unity Editor 或 Package Manager，未重新下载 Git/Registry 包，未验证 Odin 授权，未编译 Unity asmdef 或任何 solution，未运行测试、Launcher、HybridCLR、一键构建、IL2CPP/AOT、移动端/桌面端 Player，也未进行许可证、SBOM、恶意代码或 CVE 扫描。文件版本和静态引用关系不能替代这些验收。

## 源码证据

- `Unity/Packages/manifest.json`：全部直接 UPM 声明、Git URL 和显式版本。
- `Unity/Packages/packages-lock.json`：Git commit hash、直接/传递依赖和最终 Unity 包版本。
- `Unity/ProjectSettings/ProjectVersion.txt`：Unity 6000.3.21f1 精确版本。
- `Unity/Assets/Plugins/**/*.dll.meta`：预编译插件的平台、define 和自动引用边界。
- `Unity/Assets/Plugins/Sirenix/Readme.txt`：Odin Inspector 的供应商/产品身份；不提供可依赖的产品版本号。
- `Unity/Assets/Scripts/Game/Game.asmdef`、`Unity/Assets/Scripts/Game/Editor/Game.Editor.asmdef`：主业务和编辑器对 UPM/仓库程序集的直接引用。
- `Unity/Assets/Scripts/Library/ET/Core/Runtime/ET.Core.asmdef`、`Unity/Assets/Scripts/Game/ET/Code/Model/Game.ET.Code.Model.asmdef`、`Unity/Assets/Scripts/Game/ET/Code/Hotfix/Game.ET.Code.Hotfix.asmdef`、`Unity/Assets/Scripts/Game/ET/Code/ModelView/Game.ET.Code.ModelView.asmdef`、`Unity/Assets/Scripts/Game/ET/Code/HotfixView/Game.ET.Code.HotfixView.asmdef`：ET 基础依赖、表现依赖、`UNITY_ET` 与热更/编译复合约束。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/UnityGameFramework.Extension.asmdef`：UGF 扩展对 UniTask/WebSocket 的依赖。
- `DotNet/ThirdParty/DotNet.ThirdParty.csproj`：服务端 NuGet 版本及 Unity PackageCache 源码引用。
- `DotNet/Hotfix/DotNet.Hotfix.csproj`：Admin/Agent 所属 Razor 项目、FrameworkReference、项目依赖和 Unity ET Hotfix 源码链接。
- `Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj`、`Program.cs`：Buqi 纯 C# 战斗内核的 .NET 8 无头验证入口、approved hash/stress 契约和 Unity/UGF/ET 禁止依赖边界。
- `AGENTS.md`：Unity 6000.3.18f1/Odin 前置说明，以及 Unity Agent Bridge 的 fixed-slot single-flight、ack、`list_commands`、`commandsVersion` 和禁止自行创建 Bridge root 的运行规则。

## 关联知识

- 上游：`ARCH-01`（仓库架构）、`ARCH-02`（模式与分层）
- 下游：`BUILD-01`（HybridCLR 与构建）、`ET-01`、`ET-02`、`LIB-01` 至 `LIB-08`、`UNITY-16`（编辑器工具）
- 横向：`TOOLS-02`（Analyzer/SourceGenerator）、`SERVER-02`（.NET 项目依赖）
