# 知识库四轨扩充设计

日期：2026-08-04  
状态：待用户复核

## 1. 目标

在不重复现有 27 篇知识页的前提下，为 GameDevelopmentKit 增加四条面向 AI 开发的高价值阅读路径：

1. AI 开发基础设施与 CCGS 协作。
2. Unity 客户端从启动到业务可用的运行链路。
3. ET 服务端从进程启动到网络消息与 Actor 调度的运行链路。
4. 覆盖运行时、ET、编辑器、测试和自动化的脚本编写规范。

四条路径分别解决“如何组织 AI 开发”“客户端实际如何跑起来”“服务端实际如何接收和处理请求”“新增脚本必须遵守什么规则”。每篇页面都进入 `KnowledgeBase/catalog.json`，遵守 `KnowledgeBase/_template.md`，并提供仓库相对路径证据。

## 2. 文档边界

### 2.1 `28-AI开发工作流与CCGS协作.md`

新增两个 Catalog 模块：

- `AI-01`：仓库级 AI 指令、知识库 Loop 与工具选择。
- `AI-02`：CCGS Codex 插件、工作流路由与专家角色协作。

权威源包括 `AGENTS.md`、`KnowledgeBase/LOOP.md`、`.agents/plugins/marketplace.json`、`plugins/ccgs-codex`、`.codedb-mcp/codedb-mcp.toml`。页面说明指令优先级、`$ccgs` 路由、73 个工作流与 49 个角色的按需加载方式、多智能体任务边界、Unity Agent Bridge 前置条件、证据与提交要求。

本页不复制 73 个工作流全文，也不把 Claude 的 `Task`、`AskUserQuestion`、模型字段或 hooks 当作 Codex 可执行语义；这些差异由兼容层解释。

### 2.2 `29-Unity运行链路.md`

新增两个 Catalog 模块：

- `UNITY-17`：Unity 启动、Procedure 与 GameHot 装载链。
- `UNITY-18`：资源、配置、UI、Entity 和场景进入业务可用状态的运行编排。

本页以 codedb 图中的真实入口、跨社区依赖和调用关系为主线，串联 `ProcedureLaunch`、模式选择、Hot Loader、`HotEntry`、配置表加载、资源检查、UI/Entity/Scene 使用边界。它引用并链接 `01`、`03`、`04`、`06`、`07`、`08`、`17`、`20`，但只描述跨模块时序，不重复各模块 API 清单。

静态证据必须至少包含：入口文件、关键调用链、模式或条件编译分支、一个真实业务调用点。Unity Editor 状态、场景实际打开和运行画面只在通过 Agent Bridge 实际验证后才能标记为运行通过。

### 2.3 `30-ET服务端运行链路.md`

新增两个 Catalog 模块：

- `SERVER-04`：进程启动、配置装载、CodeLoader 与 Scene/Fiber 建立。
- `SERVER-05`：网络接入、消息分派、Actor/Location 路由与 Hotfix 业务处理。

本页从 `DotNet/App/Program.cs`、`DotNet/Loader/Init.cs` 和 `CodeLoader` 出发，用 codedb 的 `CALLS`、`DISPATCHES_TO`、`REFERENCES` 与跨社区依赖验证链路，再落到 Demo、网络模块、Actor 消息和管理/扩容边界。它链接 `15`、`18`、`21`、`25`、`26`，重点解释跨程序集、跨进程和生成协议之间的衔接。

本页不声称某个接口实现是活动实现，除非构造、注册或配置证据完成选择；不把可能的 dispatch 目标写成唯一运行目标。

### 2.4 `31-脚本编写规范.md`

新增三个 Catalog 模块：

- `CODE-01`：Unity/GameHot 运行时 C#、Procedure、UI、Entity、异步、事件和生命周期规则。
- `CODE-02`：ET Entity/Component/System、Model/Hotfix 分层、客户端/服务端共享代码和消息处理规则。
- `CODE-03`：Unity Editor、测试、Analyzer/SourceGenerator、PowerShell、BAT、构建、Luban、Proto 与发布脚本规则。

