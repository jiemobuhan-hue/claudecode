# SqlServerDapperHelper.cs 数据库操作帮助类

## 文件概述

`SqlServerDapperHelper.cs` 位于 `Service/` 目录下，封装了 SQL Server 数据库的两种操作方式：
1. **Dapper 轻量级 ORM** (`SqlServerDapperHelper` 类) - 用于简单的 CRUD
2. **传统 ADO.NET** (`SqlHelper` 类) - 用于复杂的数据库操作

---

## 依赖

```csharp
using Microsoft.Data.SqlClient;  // SQL Server 客户端
using Dapper;                     // 轻量级 ORM
```

---

## SqlServerDapperHelper 类

### 构造函数

```csharp
public SqlServerDapperHelper(string server, string database, string user, string password)
```

**参数**：
| 参数 | 类型 | 说明 |
|------|------|------|
| `server` | string | 服务器地址 |
| `database` | string | 数据库名 |
| `user` | string | 用户名 |
| `password` | string | 密码 |

**连接字符串格式**：
```
Server={server};Database={database};User ID={user};Password={password};TrustServerCertificate=True;
```

---

### 核心方法

#### 1. SaveListWithRealidAsync<T>

**功能**：全删全插模式保存列表，自动同步 ID

```csharp
public async Task SaveListWithRealignAsync<T>(List<T> dataList, string tableName) where T : class
```

**执行流程**：
```
1. 自动检查并创建表（如果不存在）
2. DELETE FROM tableName + DBCC CHECKIDENT (RESEED -1)  -- 清空并重置自增ID从0开始
3. 批量 INSERT（排除 ID 列，让数据库自增生成 0,1,2...）
4. 提交事务
```

**特点**：
- 使用事务保证数据一致性
- ID 列自增生成，确保连续
- 异常时自动回滚

---

#### 2. QueryAllAsync<T>

**功能**：查询表中所有记录

```csharp
public async Task<List<T>> QueryAllAsync<T>(string tableName)
```

**返回**：按 ID 升序排列的所有记录列表

---

#### 3. EnsureTableCreatedAsync<T> (私有)

**功能**：根据泛型 T 自动创建表

```csharp
private async Task EnsureTableCreatedAsync<T>(IDbConnection conn, string tableName, IDbTransaction trans)
```

**类型映射规则**：
| C# 类型 | SQL Server 类型 |
|---------|----------------|
| `int` | INT |
| `long` | BIGINT |
| `double` / `float` | FLOAT |
| `decimal` | DECIMAL(18,4) |
| `DateTime` | DATETIME |
| `bool` | BIT |
| 其他 | NVARCHAR(MAX) |

**ID 列特殊处理**：`INT PRIMARY KEY IDENTITY(0,1)`（从 0 开始自增）

---

## SqlHelper 类

传统 ADO.NET SQL Server 帮助类，提供更底层的数据库操作。

### 常量

```csharp
public static int CommandTimeOut = 600;  // 命令超时时间 600 秒
```

---

### 连接测试

#### IsConnectDB

```csharp
public static bool IsConnectDB(string connStr)
```

**功能**：测试数据库连接是否可用

---

### 增删改操作

#### ExecuteNonQuery

```csharp
public static int ExecuteNonQuery(string connStr, string sql, int cmdType, params SqlParameter[] parameters)
```

**功能**：执行增删改 SQL 或存储过程，返回受影响行数

| 参数 | 说明 |
|------|------|
| `connStr` | 连接字符串 |
| `sql` | SQL 语句或存储过程名 |
| `cmdType` | 1=SQL语句, 2=存储过程 |
| `parameters` | SQL 参数 |

---

#### ExecuteScalar

```csharp
public static object ExecuteScalar(string connStr, string sql, int cmdType, params SqlParameter[] parameters)
```

**功能**：执行查询，返回第一行第一列的值

---

### 查询操作

#### ExecuteReader

```csharp
public static SqlDataReader ExecuteReader(string connStr, string sql, int cmdType, params SqlParameter[] parameters)
```

**功能**：返回 SqlDataReader（断开式连接，自动关闭 Connection）

**注意**：调用方负责在用完后关闭 Reader

---

#### GetDataTable

```csharp
public static DataTable GetDataTable(string connStr, string sql, int cmdType, params SqlParameter[] parameters)
```

**功能**：查询结果填充到单个 DataTable

---

#### GetDataSet

```csharp
public static DataSet GetDataSet(string connStr, string sql, int cmdType, params SqlParameter[] parameters)
```

**功能**：查询结果填充到 DataSet

---

### 事务操作

#### ExecuteTrans (批量 SQL)

```csharp
public static bool ExecuteTrans(string connStr, List<string> listSql)
```

