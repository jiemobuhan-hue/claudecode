# PLC 开发技能包 — 设计规格

Status: approved
Date: 2026-06-13

## 一、目标

建立公司内部上位机-PLC 对接开发的标准化工具包，包含：

1. **ELECTRICAL.md** — 泛用架构模式知识库，新项目的第一份电气文档，Claude Code 自动加载
2. **/plc-dev 技能** — Claude Code 可调用命令，覆盖从需求分析到代码生成的完整流水线

核心原则：**泛用模板 + 参考实现分离**。泛用模式适用于任何 PLC 品牌和项目；当前项目的具体信号/配置作为附录参考。

## 二、ELECTRICAL.md 结构

### §1 架构模式

PLCHandler 三层抽象：
- **PlcMonitor** — 顶层管理器。加载配置、创建 PlcChannel 列表、暴露按名称读写 API
- **PlcChannel** — 单 PLC 连接。连接-轮询-重连循环，缓存最新值，Rx Subject 推送
- **IPlcConnection** — 品牌无关的连接接口。Connect/Disconnect/Read/Write/IsConnected

配套组件：SignalReader（类型分发）、RetryPolicy（指数退避）、PlcConnectionFactory（品牌路由）

设计原则：
- 配置驱动：PLC 和信号通过 JSON/CSV 定义，不硬编码
- 按名称访问：上层代码不接触地址，通过信号名读写
- 信号值缓存：最新值常驻内存，避免频繁 I/O
- 推送模式：信号更新通过 Rx 通知订阅者

### §2 信号定义规范

**CSV 字段**（7 列）:
| 列 | 含义 | 示例 |
|----|------|------|
| Address | PLC 地址 | D1002 |
| DataType | 数据类型 | UShort, String(15), ShortArray(30) |
| Name | 信号名（中文，功能_通道_动作） | PLC通道1来料触发 |
| Description | 用途说明 | 来料工位触发信号，PLC→PC，1=有料 |
| PlcId | 所属 PLC | omron_1 |
| Direction | 读写方向 | R(PLC→PC), W(PC→PLC), RW |
| Group | 功能分组 | 来料/分流/心跳/视觉 |

**命名规范**: `PLC通道{N}{功能}{动作}` — 如 `PLC通道1来料触发`、`PLC通道1来料电芯码`

**数据类型映射**: Bool/UShort/Int/Float/Double/String(n)/ShortArray(n)/ByteArray(n)

### §3 时序流程图规范

使用 Mermaid sequenceDiagram 表达 PLC↔PC 握手流程。

标准模式：
- **请求-响应**: PLC 置位 → PC 检测 → PC 处理 → PC 回写 → PLC 复位
- **数据推送**: PLC 置位+写数据 → PC 读取 → PC 回写结果
- **心跳**: PC 周期性读取 PLC 心跳值 → 超时判定丢失

流程图规范：
- 每个工位画一条 sequenceDiagram，包含正常路径和异常分支（alt/else）
- 异常分支标注：超时时间、重试次数、降级策略
- PC 内部调用外部服务（如 MOM）用 Note 标注
- 涉及的 PLC 信号在图中用别名标注（如 `D1002 (来料触发)`）

模板示例见附录 A 当前项目实例。

### §4 工位状态机

**IStationHandler 接口契约**:
```
CheckHeartbeatAsync(token)  → 检查通道心跳，失败抛异常
WaitForSignalAsync(token)   → 等待触发信号，超时可配置
ExecuteActionAsync(token)   → 执行业务逻辑，写结果回 PLC
```

**Station 循环**:
```
while (!token.IsCancelled) {
    if (全局心跳丢失) { await Task.Delay(1000); continue; }
    await handler.CheckHeartbeatAsync(token);
    await handler.WaitForSignalAsync(token);
    await handler.ExecuteActionAsync(token);
}
```

**多通道扩展**: 每个通道实例化一组 Station（来料+分流），各自 Task.Run，共享全局心跳状态。

**全局心跳**: `GlobalHeartbeatState { Healthy, Lost, Recovering }` — 丢失时暂停所有工站，恢复确认 2 秒后重启。

### §5 品牌适配