规则按约束强度分层：

1. 编译器、Analyzer、程序集引用、条件编译和生成器能强制执行的规则。
2. 当前仓库中稳定出现且与框架生命周期一致的源码模式。
3. `AGENTS.md`、现有 Book/KnowledgeBase 与 CCGS standards 中的书面约定。
4. 仅用于可读性或团队一致性的建议。

高层书面规则与源码冲突时以当前编译边界和实际实现为准，并修正文档。页面必须覆盖命名与目录、热更边界、异步与取消、事件订阅释放、UI/Entity 生命周期、ET 扩展方法与特性、Editor/Runtime 隔离、生成目录禁改、日志与异常、测试组织、PowerShell/BAT 错误传播、路径与编码、外部命令退出码、敏感信息和提交前验证。

权威源至少包括 `AGENTS.md`、`.claude/rules`、`plugins/ccgs-codex/skills/ccgs/references/standards`、`Share/Analyzer`、`Share/SourceGenerator`、主要 asmdef/csproj、`Tools/Shell`、构建脚本以及各模式下的代表性实现。CCGS 通用规则只能作为补充，不得覆盖仓库已有模式。

## 3. Catalog 与导航

`KnowledgeBase/catalog.json` 从 45 个模块扩展到 54 个模块。九个新模块均使用独立 `sources`、关键词、area 和对应文档；只有完成源码逐项核验后才标记为 `verified`。

`KnowledgeBase/README.md` 同步：

- 模块数由 45 更新为 54。
- 编号文档由 `01` 至 `27` 更新为 `01` 至 `31`。
- 增加“AI 协作”“Unity 运行链”“ET 服务端运行链”“脚本规范”阅读路径。
- 保留静态完成与五项运行验收的严格区分。

## 4. 证据策略

1. 先用 codedb 查询 `EntryFile`、`BoundaryFile`、跨社区 `DEPENDS_ON`，确定运行链锚点。
2. 对准确文件使用 `codedb_outline`；只在图无法表达局部语义时读取一个精确 symbol body 或源码范围。
3. Markdown、JSON、PowerShell 和插件 manifest 使用结构化解析或精确文件读取，因为它们不一定进入 C# 调用图。
4. 每条关键结论至少绑定一个入口证据和一个调用、配置或注册证据。
5. 文档中明确区分静态核验、建议验证和本轮实际运行结果。
6. 脚本规则先读取 Analyzer/SourceGenerator 的真实诊断与生成逻辑，再用代表性源码核对不能自动执行的生命周期和分层约束。

## 5. 工作区隔离

当前工作区存在其他任务产生的 Unity 配置、Luban 配置、《星期八》业务代码、WorkBuddy 缓存和 `obj.wb` 产物。本轮：

- 不修改、不删除、不暂存这些文件。
- 不把未提交业务代码作为框架知识库的稳定权威源。
- 仅修改四个新知识页、`catalog.json`、`README.md` 和必要的知识库指纹文件。
- 如果旧模块指纹因外部改动失配，先报告阻塞，不用知识库提交掩盖外部源码状态。

## 6. 验证设计

完成文档后执行：

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1 -RequireStaticComplete
```

新增模块的源指纹通过 `-RefreshSourceFingerprints` 生成；刷新前必须审查是否会吸收当前外部未提交改动。若无法隔离，新增文档和 catalog 可以完成，但静态完成门禁必须如实报告为被工作区状态阻塞。

不运行 Unity、服务端、Luban、Proto 或 Player 构建时，不更新 `runtime-acceptance.json` 为 `passed`。

## 7. 完成标准

- 四篇文档均具备模板要求的全部章节和九个准确 Catalog ID。
- 关键运行链来自图或精确源码证据，不依赖目录名推断。
- 页面间无重复大段内容，关联知识链接形成可导航路径。
- README 模块数、编号范围和 catalog 一致。
- 知识库验证结果有新鲜命令输出；若受外部改动阻塞，列出具体模块和文件。
- Git 暂存范围只包含本轮知识库文件。
