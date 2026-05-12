# ZenergyBFSI 项目代码结构分析文档

**项目路径**: `D:\蓝膜外观检测上位机\ZenergyBFSI0430`
**文档创建日期**: 2026-04-30
**项目类型**: WPF WinForms 应用 (.NET Framework 4.8)

---

## 1. 项目概述

**项目名称**: ZenergyBFSI (正力新能蓝膜检测智能控制系统)

**核心功能**:
- 蓝膜外观检测系统
- 电芯检测数据看板
- 历史数据管理
- 产能统计
- 与MOM（制造运营管理）系统集成
- PLC设备通信

---

## 2. 技术栈

| 组件 | 版本/说明 |
|------|-----------|
| .NET Framework | 4.8 |
| UI框架 | WPF + DevExpress XPF |
| 数据库 | SQL Server + SQLite |
| ORM | Dapper + EntityFramework |
| PLC通信 | HslCommunication (Omron Fins协议) |
| 图表 | LiveCharts (SkiaSharp) |
| 日志 | NLog |
| MOM集成 | WCF Web Service |

---

## 3. 项目结构

```
ZenergyBFSI0430/
├── App.xaml / App.xaml.cs          # 应用程序入口
├── App.config                        # 应用程序配置
├── ZenergyBFSI.csproj               # 项目文件
├── Properties/
│   ├── AssemblyInfo.cs
│   ├── Resources.Designer.cs
│   └── Settings.Designer.cs
├── Model/                           # 数据模型
│   ├── CellData.cs
│   ├── CellState.cs
│   ├── CsvData.cs
│   ├── DataINJ.cs
│   ├── DataPLC.cs
│   ├── History.cs
│   ├── InspectionInfo.cs            # 设备点检类（含工位1-6真空数据）
│   ├── InspectionUtils.cs
│   ├── Log.cs
│   ├── ParameterInfo.cs
│   ├── PlcBlock.cs
│   ├── ProductionItem.cs
│   ├── User.cs
│   ├── ValveInfo.cs
│   ├── WebViewBridge.cs
│   ├── Device/
│   │   ├── FSHCF10.cs
│   │   ├── PLCOmronFins.cs
│   │   ├── SR1000.cs
│   │   └── XKC601U.cs
│   ├── MOM/                         # MOM系统相关模型
│   │   ├── BaseRequest.cs
│   │   ├── BaseResponse.cs
│   │   ├── CellOutput.cs
│   │   ├── EqptAlert.cs
│   │   ├── EqptAlive.cs
│   │   ├── EqptRun.cs
│   │   ├── EqptStatus.cs
│   │   ├── Injection2Input.cs
│   │   ├── MaterialDownLoad.cs
│   │   ├── MaterialUpLoad.cs
│   │   ├── ParameterCheck.cs
│   │   ├── PartDownLoad.cs
│   │   └── PartUpLoad.cs
│   └── Vision/                      # 视觉检测相关模型
│       ├── T_BlueFilmDataMOM.cs
│       ├── T_BlueFilmDetection.cs   # 蓝膜检测记录
│       └── T_HarnessMeasure.cs     # 线束测量记录
├── Service/                         # 服务层
│   ├── AutoRun.cs
│   ├── CodeReader_1.cs
│   ├── FinsNetOmron.cs
│   ├── ICReader.cs
│   ├── LogHelper.cs
│   ├── MomHandler.cs                # MOM通信处理
│   ├── OmronConnectedCip.cs
│   ├── PlcHandler.cs                # PLC通信处理
│   ├── Settings.cs
│   ├── SQLiteGenericHelper.cs
│   ├── SqlServerDapperHelper.cs     # SQL Server + Dapper 辅助类
│   └── CRUDServices/
│       ├── BlueFilmDetectionRepository.cs
│       └── HarnessMeasureRepository.cs
├── View/                           # 视图层
│   ├── Main.xaml / Main.xaml.cs    # 主窗口
│   ├── UC_Home.xaml / .cs         # 电芯检测看板
│   ├── UC_History.xaml / .cs       # 历史数据
│   ├── UC_Operation.xaml / .cs     # 检测结果
│   ├── UC_Production.xaml / .cs    # 产能统计
│   ├── UC_Setting.xaml / .cs        # 参数设置
│   ├── UC_Control.xaml / .cs
│   ├── UC_Monitor.xaml / .cs        # 信号监控
│   ├── PA_AddUser.xaml / .cs
│   ├── PA_Signal.xaml / .cs
│   ├── WD_Alert.xaml / .cs
│   ├── WD_Inspection.xaml / .cs
│   ├── ListScrollBottomBehavior.cs
│   ├── Bars/
│   │   └── UC_StatesBar.xaml / .cs
│   └── StateCards/
│       ├── UC_ProductDashboard.xaml / .cs
│       ├── UC_InspectionView.xaml / .cs
│       ├── UC_SettingsPages.xaml / .cs
│       └── UC_StatesCards.xaml / .cs
├── MOM/                            # MOM WebService代理
│   ├── Reference.cs
│   └── Reference2.cs
├── Connected Services/
│   └── MOM/                        # WCF服务引用
├── Resources/                      # 资源文件
│   ├── Roboto/                    # 字体文件
│   └── Noto/
├── lib/                            # 第三方库
│   ├── DevExpress/                # DevExpress DLL
│   ├── 图表/                      # LiveCharts相关
│   ├── HslCommunication.dll       # PLC通信
│   ├── MQTTnet.dll                # MQTT通信
│   ├── Newtonsoft.Json.dll
│   ├── NLog.dll
│   ├── RinKitNet.dll / RinKitWPF.dll
│   └── System.Data.SQLite.dll
├── Data/                           # 数据目录
├── Images/                         # 图片目录
├── MOM/                            # MOM相关文档
├── claudecodeworkspaces/          # Claude工作区
└── packages/                      # NuGet包
```

