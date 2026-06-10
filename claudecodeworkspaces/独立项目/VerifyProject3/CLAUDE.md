# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

VisionProgram 三表 CRUD 验证工具 — .NET 8 控制台应用。对主项目 ZenergyBFSI 所使用的 SQL Server `VisionProgram` 数据库的三个检测相关表进行 Insert/Query/Update/Delete 全覆盖验证，含存储过程调用和直接 SQL 两种路径。

## 构建与运行

```bash
# 构建
dotnet build

# 运行
dotnet run
```

无测试项目，验证逻辑全部在 `Program.cs` 中以本地函数形式实现。

## 架构

### 模型层 (`Models/`)

三个实体类，DB-first 结构，字段与 `VisionProgram.dbo.*` 表列一一对应：

| 实体 | 来源表 | 备注 |
|------|--------|------|
| `T_BlueFilmDetection` | `T_BlueFilmDetection` | Num 为自增 PK，含 Reinvestment 字段 |
| `T_BlueFilmDataMOM` | `T_BlueFilmDataMOM` | 无 Reinvestment 字段，用 SideCellType 替代 CellType；2026-06-10 新增 8 个参数列 |
| `T_BlueFilmRecipeParameters` | `T_BlueFilmRecipeParameters` | ParameterID 为字符串 PK，含 6 个 ReserveField 预留列 |

### 仓储层 (`Repositories/`)

每个 Repository 封装对应的存储过程调用 + 直接 SQL 回退。统一使用原生 ADO.NET (`System.Data.SqlClient`)，无 ORM。

**存储过程覆盖度：**

| Repository | Insert | Query | Update | Delete | 备注 |
|---|---|---|---|---|---|
| `BlueFilmDetectionRepository` | sp ✓ | sp (分页, 中文列名) + 直接 SQL (GetByNum/GetAll) | 直接 SQL | 直接 SQL | PROC_GetBlueFilmDetection 不返回 Num 列，GetByNum 走直接 SQL |
| `BlueFilmDataMOMRepository` | sp ✓ | 全部直接 SQL | 直接 SQL | 直接 SQL | 分页存储过程 PROC_GetBlueFilmDataMOM 的 COUNT 走 `T_BlueFilmSide`（不存在的表），触发 bug，故全部查询走直接 SQL |
| `BlueFilmRecipeParametersRepository` | sp ✓ | sp ✓ | sp ✓ | sp ✓ | 唯一一类全部走存储过程的，CRUD 6 个 sp 全覆盖 |

**通用模式：** 所有 Repository 通过构造函数注入连接字符串。查询底层用 `SqlDataAdapter.Fill(DataTable)` + 手动 DataRow 映射。INSERT 用 `@@IDENTITY` 获取自增键（因 `SCOPE_IDENTITY()` 在此库中异常返回 NULL）。

### 入口 (`Program.cs`)

1. 构造带时间戳的测试标签 `VERIFY_yyyyMMddHHmmss`
2. 依次执行三个测试函数，每个覆盖 7 个操作：Insert → GetByNum → GetByCellCode → GetAll → Count → Update → Delete
3. 每个测试通过后清理生成的测试数据 (`SafeCleanup`)
4. 最后统计 `passed/total` 并输出

## 与本仓库主项目的关系

主项目 `ZenergyBFSI`（.NET Framework 4.8 WPF 应用）通过 `Service/CRUDServices/` 下的同名 Repository 访问相同三张表（使用 Dapper + `SqlServerDapperHelper`）。本项目的 Repository 独立实现（原生 ADO.NET），用于独立验证数据库存储过程和行为是否正确，不依赖主项目的依赖链（RinKit、DevExpress、HSL 等）。

## 重要约定

- **连接字符串硬编码在 Program.cs**，指向 `DESKTOP-0F9L4KO\RJ` SQL Server 实例。切换环境时需手动修改
- `BlueFilmDetectionRepository` 有两套列映射函数：`MapFromChineseColumns`（存储过程返回中文列名）和 `MapFromEnglishColumns`（直接 SQL 返回英文列名），不可混用
- `BlueFilmDataMOMRepository` 分页存储过程有已知 bug（COUNT 走不存在的 `T_BlueFilmSide` 表），不可在生产代码中使用该存储过程
- INSERT 存储过程不给 `@Num` 参数，由数据库自增生成
- `T_BlueFilmDataMOM` 无 `Reinvestment` 列，插入存储过程无 `@Reinvestment` 参数
- `T_BlueFilmDataMOM` 表 2026-06-10 新增 8 个参数列：ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult。旧 NG 列保留兼容但不再主动写入
