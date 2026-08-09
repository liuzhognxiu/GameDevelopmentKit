# 不器 Demo 存档恢复与重开设计

## 目标

让不器 Demo 在遇到明确不可兼容的旧存档时安全地自动开始当前版本的新局，同时保留 I/O、待结算恢复和原因不确定的业务校验失败现场；错误态和终局的点击重开与键盘 R 共用一条命令路径。

## 根因与边界

`BuqiRunSaveCodec.TryFromJson` 当前只返回 `bool + string`，虽然已经迁移 `buqi-run-save-v2/v3`，却没有把未知 schema 与当前 payload 校验失败区分开。`BuqiRunDemoOrchestrator.TryInitialize` 因此只能在内容版本不一致时直接失败，UI 创建失败后 `BuqiRunShellForm.Restart` 又通过空 controller 提交命令，点击和键盘都无法恢复；当前 form 也没有 R 键输入处理。

本次不引入内容迁移器。内容版本不一致在 codec 已成功验证结构、且当前没有对应迁移器时定义为明确不可兼容；v2/v3 仍先走现有迁移器。未知 schema 版本定义为 codec 明确不支持。其余 codec 解析、当前版本业务校验、经济/事件/战斗 payload 解码、I/O 与 PendingSettlement 恢复失败均保留原存档。

## 方案

1. 在 save codec 增加结构化失败类别，并保留现有 API 兼容性。明确的未知 `SaveVersion` 返回 `UnsupportedVersion`，空白版本仍按不确定损坏处理；v2/v3 先执行现有迁移，迁移器明确判定无法安全迁移时返回 `UnsupportedVersion`，迁移成功后照常返回当前 save data；当前版本校验失败返回 `InvalidData`。
2. 初始化读取时只在 `UnsupportedVersion` 或已完整解码且 ContentVersion 不一致时走 `TryStartNewRun`。新局通过现有 `IBuqiRunStore.TryWrite` 原子覆盖，写失败不改变旧内容。错误文本统一在初始化/UI 边界转换为中文；成功恢复进入 `OperationChoice`，保存当前 ContentVersion。
3. form 保存最近一次可重开上下文。错误面板可在没有 controller 时通过同一个 `Restart` 方法重建 controller；终局仍由 controller 的 `Restart` 命令处理。按钮只在错误态或终局显示，文案为“重新开始”并可带弱化的 R 提示；`Update` 只在同样的允许状态把 R 送入 `Restart`，正常经营阶段忽略 R。
4. 回归测试覆盖旧内容自动新建、v2/v3 迁移保留进度、当前有效存档、I/O/业务/待结算失败保留、错误态重开、终局重开、正常阶段 R 不清档和恢复后的首阶段可操作性。

## 验证

先在 Unity EditMode 测试中观察新增测试 RED，再实现并运行 `Game.Hot.Buqi.Tests` 与 `Game.Hot.Editor` 的可用 dotnet build 入口；Unity Editor 点击验证留给主任务排队执行，不启动主目录 Unity/AgentBridge。