---

## 4. 核心类说明

### 4.1 SqlServerDapperHelper (Service\SqlServerDapperHelper.cs)

数据库访问核心类，包含两个主要部分：

**SqlServerDapperHelper 类**:
- `SaveListWithRealignAsync<T>()` - 保存列表并自动同步ID（全删全插模式）
- `QueryAllAsync<T>()` - 通用读取方法
- `EnsureTableCreatedAsync<T>()` - 自动建表逻辑

**SqlHelper 类** (静态工具类):
```csharp
// cmdType: 1=SQL语句, 2=存储过程
SqlHelper.ExecuteNonQuery(connStr, sql, cmdType, parameters);
SqlHelper.GetDataTable(connStr, sql, cmdType, parameters);
SqlHelper.ExecuteScalar(connStr, sql, cmdType, parameters);
SqlHelper.ExecuteTrans(connStr, listSql);
SqlHelper.CreateParameters<T>(t);  // 反射创建SQL参数
```

### 4.2 MomHandler (Service\MomHandler.cs)

MOM（制造运营管理）系统通信处理类，单例模式。

**主要功能**:
- 与MOM系统WebService通信
- 上报设备状态、报警、产能数据
- 下发物料、参数等配置

**关键字段**:
```csharp
private WsWcfServiceClient _momOffical;  // MOM官方服务客户端
private List<ParameterInfo> _listParam;
private List<MaterialUpLoad_MaterialInfo> _material;
private List<CellData> _history;
```

### 4.3 PlcHandler (Service\PlcHandler.cs)

PLC（可编程逻辑控制器）通信处理类，单例模式。

**主要功能**:
- 通过Omron Fins协议与PLC通信
- 读写PLC寄存器数据
- 设备状态监控

**关键字段**:
```csharp
public PLCOmronFins _omronFins;  // Omron Fins协议实现
private List<PlcObj> _listPlcObj;
private List<PlcBlock> _listPlcBlock;
```

