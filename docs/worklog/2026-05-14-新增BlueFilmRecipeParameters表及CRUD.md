# 工作日志 — SQL Server 新增 T_BlueFilmRecipeParameters 表及 CRUD

**日期**: 2026-05-14
**类型**: 新增

---

## 背景

需要在 VisionProgram 数据库中新增 `T_BlueFilmRecipeParameters`（蓝膜配方参数）表，按照现有的 `BlueFilmDetectionRepository` / `HarnessMeasureRepository` 模式（参照 CCDDataDal，使用 SqlHelper 调用 Claude 前缀存储过程），在 VerifyProject 独立项目中先验证。

## 新增文件

| 文件 | 说明 |
|------|------|
| `独立项目/Models/T_BlueFilmRecipeParameters.cs` | 实体模型，16 个字段，namespace `ZenergyBFSI.Workspace.Models` |
| `独立项目/CRUDServices/BlueFilmRecipeParametersRepository.cs` | CRUD 仓储类，6 个方法 + 异步重载，调用 6 个存储过程 |
| `独立项目/VerifyProject/CreateBlueFilmRecipeParameters.sql` | 建表 + 6 个存储过程，兼容 SQL Server 2008+（`IF EXISTS DROP + CREATE`） |

## 修改文件

| 文件 | 改动 |
|------|------|
| `VerifyProject/Program.cs` | 新增 `TestBlueFilmRecipeParametersCRUD()`（完整 Insert → Query → Update → Verify → Delete 自测）和 `EnsureTableAndProcsExist()`（运行时自动建表建存储过程） |
| `VerifyProject/CRUDVerify.csproj` | 修复 `SqlServerDapperHelper.cs` 相对路径（`../../` → `../../../`） |

## 存储过程清单

| 操作 | 存储过程名 | 主键 |
|------|-----------|------|
| Insert | `Proc_InsertBlueFilmRecipeParameters` | `@ParameterID` |
| GetAll | `PROC_Claude_GetAllBlueFilmRecipeParameters` | — |
| GetByID | `PROC_Claude_GetBlueFilmRecipeParametersByParameterID` | `@ParameterID` |
| Update | `PROC_Claude_UpdateBlueFilmRecipeParameters` | `@ParameterID` |
| Delete | `PROC_Claude_DeleteBlueFilmRecipeParameters` | `@ParameterID` |
| Count | `PROC_Claude_GetBlueFilmRecipeParametersCount` | — |

## 表结构 (T_BlueFilmRecipeParameters)

| 字段 | 类型 | 说明 |
|------|------|------|
| ParameterID | NVARCHAR(50) PK | 参数标识 |
| Description | NVARCHAR(200) | 描述 |
| UpdateTime | DATETIME | 更新时间 |
| ACK | INT | 确认标志 |
| Enable | INT (default 1) | 启用状态 |
| ParameterName | NVARCHAR(100) | 参数名 |
| ParameterType | NVARCHAR(50) | 参数类型 |
| UpperSpecificationsLimit | NVARCHAR(50) | 规格上限 |
| LowerSpecificationsLimit | NVARCHAR(50) | 规格下限 |
| Unit | NVARCHAR(20) | 单位 |
| status | NVARCHAR(20) | 状态 |
| ReserveField1-6 | NVARCHAR(100)×6 | 预留字段 |

## 遇到的坑

1. **`CREATE OR ALTER` 不兼容** — 用户真实数据库不支持此语法（SQL Server 2016+ 才支持），改为 `IF EXISTS DROP + CREATE GO` 模式
2. **csproj 相对路径错误** — `SqlServerDapperHelper.cs` 引用路径少了一层 `..`

## 待办

- 编译通过（0 warning 0 error），待用户在实际数据库运行验证
- 验证通过后，将 Model 和 Repository 同步到主项目 `Service/CRUDServices/` 目录
