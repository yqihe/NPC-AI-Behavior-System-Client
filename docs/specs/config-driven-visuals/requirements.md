# config-driven-visuals 需求分析

## 动机

当前代码在 3 个位置硬编码了游戏数据，直接违反 `docs/architecture/red-lines.md` 的"禁止硬编码游戏数据"红线：

1. **NPC 类型** — `NpcVisual.cs` 中 `typeName == "police"` 决定胶囊体大小和颜色；`GMPanel.cs` 中 `new[] { "civilian", "police" }` 硬编码下拉选项
2. **事件类型** — `GMPanel.cs` 中 `new[] { "explosion", "gunshot", "shout", "fire" }` 硬编码下拉选项
3. **FSM 状态** — `NpcVisual.cs` 中 `switch (snap.fsm_state)` 分支硬编码 `"Alarmed"`、`"Flee"`、`"Engage"` 驱动发光颜色

不做会怎样：
- 运营平台新增 NPC 类型（如 `medic`）或 FSM 状态（如 `Cower`、`Dead`，服务端 civilian FSM 已定义这两个状态），客户端需要改 C# 代码才能正确显示，违背"新增类型零代码变更"的原则
- 服务端已有 `Cower`、`Dead` 状态但客户端完全不识别，表现会降级到默认（黑色发光），用户无法区分

## 优先级

**中高** — 这是架构合规性修复，不影响当前功能运行，但：
- 红线违规应在功能开发早期修正，越晚修成本越高
- 后续 AutoTestRunner 和答辩演示可能会添加新 NPC 类型来展示扩展性，此时配置驱动是前提

## 预期效果

### 场景 1：新增 NPC 类型
运营在服务端 `configs/npc_types/` 下新增 `medic.json`，客户端在 `Assets/Configs/npc_visuals.json` 中添加 medic 的颜色/大小配置。无需改 C# 代码，Play 模式下 medic NPC 自动使用新配置的外观。

### 场景 2：新增 FSM 状态
服务端给 civilian 新增 `Dead` 状态（已在 FSM 配置中定义）。客户端在 `Assets/Configs/fsm_visuals.json` 中添加 `Dead` 的发光颜色。无需改 C# 代码，NPC 进入 Dead 状态时自动显示配置的颜色。

### 场景 3：未配置的类型/状态
服务端下发了客户端配置中没有的 NPC 类型或 FSM 状态。客户端使用合理的默认值（默认颜色/大小），不崩溃，在日志中输出一次警告。

### 场景 4：GM 面板下拉选项
GM 面板的 NPC 类型和事件类型下拉选项从配置文件读取，而不是硬编码。新增类型只需改配置文件。

## 依赖分析

- **依赖**：WebSocket 通信层（已完成）、NpcVisual（已完成）、GMPanel（已完成）
- **被依赖**：AutoTestRunner（未开始）— 自动测试可能需要动态类型列表

## 改动范围

预估涉及文件：
- 新增：`Assets/Configs/` 下 2-3 个 JSON 配置文件 + 1 个配置加载类
- 修改：`NpcVisual.cs`（移除硬编码状态映射）、`GMPanel.cs`（下拉选项从配置读取）
- 总计约 4-6 个文件

## 扩展轴检查

`extension-axes.md` 尚未在客户端创建。但本需求直接服务于 red-lines.md 明确的扩展性要求：
- **NPC 类型扩展** — 新增类型不改 C# 代码 ✅
- **事件类型扩展** — 新增类型不改 C# 代码 ✅
- **FSM 状态扩展** — 新增状态不改 C# 代码 ✅

## 验收标准

| 编号 | 标准 | 验证方式 |
|------|------|----------|
| R1 | `NpcVisual.cs` 中不包含任何硬编码的 NPC 类型名称字符串（`"civilian"`、`"police"` 等） | grep 搜索确认零匹配 |
| R2 | `NpcVisual.cs` 中不包含任何硬编码的 FSM 状态名称字符串（`"Alarmed"`、`"Flee"`、`"Engage"` 等） | grep 搜索确认零匹配 |
| R3 | `GMPanel.cs` 中不包含任何硬编码的 NPC 类型或事件类型名称字符串 | grep 搜索确认零匹配 |
| R4 | 配置文件存在且包含当前所有已知类型/状态的视觉映射 | 人工检查配置文件完整性 |
| R5 | 服务端下发未配置的 NPC 类型时，客户端不崩溃，使用默认外观，日志输出一次警告 | Play 模式 spawn 未配置类型验证 |
| R6 | 服务端下发未配置的 FSM 状态时，客户端不崩溃，使用默认发光颜色 | Play 模式验证 |
| R7 | 编译通过，Play 模式下现有 civilian/police NPC 外观与改动前一致 | 目视对比 |

## 不做什么

- **不做**运行时从服务端拉取类型列表（服务端没有此 API，也不需要）
- **不做** NPC 3D 模型/预制体切换（当前阶段只用胶囊体+颜色区分）
- **不做**动画状态机配置（当前没有动画系统）
- **不做**配置热更新（修改配置后需要重启 Play 模式）
