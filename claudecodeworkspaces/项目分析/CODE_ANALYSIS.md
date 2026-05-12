# ZenergyBFSI 项目代码分析文档

## 项目概述

**ZenergyBFSI** 是一个基于 WPF 的上位机**蓝膜外观检测系统**，用于动力电池生产中的视觉检测工位。项目负责与 PLC、MOM 系统、扫码枪等设备通讯，实现电芯的入站、检测、出站全流程管理。

## 技术栈

| 类别 | 技术 |
|------|------|
| 框架 | WPF (.NET Framework) |
| UI 组件库 | DevExpress |
| PLC 通讯 | HslCommunication (OmronFinsNet) |
| 数据库 | SQLite (本地) + SQL Server (历史数据) |
| 日志 | RinKit (Rlog) |
| MOM 对接 | WCF HTTP Service |
| 架构 | 单例模式 + 消息总线 |

---

## 目录结构

```
ZenergyBFSI0416/
├── App.xaml / App.xaml.cs          # 应用程序入口
├── ZenergyBFSI.sln                 # 解决方案文件
├── Service/                         # 核心服务层
│   ├── AutoRun.cs                  # 自动机运行逻辑（核心）
│   ├── PlcHandler.cs               # PLC 通讯处理
│   ├── MomHandler.cs               # MOM 系统交互
│   ├── Settings.cs                 # 系统配置管理
│   ├── SQLiteGenericHelper.cs      # SQLite 数据库操作
│   ├── SqlServerDapperHelper.cs    # SQL Server 数据库操作
│   ├── FinsNetOmron.cs             # FINS 协议封装
│   ├── ICReader.cs                 # IC 读卡器（预留）
│   ├── CodeReader_1.cs             # 扫码枪服务
│   └── LogHelper.cs                # 日志辅助类
├── Model/                           # 数据模型
│   ├── CellData.cs                 # 电芯数据实体
│   ├── CellState.cs                # 电芯状态
│   ├── InspectionInfo.cs           # 点检信息
│   ├── InspectionUtils.cs         # 点检工具类
│   ├── ParameterInfo.cs            # 参数信息
│   ├── PlcBlock.cs                 # PLC 数据块
│   ├── ValveInfo.cs                # 阀门信息
│   ├── User.cs                     # 用户实体
│   ├── Log.cs                      # 日志实体
│   ├── History.cs                  # 历史记录
│   ├── CsvData.cs                  # CSV 数据服务
│   ├── WebViewBridge.cs            # WebView 桥接
│   ├── Device/                     # 设备驱动
│   │   ├── PLCOmronFins.cs         # 欧姆龙 PLC 通讯（FINS UDP）
│   │   ├── FSHCF10.cs              # 力传感器驱动
│   │   ├── SR1000.cs               # 扫码枪驱动
│   │   └── XKC601U.cs              # XKC601U 设备驱动
│   └── MOM/                        # MOM 接口模型
│       ├── BaseRequest.cs
│       ├── BaseResponse.cs
│       ├── CellOutput.cs           # 电芯出站
│       ├── EqptAlert.cs            # 设备报警
│       ├── EqptAlive.cs             # 设备心跳
│       ├── EqptRun.cs               # 设备运行
│       ├── EqptStatus.cs            # 设备状态
│       ├── Injection2Input.cs       # 二次注液进站
│       ├── MaterialDownLoad.cs
│       ├── MaterialUpLoad.cs
│       ├── ParameterCheck.cs
│       ├── PartDownLoad.cs
│       └── PartUpLoad.cs
├── View/                            # 界面层
│   ├── Main.xaml / .cs             # 主窗口
│   ├── UC_Home.xaml / .cs          # 主页看板
│   ├── UC_Operation.xaml / .cs     # 操作界面
│   ├── UC_Control.xaml / .cs       # 控制界面
│   ├── UC_Monitor.xaml / .cs       # 监控界面
│   ├── UC_Setting.xaml / .cs        # 设置界面
│   ├── UC_History.xaml / .cs       # 历史记录
│   ├── UC_Production.xaml / .cs   # 生产数据
│   ├── WD_Inspection.xaml / .cs    # 检测窗口
│   ├── WD_Alert.xaml / .cs         # 报警窗口
│   ├── PA_AddUser.xaml / .cs       # 添加用户
│   ├── PA_Signal.xaml / .cs        # 信号配置
│   ├── Bars/                       # 状态栏组件
│   │   └── UC_StatesBar.xaml / .cs
│   └── StateCards/                 # 状态卡片
│       ├── UC_StatesCards.xaml / .cs
│       ├── UC_InspectionView.xaml / .cs
│       ├── UC_ProductDashboard.xaml / .cs
│       └── UC_SettingsPages.xaml / .cs
├── MOM/                             # MOM WCF 引用
├── Data/                            # 数据文件目录
├── Resources/                       # 静态资源
│   ├── Noto/                        # Noto 字体
│   ├── Roboto/                      # Roboto 字体
│   └── web/                         # Web 资源
├── Properties/                      # 程序集属性
└── Local.db                         # SQLite 本地数据库
```