### 4.4 InspectionInfo (Model\InspectionInfo.cs)

设备点检数据模型，包含大量真空检测相关字段：

- 称重点检上/下限
- 真空点检上/下限
- 前/后称重点检1-4
- 工位1-6的真空变化值(1-8)
- 工位1-6的高真空值(1-8)
- 工位1-6的低真空值(1-8)

### 4.5 AutoRun (Service\AutoRun.cs)

系统自动运行核心类，**sealed 单例模式**，负责整个系统的自动化运行逻辑。

**主要职责**:
- 循环检测设备连接状态
- 处理产品入站/出站流程
- 与PLC、MOM系统交互
- 心跳维护

**关键属性**:
```csharp
public List<CellData> ListData { get; set; }  // 电芯数据列表
public int Flag_Error { get; set; }            // 错误计数
public int Power { get; set; }                // 功率
public int LossCount { get; set; }            // 注液偏差计数
public InspectionInfo Inspection { get; set; } // 点检数据
```

**核心方法**:

| 方法 | 说明 |
|------|------|
| `Init()` | 初始化：连接SQL Server数据库，加载CellData，启动循环任务 |
| `Thread_Run()` | **主循环线程**：设备连接检测 → 心跳 → ProductArrive(1-4) → ProductLeadArrive(1-4) |
| `DeviceLink()` | 检测PLC连接状态，更新UI状态指示灯 |
| `HeartBeat()` | PLC心跳响应 |
| `ProductArrive(int no)` | **入站处理**（工位1-4）：获取电芯码 → 查询MOM → 写入PLC结果 → 存储本地数据 |
| `ProductLeadArrive(int no)` | **出站处理**（工位1-4）：获取电芯码 → 查询SQLServer视觉数据 → 视觉排序算法 → 写入PLC分流结果 |
| `GetCodeFromTunnal(int no)` | 从PLC获取工位电芯码 |
| `GetReloadres()` | 处理复投电芯的检测结果数组转换 |
| `UpdateCellDataFromSQLserver()` | 从SQLServer查询视觉检测数据更新CellData |
| `GetIO/GetInt/GetString` | 读取PLC寄存器（Bool/Int/String） |
| `SetIO/SetInt/SetString` | 写入PLC寄存器（Bool/Int/String） |

**运行流程**:
```
Thread_Run() 循环:
├── DeviceLink() → 检查PLC连接
├── HeartBeat() → 响应PLC心跳
├── ProductArrive(1-4) → 产品入站（扫码+MOM查询+写入PLC）
├── ProductLeadArrive(1-4) → 产品出站（视觉检测+分流）
└── Thread.Sleep(Settings.自动机循环等待)
```

**数据流**:
1. PLC触发"来料触发"信号 → 获取电芯码 → 查询MOM → MOM返回OK/NG → 写入PLC"来料结果"
2. 复投处理：查询本地SQLServer数据库获取历史检测数据 → 写入PLC复投信息
3. 出站时：从SQLServer查询T_HarnessMeasure数据 → 更新CellData → 视觉排序 → 写入PLC"分流通道结果"

**数据库连接**:
```csharp
// LocalDB SQLServer 连接
_connectionStringA = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789";
_localHarnessMeasureRepositoryA  // 线束测量仓储
_localBlueFilmDetectionRepositoryA // 蓝膜检测仓储
```

**PLC交互方式**:
- 通过`PlcHandler.I._omronFins.omronFinsNet`读写PLC寄存器
- 使用`GetOBJ(name)`获取PlcObj对象，再读取vBool/vInt/vFloat/vString属性
- 地址映射存储在PlcObj.Adress中

---

### 4.6 CRUD Repository 类

**BlueFilmDetectionRepository** (Service\CRUDServices\BlueFilmDetectionRepository.cs):
- 对应表: `T_BlueFilmDetection`
- 使用存储过程: `Proc_InsertBlueFilmDetection`, `PROC_Claude_GetAllBlueFilmDetection` 等

