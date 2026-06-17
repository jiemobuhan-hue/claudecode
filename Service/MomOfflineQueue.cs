using RinKit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZenergyBFSI.Service
{
    internal class MomOfflineRecord
    {
        public long Id { get; set; }
        public string CommandId { get; set; }
        public string RequestJson { get; set; }
        public string CreatedAt { get; set; }
        public int RetryCount { get; set; }
        public string LastError { get; set; }
        public string Status { get; set; }
    }

    internal class MomOfflineQueue
    {
        private const int MaxQueueSize = 10000;
        private const int MaxQueueHardLimit = 50000;

        public async Task InitializeAsync()
        {
            await SQLiteGenericHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS MomOfflineQueue (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    CommandId   TEXT    NOT NULL,
                    RequestJson TEXT    NOT NULL,
                    CreatedAt   TEXT    NOT NULL,
                    RetryCount  INTEGER DEFAULT 0,
                    LastError   TEXT,
                    Status      TEXT    DEFAULT 'Pending'
                )").ConfigureAwait(false);
            var cutoff = DateTime.UtcNow.AddDays(-7).ToString("O");
            await SQLiteGenericHelper.ExecuteNonQueryAsync(
                "DELETE FROM MomOfflineQueue WHERE CreatedAt < @p0", cutoff).ConfigureAwait(false);
            Rlog.Debug("MomOfflineQueue 初始化完成");
        }

        public async Task EnqueueAsync(string commandId, string requestJson)
        {
            try
            {
                // 硬上限检查
                var count = SQLiteGenericHelper.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM MomOfflineQueue");
                if (count >= MaxQueueHardLimit)
                {
                    Rlog.Error($"MomOfflineQueue 达到硬上限 {MaxQueueHardLimit}，拒绝入队: {commandId}");
                    return;
                }
                // 软上限：清理最旧的 Completed/Failed 记录
                if (count >= MaxQueueSize)
                {
                    await SQLiteGenericHelper.ExecuteNonQueryAsync(
                        "DELETE FROM MomOfflineQueue WHERE Id IN (SELECT Id FROM MomOfflineQueue WHERE Status IN ('Completed','Failed') ORDER BY Id ASC LIMIT 1000)");
                }
                await SQLiteGenericHelper.ExecuteNonQueryAsync(
                    "INSERT INTO MomOfflineQueue (CommandId, RequestJson, CreatedAt, RetryCount, Status) VALUES (@p0, @p1, @p2, 0, 'Pending')",
                    commandId, requestJson, DateTime.UtcNow.ToString("O"));
                Rlog.Debug($"MomOfflineQueue 入队: {commandId} (当前{count + 1}条)");
            }
            catch (Exception ex)
            {
                Rlog.Error($"MomOfflineQueue 入队异常: {ex.Message}");
            }
        }

        public async Task<List<MomOfflineRecord>> DequeuePendingAsync(int maxCount = 50)
        {
            try
            {
                return await SQLiteGenericHelper.QueryRawAsync<MomOfflineRecord>(
                    $"SELECT * FROM MomOfflineQueue WHERE Status='Pending' ORDER BY Id ASC LIMIT {maxCount}");
            }
            catch (Exception ex)
            {
                Rlog.Error($"MomOfflineQueue 查询异常: {ex.Message}");
                return new List<MomOfflineRecord>();
            }
        }

        public async Task MarkCompletedAsync(long id)
        {
            await SQLiteGenericHelper.DeleteRowAsync("MomOfflineQueue", "Id", id);
        }

        public async Task MarkFailedAsync(long id, string error)
        {
            await SQLiteGenericHelper.ExecuteNonQueryAsync(
                "UPDATE MomOfflineQueue SET RetryCount = RetryCount + 1, LastError = @p0, Status = CASE WHEN RetryCount >= 9 THEN 'Failed' ELSE 'Pending' END WHERE Id = @p1",
                error, id);
        }

        public async Task<int> CleanupExpiredAsync(int retentionDays = 7)
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");
            var deleted = SQLiteGenericHelper.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM MomOfflineQueue WHERE CreatedAt < @p0", cutoff);
            if (deleted > 0)
            {
                await SQLiteGenericHelper.ExecuteNonQueryAsync(
                    "DELETE FROM MomOfflineQueue WHERE CreatedAt < @p0", cutoff);
                Rlog.Debug($"MomOfflineQueue TTL清理: {deleted}条记录(>{retentionDays}天)");
            }
            return (int)deleted;
        }
    }
}