---

## 核心模块分析

### 1. PlcHandler - PLC 通讯服务

**职责**：通过 FINS UDP 协议与欧姆龙 PLC 通讯，实现数据读写。

**关键特性**：
- 单例模式确保全局唯一
- 使用 `HslCommunication` 库的 `OmronFinsNet`
- 支持多种数据类型：UInt16、Float、String、UTF8
- 4 种工作模式：
  - **Mode 1**：单点只读
  - **Mode 2**：单点读写同步
  - **Mode 4**：块读取（Byte/UInt16/Real）
  - **Mode 5**：块读写同步
- 心跳检测与自动重连

**核心流程**：
```csharp
// 初始化 PLC 连接
PlcHandler.I.Init();

// 读取 PLC 数据
var obj = PlcHandler.I.GetOBJ("地址名称");
ushort value = obj.vInt;  // 读取 UInt16
float value = obj.vFloat; // 读取 Float
bool value = obj.vBool;   // 读取 Bit
```

---

### 2. MomHandler - MOM 系统交互

**职责**：与制造执行系统(MOM)对接，实现参数下发、数据上报。

**主要接口**：
| 接口 | 功能 |
|------|------|
| `EqptAlive` | 设备心跳，检测 KeyFlag 触发停机 |
| `EqptRun` | 联机获取工艺参数 |
| `ParameterCheck` | 参数一致性校验 |
| `MaterialUpLoad` | 原材料上料查询 |
| `MaterialDownLoad` | 原材料下料 |
| `Injection2Input` | 电芯进站（MOM 查询来料状态） |
| `CellOutput` | 电芯出站上报 |
| `EqptStatus` | 设备状态上传 |
| `EqptAlert` | 设备报警上传 |

**心跳机制**：
- 间隔：3 秒（`Settings.MOM心跳间隔`）
- 联机计数达到阈值后执行：参数校验、版本上传、物料查询等

---

### 3. AutoRun - 自动机核心逻辑

**职责**：产品流转的主逻辑控制，包含入站/出站流程。

**核心流程**：
```
产品入站 → MOM 查询 → PLC 交互 → 产品出站
   ↓
1. ProductArrive(1~4)  - 工位产品到达
2. ProductLeadArrive(1~4) - 极耳产品到达
```

**关键数据**：
- `ListData<CellData>` - 电芯数据列表
- `Inspection` - 点检数据
- `Flag_Error` - 错误标志
- `LossCount` - 注液偏差计数

---

### 4. CellData - 电芯数据模型