**HarnessMeasureRepository** (Service\CRUDServices\HarnessMeasureRepository.cs):
- 对应表: `T_HarnessMeasure`
- 使用存储过程: `Proc_InsertHarnessMeasure` 等

---

## 5. 视图结构 (Main.xaml)

主窗口使用 DXTabControl 实现多标签页：

| Tab | Header | 内容 |
|-----|--------|------|
| 1 | 电芯检测看板 | UC_Home |
| 2 | 历史数据 | UC_History |
| 3 | 检测结果 | UC_Operation |
| 4 | 产能统计 | UC_Production |
| 5 | 信号监控 | UC_Monitor (隐藏) |
| 6 | 参数设置 | UC_Setting |
| 7 | 用户管理 | (未实现) |

底部状态栏: UC_StatesBar 显示运行状态

---

## 6. 数据库表结构

### T_BlueFilmDetection (蓝膜检测表)
| 字段 | 类型 | 说明 |
|------|------|------|
| Num | int | 主键，自增 |
| BottomCellType | nchar(10) | 底壳电芯类型 |
| CellCode | nvarchar(50) | 电芯编码 |
| DetectionArea | nchar(10) | 检测区域 |
| DetectionResults | nchar(10) | 检测结果 |
| NGtypeNum | int | NG类型数量 |
| NGtype1-3 | nchar(10) | NG类型1-3 |
| CreateTime | datetime | 创建时间 |

### T_HarnessMeasure (线束测量表)
| 字段 | 类型 | 说明 |
|------|------|------|
| Num | int | 主键，自增 |
| PackCode | nvarchar(50) | 包装编码 |
| MarkNumber | int | 标记编号 |
| Result | nvarchar(50) | 结果 |
| Width1-6 | decimal(18,4) | 宽度1-6 |
| WidthStandard | decimal(18,4) | 宽度标准 |
| CreateTime | datetime | 创建时间 |

---

## 7. 关键存储过程

| 存储过程 | 用途 |
|----------|------|
| `Proc_InsertBlueFilmDetection` | 插入蓝膜检测记录 |
| `Proc_InsertHarnessMeasure` | 插入线束测量记录 |
| `PROC_GetBlueFilmDataMOM` | 蓝膜分页查询(MOM) |
| `PROC_GetBlueFilmDetection` | 蓝膜分页查询 |
| `Proc_InsertBlueFilmDataMOM` | 蓝膜数据MOM插入 |
| `PROC_Claude_*` | Claude前缀的CRUD存储过程 |

---

## 8. 第三方库依赖

| 库 | 用途 |
|----|------|
| DevExpress | UI组件库 |
| HslCommunication | PLC通信 (Omron Fins) |
| Dapper / Dapper.Contrib | 数据库ORM |
| EntityFramework | 数据库ORM |
| LiveChartsCore | 图表绘制 |
| MQTTnet | MQTT通信 |
| Newtonsoft.Json | JSON序列化 |
| NLog | 日志记录 |
| MaterialDesign | UI主题 |
| RinKitNet / RinKitWPF | 图像处理? |

---

## 9. 配置文件

**App.config** 包含:
- 数据库连接字符串 (SQL Server)
- MOM WebService endpoint配置
- 应用程序设置

---

## 10. 开发相关文件

| 文件 | 说明 |
|------|------|
| `正力新能 MOM项目-设备集成接口规约 V2.39.docx` | MOM接口规约文档 |
| `claudecodeworkspaces/` | Claude Code工作区 |
| `LoginProject/` | 登录页面项目 (WPF + MaterialDesign) |

---

## 备注

- 项目使用单例模式管理全局服务（MomHandler, PlcHandler）
- 数据库操作主要通过存储过程
- UI使用DevExpress组件库实现现代化界面
- MOM通信采用WCF WebService方式