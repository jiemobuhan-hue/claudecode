using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZenergyBFSI.Service
{
    // =========================================================================
    //  写队列：所有写操作串行化，消除 "database is locked"
    // =========================================================================

    public sealed class DbWriteQueue : IDisposable
    {
        private static readonly Lazy<DbWriteQueue> _lazy =
            new Lazy<DbWriteQueue>(() => new DbWriteQueue(),
                LazyThreadSafetyMode.ExecutionAndPublication);

        public static DbWriteQueue Instance => _lazy.Value;

        private sealed class WorkItem
        {
            public readonly Action Action;
            public readonly TaskCompletionSource<bool> Tcs =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            public WorkItem(Action a) => Action = a;
        }

        private readonly BlockingCollection<WorkItem> _queue =
            new BlockingCollection<WorkItem>(new ConcurrentQueue<WorkItem>());
        private bool _disposed;

        private DbWriteQueue()
        {
            new Thread(() =>
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    try { item.Action(); item.Tcs.TrySetResult(true); }
                    catch (Exception ex) { item.Tcs.TrySetException(ex); }
                }
            })
            { Name = "SQLite-WriteQueue", IsBackground = true }.Start();
        }

        public Task EnqueueAsync(Action write)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DbWriteQueue));
            var item = new WorkItem(write);
            _queue.Add(item);
            return item.Tcs.Task;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _queue.CompleteAdding();
        }
    }

    // =========================================================================
    //  反射元数据缓存（每种 T 只反射一次）
    // =========================================================================

    internal static class TypeMeta<T>
    {
        public static readonly PropertyInfo[] Props =
            typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .ToArray();

        public static readonly Dictionary<string, PropertyInfo> PropMap =
            Props.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        public static readonly string TableName = typeof(T).Name;
    }

    // =========================================================================
    //  SQLiteGenericHelper
    // =========================================================================

    public static class SQLiteGenericHelper
    {
        private static string _connStr;
        private static readonly object _initLock = new object();

        private static string ConnStr
        {
            get
            {
                if (_connStr != null) return _connStr;
                lock (_initLock)
                {
                    if (_connStr != null) return _connStr;
                    Initialize();
                }
                return _connStr;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  初始化
        // ─────────────────────────────────────────────────────────────────────

        public static void Initialize(string dbPath = null)
        {
            if (dbPath == null)
                dbPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Local.db");

            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connStr = new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                Version = 3,
                JournalMode = SQLiteJournalModeEnum.Wal, 
                CacheSize = 8000,
                FailIfMissing = false,
                Pooling = true
            }.ToString();

            // WAL 模式激活（只需执行一次，持久化到数据库文件）
            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "PRAGMA journal_mode=WAL;" +
                        "PRAGMA synchronous=NORMAL;" +
                        "PRAGMA foreign_keys=ON;";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static SQLiteConnection OpenConn()
        {
            var conn = new SQLiteConnection(ConnStr);
            conn.Open();
            return conn;
        }

        // =========================================================================
        //  ████  BulkUpsert  ████
        //
        //  对每一行执行：
        //    Step1: UPDATE "Tbl" SET col1=@v1, col2=@v2 WHERE keyCol=@key
        //    Step2: 若 Step1 影响行数 == 0 → INSERT INTO "Tbl" (所有列) VALUES (...)
        //
        //  · 不依赖任何表约束，对任意现有表均有效
        //  · key 列值为空时跳过该条，不影响其余行
        //  · 单条异常跳过，事务整体提交成功的行
        // =========================================================================

        /// <summary>
        /// 批量 Upsert：按 <paramref name="keyPropertyName"/> 指定的属性判断行是否存在，
        /// 存在则更新其余列，不存在则插入，绝不产生重复行。
        /// <para>不需要表有任何约束，传入的属性名对应普通列即可。</para>
        /// </summary>
        /// <param name="dataList">要写入的数据列表</param>
        /// <param name="keyPropertyName">
        ///   用于判断行是否存在的属性名，例如 nameof(Product.Code)。
        /// </param>
        /// <param name="tableName">表名，不传则使用类名。</param>
        public static void BulkUpsert<T>(
            ICollection<T> dataList,
            string keyPropertyName,
            string tableName = null)
        {
            BulkUpsert(dataList, new[] { keyPropertyName }, tableName);
        }

        /// <summary>支持联合 key（多个属性同时作为判断依据）。</summary>
        public static void BulkUpsert<T>(
            ICollection<T> dataList,
            string[] keyPropertyNames,
            string tableName = null)
        {
            if (dataList == null || dataList.Count == 0) return;
            DbWriteQueue.Instance
                .EnqueueAsync(() => BulkUpsertCore(dataList, keyPropertyNames, tableName))
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步批量 Upsert，UI 线程调用不阻塞界面。
        /// await 后写入已全部完成。
        /// </summary>
        public static Task BulkUpsertAsync<T>(
            List<T> dataList,
            string keyPropertyName,
            string tableName = null)
        {
            return BulkUpsertAsync(dataList, new[] { keyPropertyName }, tableName);
        }

        /// <summary>支持联合 key 的异步重载。</summary>
        public static Task BulkUpsertAsync<T>(
            List<T> dataList,
            string[] keyPropertyNames,
            string tableName = null)
        {
            if (dataList == null || dataList.Count == 0)
                return Task.CompletedTask;

            return DbWriteQueue.Instance.EnqueueAsync(
                () => BulkUpsertCore(dataList, keyPropertyNames, tableName));
        }

        // ── 核心：UPDATE → 若无影响行则 INSERT ───────────────────────────────

        private static void BulkUpsertCore<T>(
            ICollection<T> dataList,
            string[] keyPropertyNames,
            string tableName)
        {
            var allProps = TypeMeta<T>.Props;
            var tbl = tableName ?? TypeMeta<T>.TableName;

            // 分出 key 属性 和 非key属性
            var keySet = new HashSet<string>(
                keyPropertyNames ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var keyProps = allProps.Where(p => keySet.Contains(p.Name)).ToArray();
            var nonKeyProps = allProps.Where(p => !keySet.Contains(p.Name)).ToArray();

            if (keyProps.Length == 0)
                throw new ArgumentException(
                    $"未找到属性 [{string.Join(", ", keyPropertyNames)}]，" +
                    $"请检查属性名与类型 {typeof(T).Name} 的属性名是否一致（区分大小写不重要）。");

            // ── 构建 UPDATE SQL ───────────────────────────────────────────────
            //
            //   UPDATE "Tbl"
            //   SET "Col1"=@s0, "Col2"=@s1
            //   WHERE "KeyCol"=@w0
            //
            // 参数名前缀：@s = SET，@w = WHERE（避免同名冲突）

            var setClause = string.Join(", ",
                nonKeyProps.Select((p, i) => $"\"{p.Name}\"=@s{i}"));

            var whereClause = string.Join(" AND ",
                keyProps.Select((p, i) => $"\"{p.Name}\"=@w{i}"));

            var updateSql = $"UPDATE \"{tbl}\" SET {setClause} WHERE {whereClause}";

            // ── 构建 INSERT SQL ───────────────────────────────────────────────
            //
            //   INSERT INTO "Tbl" ("Col1","Col2","KeyCol")
            //   VALUES (@i0, @i1, @i2)

            var insertCols = string.Join(", ",
                allProps.Select(p => $"\"{p.Name}\""));
            var insertParms = string.Join(", ",
                allProps.Select((_, i) => $"@i{i}"));
            var insertSql = $"INSERT INTO \"{tbl}\" ({insertCols}) VALUES ({insertParms})";

            // ── 执行 ─────────────────────────────────────────────────────────

            int updated = 0;
            int inserted = 0;
            int skipped = 0;

            using (var conn = OpenConn())
            using (var tx = conn.BeginTransaction())
            {
                // 预编译两条语句，重复使用
                using (var updateCmd = conn.CreateCommand())
                using (var insertCmd = conn.CreateCommand())
                {
                    updateCmd.Transaction = tx;
                    insertCmd.Transaction = tx;

                    updateCmd.CommandText = updateSql;
                    insertCmd.CommandText = insertSql;

                    // 预分配 UPDATE 参数
                    for (int i = 0; i < nonKeyProps.Length; i++)
                        updateCmd.Parameters.AddWithValue($"@s{i}", DBNull.Value);
                    for (int i = 0; i < keyProps.Length; i++)
                        updateCmd.Parameters.AddWithValue($"@w{i}", DBNull.Value);

                    // 预分配 INSERT 参数
                    for (int i = 0; i < allProps.Length; i++)
                        insertCmd.Parameters.AddWithValue($"@i{i}", DBNull.Value);

                    updateCmd.Prepare();
                    insertCmd.Prepare();

                    foreach (var item in dataList)
                    {
                        // key 值为空时跳过该条
                        if (keyProps.Any(kp => IsEmpty(kp.GetValue(item))))
                        {
                            skipped++;
                            continue;
                        }

                        try
                        {
                            // Step 1：填 UPDATE 参数并执行
                            for (int i = 0; i < nonKeyProps.Length; i++)
                                updateCmd.Parameters[i].Value =
                                    nonKeyProps[i].GetValue(item) ?? DBNull.Value;

                            for (int i = 0; i < keyProps.Length; i++)
                                updateCmd.Parameters[nonKeyProps.Length + i].Value =
                                    keyProps[i].GetValue(item) ?? DBNull.Value;

                            int affected = updateCmd.ExecuteNonQuery();

                            if (affected > 0)
                            {
                                // 行已存在，UPDATE 成功
                                updated++;
                            }
                            else
                            {
                                // Step 2：行不存在，执行 INSERT
                                for (int i = 0; i < allProps.Length; i++)
                                    insertCmd.Parameters[i].Value =
                                        allProps[i].GetValue(item) ?? DBNull.Value;

                                insertCmd.ExecuteNonQuery();
                                inserted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 单条失败只跳过，不影响其他行
                            skipped++;
                            var keyVal = keyProps.FirstOrDefault()?.GetValue(item);
                            System.Diagnostics.Debug.WriteLine(
                                $"[BulkUpsert] {tbl} 单条跳过 (key={keyVal}): {ex.Message}");
                        }
                    }
                }

                tx.Commit();
            }

            System.Diagnostics.Debug.WriteLine(
                $"[BulkUpsert] {tbl}：更新 {updated} 条，插入 {inserted} 条，跳过 {skipped} 条");
        }

        // =========================================================================
        //  其他 CRUD
        // =========================================================================

        public static List<T> QueryAll<T>(string tableName = null) where T : new() =>
            QueryRaw<T>($"SELECT * FROM \"{tableName ?? TypeMeta<T>.TableName}\"");

        public static Task<List<T>> QueryAllAsync<T>(string tableName = null) where T : new() =>
            Task.Run(() => QueryAll<T>(tableName));

        public static void UpdateRows(
            string tableName,
            string targetColumn, object targetValue,
            string whereColumn, object whereValue)
        {
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        $"UPDATE \"{tableName}\" " +
                        $"SET \"{targetColumn}\"=@tv " +
                        $"WHERE \"{whereColumn}\"=@wv";
                    cmd.Parameters.AddWithValue("@tv", targetValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@wv", whereValue ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }).GetAwaiter().GetResult();
        }

        public static Task UpdateRowsAsync(
            string tableName,
            string targetColumn, object targetValue,
            string whereColumn, object whereValue) =>
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        $"UPDATE \"{tableName}\" " +
                        $"SET \"{targetColumn}\"=@tv " +
                        $"WHERE \"{whereColumn}\"=@wv";
                    cmd.Parameters.AddWithValue("@tv", targetValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@wv", whereValue ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            });

        public static void DeleteRow(string tableName, string keyColumn, object keyValue)
        {
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        $"DELETE FROM \"{tableName}\" WHERE \"{keyColumn}\"=@v";
                    cmd.Parameters.AddWithValue("@v", keyValue ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }).GetAwaiter().GetResult();
        }

        public static Task DeleteRowAsync(string tableName, string keyColumn, object keyValue) =>
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        $"DELETE FROM \"{tableName}\" WHERE \"{keyColumn}\"=@v";
                    cmd.Parameters.AddWithValue("@v", keyValue ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            });

        public static void ClearTable(string tableName)
        {
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DELETE FROM \"{tableName}\"";
                    cmd.ExecuteNonQuery();
                }
            }).GetAwaiter().GetResult();
        }

        public static Task ClearTableAsync(string tableName) =>
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DELETE FROM \"{tableName}\"";
                    cmd.ExecuteNonQuery();
                }
            });

        public static void ExecuteNonQuery(string sql, params object[] args)
        {
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    for (int i = 0; i < args.Length; i++)
                        cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }).GetAwaiter().GetResult();
        }

        public static Task ExecuteNonQueryAsync(string sql, params object[] args) =>
            DbWriteQueue.Instance.EnqueueAsync(() =>
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    for (int i = 0; i < args.Length; i++)
                        cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            });

        public static List<T> QueryRaw<T>(string sql, params object[] args) where T : new()
        {
            using (var conn = OpenConn())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                for (int i = 0; i < args.Length; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);

                using (var reader = cmd.ExecuteReader())
                {
                    var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                        colMap[reader.GetName(i)] = i;

                    var result = new List<T>();
                    var props = TypeMeta<T>.PropMap;

                    while (reader.Read())
                    {
                        var item = new T();
                        foreach (var kv in colMap)
                        {
                            if (!props.TryGetValue(kv.Key, out var prop) || !prop.CanWrite)
                                continue;
                            var raw = reader.GetValue(kv.Value);
                            if (raw == DBNull.Value || raw == null) continue;
                            try
                            {
                                prop.SetValue(item, Convert.ChangeType(
                                    raw,
                                    Nullable.GetUnderlyingType(prop.PropertyType)
                                        ?? prop.PropertyType));
                            }
                            catch { /* 类型不匹配保持默认值 */ }
                        }
                        result.Add(item);
                    }
                    return result;
                }
            }
        }

        public static Task<List<T>> QueryRawAsync<T>(string sql, params object[] args)
            where T : new() =>
            Task.Run(() => QueryRaw<T>(sql, args));

        public static TResult ExecuteScalar<TResult>(string sql, params object[] args)
        {
            using (var conn = OpenConn())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                for (int i = 0; i < args.Length; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
                var raw = cmd.ExecuteScalar();
                if (raw == null || raw == DBNull.Value) return default;
                return (TResult)Convert.ChangeType(raw, typeof(TResult));
            }
        }

        public static void CreateTable(string createSql) => ExecuteNonQuery(createSql);

        public static bool TableExists(string tableName) =>
            ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@p0",
                tableName) > 0;

        // ── 工具 ─────────────────────────────────────────────────────────────

        private static bool IsEmpty(object val)
        {
            // 只过滤 null，不过滤空字符串和 0（这些可以是有效值）
            if (val == null) return true;
            if (val is string s) return s == null; // 只过滤 null 字符串，空字符串是有效值
            if (val is Guid g) return g == Guid.Empty;
            if (val is DateTime d) return d == DateTime.MinValue;
            return false;
        }
    }
}