**主要字段**：
```csharp
public class CellData
{
    public int Id { get; set; }
    public long TimeStamp { get; set; }
    public string 电芯码 { get; set; }           // 电芯唯一编码
    public string 进站时间 { get; set; }
    public string 检验位置 { get; set; }
    public bool 是否复投 { get; set; }

    // NG 类型记录（最多8种）
    public int Ng类型数量 { get; set; }
    public string Ng类型1 ~ Ng类型8 { get; set; }

    // MOM 交互结果
    public string 入站结果 { get; set; }        // MOM 查询结果
    public string 出站结果 { get; set; }
    public string MOM查询来料状态 { get; set; }
    public string MOM出站结果 { get; set; }

    // 视觉检测
    public string 视觉检测状态 { get; set; }   // 0:生产中 1:结束 -1:检测中 -2:备用
    public string 视觉检测参数一~六 { get; set; }
    public string 视觉检测结果 { get; set; }

    public int 人工复判次数 { get; set; }
}
```

---

### 5. 设备驱动

#### PLCOmronFins
- 封装 `OmronFinsNet`，支持 TCP FINS 协议
- 提供 `Read<T>` / `Write<T>` 泛型接口
- 心跳检测（500ms 超时）

#### SR1000
- 扫码枪驱动，TCP Socket 通讯
- 指令：`LON` 触发扫码，`CANCEL` 取消
- ASCII 协议解析

#### FSHCF10
- 力传感器通讯驱动

#### XKC601U
- XKC601U 系列设备驱动

---

## 数据流

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   扫码枪    │────▶│   上位机    │◀───▶│  欧姆龙PLC  │
│  (SR1000)   │     │  (Zenergy)  │     │ (FINS UDP)  │
└─────────────┘     └──────┬──────┘     └─────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │  MOM系统    │
                   │ (WCF HTTP)  │
                   └─────────────┘
```

---

## 关键配置 (Settings)

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `PLC_IP` | `127.0.0.1` | PLC 地址 |
| `PLC_Port` | `9600` | PLC 端口 |
| `PLC循环等待` | `80ms` | PLC 轮询间隔 |
| `MOM地址` | `http://10.6.33.3:8007/wcfhttpservice` | MOM 服务地址 |
| `MOM心跳间隔` | `3000ms` | 心跳间隔 |
| `MOM在线` | `1` | MOM 联机开关 |
| `电芯型号` | `Test` | 产品型号 |
| `EquipmentCode` | `Test1` | 设备编号 |

---

## 界面结构

```
MainWindow
├── UC_StatesBar (状态栏)
└── ContentArea
    ├── UC_Home         - 主页看板
    ├── UC_Operation    - 操作界面
    ├── UC_Control      - 控制界面
    ├── UC_Monitor      - 监控界面
    ├── UC_Setting      - 设置界面
    ├── UC_History      - 历史记录
    └── UC_Production   - 生产数据

Dialogs:
├── WD_Inspection       - 检测详情窗口
├── WD_Alert            - 报警窗口
├── PA_AddUser          - 添加用户
└── PA_Signal           - 信号配置
```

---

## 数据库

### SQLite (Local.db)
- 存储系统配置、PLC 地址映射、参数配置
- 使用 RinKit 的 `Rdb` 封装

### SQL Server
- 存储历史生产数据
- 使用 Dapper 轻量级 ORM

---

## 技术特点

1. **单例模式广泛使用**：PlcHandler、AutoRun、MomHandler 均采用线程安全的单例
2. **消息日志**：通过 `UC_Operation.I.WriteLog()` 统一日志输出
3. **异步任务**：大量使用 `Task.Run()` 处理耗时操作
4. **配置持久化**：Settings 类通过 `Rdb.SaveSettings/LoadSettings` 实现配置序列化
5. **WCF 服务引用**：MOM 接口通过 WSDL 生成代理类

---

## 代码注释风格

- 使用中文注释
- `TODO` 标记未完成功能
- 方法有简单的 Summary 文档注释

---

## 注意事项

1. 项目中部分代码被注释（如旧版 PLC 连接方式、注液相关逻辑），可能处于过渡期
2. `AutoRun.cs` 中存在硬编码的 SQL Server 连接字符串
3. MOM 接口模型中部分类存在多个版本，需确认实际使用的接口版本
4. 视觉检测相关参数（`视觉检测参数一~六`）的具体含义需进一步确认