| 品牌 | 协议 | HSL 类 | 地址格式 | 备注 |
|------|------|--------|----------|------|
| Omron | FINS TCP | OmronFinsNet | Dxxxx/Wxxxx | 需 SA1/DA1 节点号 |
| Siemens | S7 | SiemensS7Net | DBx.DBWx | 需 Rack/Slot |
| Modbus | Modbus TCP | ModbusTcpNet | 4xxxx | 功能码 03/06 |
| Mitsubishi | Melsec | MelsecMcNet | Dxxxx | 二进制/ASCII 模式 |

每种品牌的连接参数模板和已知限制。

### §6 模拟与测试

- **SimulationMode**: App.config 开关，启用时 SimulationDataGenerator 周期性生成假数据
- **PLC 信号模拟器**: 测试配置使用 localhost PLC (127.0.0.1)，可配合软 PLC 或模拟器
- **无硬件测试**: 断开 PLC 连接时代码使用缓存的最新值或默认值，不崩溃
- **单元测试策略**: PlcChannel 可注入 Mock IPlcConnection，SignalReader 可单独测试类型分发

### §7 故障排查

分层诊断流程：
1. **物理层**: ping PLC IP、检查网线/交换机、防火墙端口
2. **连接层**: PlcMonitor 连接状态、PlcChannel.ConnectionState、重连日志
3. **心跳层**: 心跳值是否变化、心跳间隔是否稳定
4. **信号层**: 单个信号读写测试（TryGetLatestByName / ReadOnceByNameAsync）
5. **逻辑层**: 工位状态机是否正常运行、StationState 流转

每层给出检查命令、判断标准、常见修复方案。

### 附录 A: 本项目参考实现

- 2 台 Omron PLC 完整信号表（50 信号）
- 来料流程 + 分流流程 Mermaid 时序图
- 8 工位 Station 配置
- PLCHandler 项目结构

## 三、/plc-dev 技能

技能文件位置: `.claude/skills/plc-dev/SKILL.md`

### 命令总览

| 命令 | 阶段 | 输入 | 输出 |
|------|------|------|------|
| `flowchart <需求>` | ①需求分析 | 自然语言描述 | Mermaid 时序图 + 状态迁移图 |
| `define-signals` | ②信号定义 | 确认的流程图 | 信号清单 + CSV 行 |
| `gen-code` | ③代码生成 | 流程图 + 信号表 | IStationHandler 实现 |
| `validate` | ④校验 | 现有代码 | 一致性报告 |
| `diag` | ⑤诊断 | 异常现象 | 分层检查结果 + 修复建议 |
| `scaffold` | 新项目 | 项目名+品牌 | 完整项目骨架 |

### 3.1 flowchart — 需求→流程图

**输入**: 自然语言描述工位需求，包含：
- 触发条件（什么信号、什么时机）
- 动作序列（读什么、算什么、写什么）
- 外部依赖（数据库、MOM、视觉系统）
- 异常处理（超时、重试、降级）

**输出**:
1. 一张 Mermaid sequenceDiagram（PLC ↔ PC ↔ 外部系统 的交互时序）
2. 一张 Mermaid stateDiagram（工位状态迁移：Idle→Waiting→Processing→Done→Error）
3. 涉及信号清单（从流程图中提取，标注方向）

**行为规范**:
- 向用户提问澄清模糊点（超时时间？重试次数？降级策略？）
- 生成流程图后请用户确认，不直接进入下一步
- 流程图中的 PLC 信号标注地址别名（如 `D1002 (来料触发)`）
- 异常分支必须用 `alt/else` 语法明确画出

**示例**:
```
用户: /plc-dev flowchart "通道1来料：PLC触发→PC扫码→MOM查单→回写结果。异常：扫码超时3秒重试，MOM不可用走离线队列"

Skill 行为:
1. 澄清: 扫码超时重试几次？离线队列写入后何时回写PLC？
2. 生成 Mermaid 时序图（含 alt 超时/离线分支）
3. 生成状态迁移图
4. 列出涉及信号: 来料触发(D1002)、来料电芯码(D1006)、来料结果(D1070)
5. 等用户确认
```

### 3.2 define-signals — 流程图→信号表

**输入**: 已确认的流程图 + 目标 PLC ID

**输出**:
- 信号清单表格（名称、建议地址、类型、方向）
- 可直接追加到 signals_config.csv 的文本
- 命名规范检查报告

**行为规范**:
- 从流程图中自动识别需要哪些信号（触发信号、数据信号、结果信号、心跳）
- 根据已有信号表自动建议下一个可用地址（避免冲突）
- 信号名自动按 `PLC通道{N}{功能}{动作}` 格式生成
- 允许用户手动修改后再写入 CSV

