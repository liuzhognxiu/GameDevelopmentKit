# GameDevelopmentKit AI 知识库

本目录是面向 AI 辅助开发的仓库内知识层。它不替代源码和 `Book/`，而是把模块边界、入口链路、扩展方法、约束和验证方式整理成可检索、可追溯、可逐轮验收的内容。

## AI 使用入口

处理开发任务时按以下顺序读取，避免无目标地加载整个仓库：

1. 读取 [`catalog.json`](catalog.json)，按 `id`、`area` 和 `sources` 定位模块。
2. 读取命中的模块文档；涉及跨模块调用时，再读取其依赖模块。
3. 以 `sources` 指向的当前源码为最终事实。知识库与源码冲突时，以源码为准并修正文档。
4. 自动生成目录、Unity `.meta`、Luban/Proto 生成物只作为结果查阅，不作为手工修改入口。

## 完成状态

| 状态 | 含义 |
| --- | --- |
| `planned` | 已发现模块，但尚未形成可用文档 |
| `seed` | 已有可检索草稿，尚未按模板逐项核验 |
| `verified` | 已对照当前源码完成模板内容、权威源指纹和静态门禁；不表示运行验收通过 |

只有 `catalog.json` 中全部模块均为 `verified`，且静态完整性校验通过，才能声明“静态知识层完成”。只有五类运行验收也通过，才能声明“知识库全流程完成”。当前草稿数量不能代替模块覆盖率。

## 当前验收状态

- `catalog.json` 共 55 个模块，当前全部标记为 `verified`。
- `01` 至 `32` 文档均按统一模板组织；2026-08-05 已完成历史模块复审，并新增 `BUQI-01` 不器战斗与配置链路。普通校验和 `-RequireStaticComplete` 必须在本轮刷新源码指纹后通过：55 个 verified 模块、32 篇编号文档、0 warning。
- 普通校验与 `-RequireStaticComplete` 静态完整校验必须在干净源码基线中同时通过，不能用刷新指纹掩盖当前工作区的源码改动。
- [`runtime-acceptance.json`](runtime-acceptance.json) 如实记录五类运行验收；当前均为 `not_run`，因此 `-RequireComplete` 预期失败，不能声明全流程完成。

## 知识分区

| 分区 | 内容 | 主要文档 |
| --- | --- | --- |
| AI 开发协作 | 指令优先级、知识 Loop、CCGS Codex 适配与多智能体契约 | `28` |
| 架构与入口 | 仓库结构、启动链路、模式与程序集边界 | `01`、`02`、`03` |
| Unity 运行链 | 公共 Procedure、资源闸门、GameHot 与 ET 模式分派 | `29` |
| Unity 业务能力 | UI、Entity、资源、场景、音频、本地化、平台等 | `04` 至 `14` |
| 不器玩法链路 | Buqi Battle、Charge、Step 3 配置、局部门禁与 P-1 停止线 | `32` |
| ET 客户端集成 | ET Core、四程序集、UGF 桥接、动态事件 | `15`、`16` |
| 数据与协议 | Luban、Proto、生成物与运行时装载 | `17`、`18` |
| 编辑器与构建 | 编辑器工具、HybridCLR、资源与 Player 构建 | `19`、`20` |
| 服务端与工具链 | DotNet 服务端、运行链、Share 工具、文件服务、Aspire | `21`、`22`、`26`、`30` |
| 基础库与依赖 | UGF、辅助库、Unity Package 与 Assets 插件 | `23`、`24`、`27` |
| ET 网络示例 | Demo 登录进图、消息分派与 LockStep | `25` |
| 脚本规范 | Unity、GameHot、ET、Analyzer、生成与自动化脚本规则 | `31` |

模块与文档不是一一对应关系；一篇文档可以覆盖多个紧密相关的模块，完整覆盖关系以 [`catalog.json`](catalog.json) 为准。

## 常用阅读路径

- 第一次了解工程：`01` -> `02` -> `03` -> `20`。
- AI 协作与知识维护：`28` -> `catalog.json` 命中的模块文档 -> `31`。
- Unity 完整运行链：`01` -> `29` -> `03`（GameHot）或 `15`（ET）。
- GameHot 功能开发：`03` -> 对应的 `04` 至 `14` -> `17/18`。
- Buqi 战斗或配置开发：`32` -> `02` -> `17` -> `31`。
- ET 客户端开发：`01` -> `02` -> `15` -> `16` -> 对应 UI/Entity 文档。
- UI 开发：`03` -> `04`（Form、通用组件接入与 AI 工作流）-> `05`（组件库、绑定与所有权）-> `17`；ETUI 先补读 `15`、`16`。
- ET 服务端开发：`21` -> `30` -> `25` -> `26`；改协议或配置时补读 `17/18`。
- 脚本编写与审查：`31` -> 目标模块文档 -> 对应源码和 Analyzer 规则。
- 构建与发布：`02` -> `20` -> `22`。

## Loop 维护

每轮知识库建设遵循 [`LOOP.md`](LOOP.md)。新增或更新文档使用 [`_template.md`](_template.md)，完成后运行：

多个互不重叠模块可分别交给独立实现任务；每个实现任务完成后启动一个新的只读 review 任务。默认每模块只做一轮 review，不自动执行三重审查；只有具体事实争议或用户明确要求时才追加专项讨论或更多评审。

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1
```

静态知识层验收使用：

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1 -RequireStaticComplete
```

包含客户端启动、服务端启动、Luban、Proto 和目标 Player 构建的最终验收使用：

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1 -RequireComplete
```

源码或配置核验完成后，使用 `-RefreshSourceFingerprints` 刷新 [`source-fingerprints.json`](source-fingerprints.json)；当前 `sha256-path-git-clean-oid-v2` 算法按路径和 Git clean-filter 对象 ID 计算，避免 CRLF 工作树转换产生假漂移，同时仍能发现真实内容改动。该操作只更新知识库基线，不代替文档审查。运行验收只能在真实执行后更新 `runtime-acceptance.json`，不得为了让门禁变绿而填写 `passed`。

## 已有主题文档

`01` 至 `32` 为当前知识条目，覆盖 AI 协作、架构、Unity 业务与运行链、ET、数据生成、编辑器、构建、服务端、脚本规范、工具链、基础库、网络、运维、第三方依赖与 Buqi 玩法链路。后续源码、配置或依赖发生变化时，必须在同一变更中更新对应文档并重新运行门禁。