**功能**：事务批量执行多条 SQL 语句

**返回**：成功返回 `true`，异常回滚并抛出

---

#### ExecuteTrans (CommandInfo 列表)

```csharp
public static bool ExecuteTrans(string connStr, List<CommandInfo> comList)
```

**功能**：事务批量执行 `CommandInfo` 列表（支持存储过程）

```csharp
public class CommandInfo
{
    public string CommandText;      // SQL 或存储过程名
    public SqlParameter[] Paras;    // 参数列表
    public bool IsProc;            // 是否存储过程
}
```

---

### 辅助方法

#### BuilderCommand

```csharp
private static SqlCommand BuilderCommand(SqlConnection conn, string sql, int cmdType,
                                        SqlTransaction trans, params SqlParameter[] paras)
```

**功能**：构建 SqlCommand 对象，统一设置：
- CommandTimeout
- CommandType (Text / StoredProcedure)
- Transaction
- Parameters

---

#### CreateParameters<T>

```csharp
public static List<SqlParameter> CreateParameters<T>(T t)
```

**功能**：通过泛型反射自动创建 SQL 参数列表

**示例**：
```csharp
// User 有 Name, Age 属性
// 生成: [@name, @age]
```

---

### 日志表与存储过程

#### CreateOperationLogDataModelTable

```csharp
public static void CreateOperationLogDataModelTable(string sqlconn)
```

**功能**：创建操作日志表 `OperationLogData`

**表结构**：
| 列名 | 类型 | 说明 |
|------|------|------|
| num | BIGINT IDENTITY | 主键 |
| loginRoleName | VARCHAR(10) | 角色名 |
| logType | VARCHAR(15) | 日志类型 |
| modifyField | VARCHAR(30) | 修改字段 |
| oldValue | VARCHAR(30) | 旧值 |
| newValue | VARCHAR(30) | 新值 |
| modifyInfo | VARCHAR(100) | 详情 |
| createDatetime | DATETIME | 创建时间 |

---

#### CreateInsertOperationLogDataProc

```csharp
public static void CreateInsertOperationLogDataProc(string sqlconn)
```

**功能**：创建插入日志存储过程 `PROC_InsertOperationLogData`

---

#### CreateGetOperationLogDataByPageProc

```csharp
public static void CreateGetOperationLogDataByPageProc(string sqlconn)
```

**功能**：创建分页查询日志存储过程 `PROC_GetOperationLogDataByPage`

**参数**：`@pageIndex`, `@pageSize`, `@startTime`, `@endTime`, `@logType`

---

#### IsExistTable

```csharp
public static bool IsExistTable(string tableName, string sqlconn)
```

---

#### IsExistProc

```csharp
public static bool IsExistProc(string procName, string sqlconn)
```

---

## 使用示例

### Dapper 方式

```csharp
// 初始化
var db = new SqlServerDapperHelper(
    "DESKTOP-NENQRM5\\LOCALDB#AEC29A50",
    "BFSIDB",
    "sa",
    "123456789"
);

// 查询
var users = await db.QueryAllAsync<User>("");

// 保存（全删全插）
await db.SaveListWithRealignAsync(cellDataList, "CellData");
```

### SqlHelper 方式

```csharp
string connStr = "Server=...;Database=...;User ID=...;Password=...";

// 测试连接
bool connected = SqlHelper.IsConnectDB(connStr);

// 增删改
int rows = SqlHelper.ExecuteNonQuery(connStr,
    "UPDATE User SET Name=@name WHERE Id=@id", 1,
    new SqlParameter("@name", "test"),
    new SqlParameter("@id", 1));

// 查询单值
object result = SqlHelper.ExecuteScalar(connStr,
    "SELECT COUNT(*) FROM User", 1);

// 事务批量执行
var sqls = new List<string> {
    "INSERT INTO User VALUES('A')",
    "INSERT INTO User VALUES('B')"
};
SqlHelper.ExecuteTrans(connStr, sqls);
```

---

## 两个类的定位

| 类 | 定位 | 适用场景 |
|---|------|----------|
| `SqlServerDapperHelper` | Dapper ORM | 简单 CRUD、泛型操作、自动建表 |
| `SqlHelper` | 传统 ADO.NET | 复杂查询、存储过程、事务、批量操作 |

---

## 注意事项

1. `SqlServerDapperHelper.SaveListWithRealignAsync` 采用**全删全插**模式，不适合大数据量
2. 连接字符串使用 `TrustServerCertificate=True`，生产环境建议使用正式证书
3. `SqlHelper` 的 SQL 参数化查询可防止 SQL 注入
4. 事务操作异常时会自动回滚