### 3.3 gen-code — 信号表→Handler 代码

**输入**: 流程图 + 信号表 + 目标工位类型（来料/分流/自定义）

**输出**:
- 一个实现 IStationHandler 的类
- CheckHeartbeatAsync / WaitForSignalAsync / ExecuteActionAsync 三个方法
- 超时、重试、异常处理代码
- 需要的 using 和依赖注入

**行为规范**:
- 心跳检查复用 PlcMonitor.TryGetLatestByName 读取心跳信号
- 信号等待使用 ReadOnceByNameAsync + 轮询间隔
- 动作执行中使用已有的 SetIO/SetInt/SetString 封装方法
- 不破坏现有的 IStationHandler 接口签名

**生成的代码模板** (以本项目为例):
```csharp
private class MyStationHandler : IStationHandler
{
    private readonly AutoRun _owner;
    private readonly int _channelNo;

    public async Task<bool> CheckHeartbeatAsync(CancellationToken token)
    {
        var val = _owner.GetInt_Plc($"PLC通道{_channelNo}来料触发");
        // 如果能读到值说明连接正常
        return true;
    }

    public async Task<bool> WaitForSignalAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var trigger = _owner.GetInt_Plc($"PLC通道{_channelNo}来料触发");
            if (trigger == 1) return true;
            await Task.Delay(100, token);
        }
        return false;
    }

    public async Task ExecuteActionAsync(CancellationToken token)
    {
        // ① 读电芯码
        var code = _owner.GetString_Plc($"PLC通道{_channelNo}来料电芯码");
        // ② 调用外部服务
        // ③ 写结果
        _owner.SetInt_Plc($"PLC通道{_channelNo}来料结果", 1);
    }
}
```

### 3.4 validate — 信号一致性校验

**检查项**:
1. CSV 中每个信号是否有代码引用（Grep 搜索信号名）
2. 代码中直接写的信号名字符串是否都在 CSV 中存在
3. 同一地址是否被多个信号占用
4. 读写方向是否与代码行为一致（读信号不应被 SetIO 写入）
5. 每个 PLC 是否有心跳信号定义
6. 触发-结果信号是否成对出现

**输出**: 校验报告（通过/警告/错误），错误项给出文件和行号。

### 3.5 diag — 交互式故障排查

**流程**:
1. 物理连接检查（ping PLC → 检查端口 → 检查 PlcChannel.ConnectionState）
2. 心跳检查（读取心跳信号值 → 确认值在变化 → 确认间隔正常）
3. 信号检查（选择可疑信号 → 单次读取 → 对比预期值）
4. 工位检查（查看 StationState → 查看当前等待的信号 → 查看最近日志）

每一步输出状态（✅/⚠️/❌）和修复建议。

### 3.6 scaffold — 新项目脚手架

**输入**: 项目名、目标品牌（Omron/Siemens/Modbus/Mitsubishi）、PLC 数量

**输出**: 生成以下文件结构：
```
项目根/
├── PlcConfig/

│   ├── plc_config.json          # PLC 连接配置模板
│   └── signals_config.csv       # 空信号表（含表头）
├── Connections/
│   └── {Brand}Connection.cs     # 品牌连接实现骨架
├── PlcMonitor.cs                # 顶层管理器骨架
├── PlcChannel.cs                # 单 PLC 通道骨架
├── SignalReader.cs              # 类型分发骨架
└── IStationHandler.cs           # 工位处理器接口
```

生成的代码引用 ELECTRICAL.md 中的架构模式，保持与参考实现一致的命名和风格。

## 四、输出文件清单

| 文件 | 类型 | 用途 |
|------|------|------|
| `ELECTRICAL.md` | 知识库文档 | 泛用架构模式 + 规范，Claude Code 自动加载 |
| `.claude/skills/plc-dev/SKILL.md` | Claude Code 技能 | /plc-dev 命令入口，6 个子命令行为定义 |

## 五、边界与不影响的范围

- 不修改现有 PLCHandler 项目代码
- 不修改现有 AutoRun 或 Station 实现
- 不涉及电气原理图/接线图绘制
- 不涉及 PLC 梯形图编程
- 不覆盖注液控制器(FSHCF10)、扫码枪(SR1000)、RFID(XKC601U) — 这些设备后续另行扩展
