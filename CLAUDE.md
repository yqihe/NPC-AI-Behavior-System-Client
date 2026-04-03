## CLAUDE.md

本文件为 Claude Code 提供项目上下文和开发指导。

## 项目概述

**项目名称**：NPC AI 行为系统 — Unity 客户端
**项目性质**：毕业设计
**当前状态**：初始开发

本项目是 NPC AI 行为系统的 Unity 客户端，通过 WebSocket 与 Go 服务端通信，接收 NPC 状态并驱动游戏表现。

**服务端仓库**：`../NPC-AI-Behavior-System-Server/`（Go，已完成）

## 技术栈

- **引擎**：Unity（C#）
- **通信**：WebSocket（JSON 文本帧）
- **UI 框架**：Unity UI / UGUI

## 核心功能

### 1. WebSocket 通信层
- 连接服务端 `ws://localhost:9820/ws`
- 发送请求（spawn_npc、remove_npc、publish_event、query_npc）
- 接收响应和 world_snapshot 广播
- 断线重连

### 2. NPC 可视化
- 根据 world_snapshot 广播驱动 NPC GameObject 的位置、动画、状态表现
- 不同 NPC 类型（civilian/police）不同外观
- 不同 FSM 状态（Idle/Alarmed/Flee/Engage）不同动画/特效

### 3. GM 面板（调试工具）
- 点击地图位置发布事件（explosion/gunshot/shout/fire）
- 生成/移除 NPC
- 查看 NPC 详细状态（FSM 状态、威胁值、当前行为）
- 实时观察 NPC 行为变化

### 4. AutoTestRunner（自动化测试）
- 自动执行预设场景序列
- 验证 NPC 行为响应是否符合预期
- 用于答辩演示

## 协议规范

完整的 WebSocket 协议定义见服务端文档：
`../NPC-AI-Behavior-System-Server/docs/protocol.md`

### 快速参考

**连接地址**：`ws://localhost:9820/ws`

**消息信封**：
```json
{"type": "消息类型", "id": "请求ID", "data": {...}}
```

**客户端 → 服务端**：
| type | 说明 |
|------|------|
| `spawn_npc` | 创建 NPC（指定 ID、类型、位置） |
| `remove_npc` | 移除 NPC |
| `publish_event` | 发布事件（爆炸/枪声/火灾等） |
| `query_npc` | 查询 NPC 详细状态 |

**服务端 → 客户端**：
| type | 说明 |
|------|------|
| `response` | 请求成功响应 |
| `error` | 请求失败响应（含 code + message） |
| `world_snapshot` | 每 100ms 广播所有 NPC 状态（位置、FSM 状态、行为、威胁值） |

**可用 NPC 类型**：`civilian`（逃跑）、`police`（迎敌）

**可用事件类型**：`explosion`、`gunshot`、`shout`、`fire`

## 开发环境

### 服务端启动
```bash
cd ../NPC-AI-Behavior-System-Server
docker compose up --build
```

### 关键约束

- 客户端不做 AI 逻辑——所有 NPC 行为由服务端决策，客户端只负责表现
- 客户端不缓存 NPC 状态——以 world_snapshot 为权威数据源
- 协议变更必须服务端先改，客户端跟随
