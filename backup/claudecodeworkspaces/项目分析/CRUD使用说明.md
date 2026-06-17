# CRUD代码生成说明

## 概述

本工作区为 `VisionProgram` 数据库中的 `T_BlueFilmDetection`（蓝膜检测）和 `T_HarnessMeasure`（线束测量）两个表创建了完整的基于存储过程的CRUD代码。

## 文件结构

```
claudecodeworkspaces/
├── Models/
│   ├── T_BlueFilmDetection.cs      # 蓝膜检测实体类
│   └── T_HarnessMeasure.cs         # 线束测量实体类
├── CRUDServices/
│   ├── BlueFilmDetectionRepository.cs  # 蓝膜检测CRUD服务
│   └── HarnessMeasureRepository.cs     # 线束测量CRUD服务
├── SQLScripts/
│   └── CreateStoredProcedures.sql  # 存储过程创建脚本
├── Examples/
│   └── UsageExample.cs             # 使用示例
└── CRUD使用说明.md                  # 本文档
```

## 数据库配置

- **服务器**: `(localdb)\MSSQLLocalDB`
- **数据库**: `VisionProgram`
- **用户名**: `sa`
- **密码**: `123456789`
- **连接字符串**: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789;Trust Server Certificate=True`

## 使用步骤

### 1. 创建存储过程

首先在 SQL Server 中执行 `SQLScripts/CreateStoredProcedures.sql` 脚本，创建所需的表和存储过程。

### 2. 引用 SqlServerDapperHelper

在项目中引用 `ZenergyBFSI\Service\SqlServerDapperHelper.cs` 的相关代码。

### 3. 使用Repository进行CRUD操作

```csharp
// 初始化
string connStr = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789;Trust Server Certificate=True";
var blueFilmRepo = new BlueFilmDetectionRepository(connStr);
var harnessRepo = new HarnessMeasureRepository(connStr);

// 插入
var blueFilm = new T_BlueFilmDetection
{
    CellCode = "CELL001",
    DetectionTime = DateTime.Now,
    DetectionResult = "OK",
    CameraId = "CAM01",
    Operator = "张三",
    CreateTime = DateTime.Now
};
blueFilmRepo.Insert(blueFilm);

// 查询所有
var allRecords = blueFilmRepo.GetAll();

// 根据ID查询
var record = blueFilmRepo.GetById(1);

// 条件查询
var records = blueFilmRepo.GetByCellCode("CELL001");

// 分页查询
var pagedRecords = blueFilmRepo.GetByPage(pageIndex: 1, pageSize: 10);

// 更新
record.DetectionResult = "NG";
blueFilmRepo.Update(record);

// 删除
blueFilmRepo.Delete(1);

// 异步方法
await blueFilmRepo.InsertAsync(blueFilm);
await blueFilmRepo.GetAllAsync();
```

## T_BlueFilmDetection 表结构

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | bigint | 主键，自增 |
| CellCode | nvarchar(50) | 电芯编码 |
| DetectionTime | datetime | 检测时间 |
| DetectionResult | nvarchar(20) | 检测结果(OK/NG) |
| NgType | nvarchar(50) | NG类型 |
| NgPosition | nvarchar(100) | NG位置 |
| NgArea | float | NG面积 |
| CameraId | nvarchar(20) | 相机ID |
| Operator | nvarchar(20) | 操作员 |
| Remark | nvarchar(200) | 备注 |
| CreateTime | datetime | 创建时间 |

## T_HarnessMeasure 表结构

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | bigint | 主键，自增 |
| HarnessCode | nvarchar(50) | 线束编码 |
| MeasureTime | datetime | 测量时间 |
| Length | float | 长度 |
| Width | float | 宽度 |
| Height | float | 高度 |
| MeasureResult | nvarchar(20) | 测量结果 |
| Tolerance | nvarchar(50) | 公差 |
| StationId | nvarchar(20) | 工位ID |
| Operator | nvarchar(20) | 操作员 |
| Remark | nvarchar(200) | 备注 |
| CreateTime | datetime | 创建时间 |

## Repository方法列表

### BlueFilmDetectionRepository

| 方法 | 说明 |
|------|------|
| Insert | 插入记录 |
| InsertAsync | 异步插入记录 |
| Update | 更新记录 |
| UpdateAsync | 异步更新记录 |
| Delete | 删除记录 |
| DeleteAsync | 异步删除记录 |
| GetById | 根据ID查询 |
| GetByIdAsync | 异步根据ID查询 |
| GetAll | 查询所有记录 |
| GetAllAsync | 异步查询所有记录 |
| GetByPage | 分页查询 |
| GetByPageAsync | 异步分页查询 |
| GetCount | 获取记录总数 |
| GetCountAsync | 异步获取记录总数 |
| GetByCellCode | 根据电芯码查询 |
| GetByCellCodeAsync | 异步根据电芯码查询 |

### HarnessMeasureRepository

| 方法 | 说明 |
|------|------|
| Insert | 插入记录 |
| InsertAsync | 异步插入记录 |
| Update | 更新记录 |
| UpdateAsync | 异步更新记录 |
| Delete | 删除记录 |
| DeleteAsync | 异步删除记录 |
| GetById | 根据ID查询 |
| GetByIdAsync | 异步根据ID查询 |
| GetAll | 查询所有记录 |
| GetAllAsync | 异步查询所有记录 |
| GetByPage | 分页查询 |
| GetByPageAsync | 异步分页查询 |
| GetCount | 获取记录总数 |
| GetCountAsync | 异步获取记录总数 |
| GetByHarnessCode | 根据线束码查询 |
| GetByHarnessCodeAsync | 异步根据线束码查询 |

## 注意事项

1. 所有CRUD操作均使用存储过程实现
2. 异步方法内部使用 `Task.Run()` 包装同步操作
3. 分页查询的 `GetByPage` 方法会同时返回总数（第一个结果集）和分页数据（第二个结果集）
4. 使用 `SqlHelper` 类执行存储过程，遵循 `SqlServerDapperHelper.cs` 的设计模式
