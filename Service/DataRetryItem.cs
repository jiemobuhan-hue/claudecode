using System;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 远程库写入失败重试记录
    /// 当远程 SQL Server 写入超时或异常时，记录入 _retryBuffer
    /// 后台定时器每 30s 遍历重试，最多 10 次后标记 Failed 丢弃
    /// </summary>
    internal class DataRetryItem
    {
        /// <summary>目标库完整连接字符串</summary>
        public string TargetConnectionString { get; set; }

        /// <summary>目标服务器名称（用于日志）</summary>
        public string TargetServerName { get; set; }

        /// <summary>待写入的数据实体（T_BlueFilmDetection 或 T_BlueFilmDataMOM）</summary>
        public object Payload { get; set; }

        /// <summary>负载类型标识："Detection" 或 "MOM"</summary>
        public string PayloadType { get; set; }

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>首次失败时间</summary>
        public DateTime FirstFailTime { get; set; }

        /// <summary>最近一次失败时间</summary>
        public DateTime LastFailTime { get; set; }

        /// <summary>最近一次失败的错误信息</summary>
        public string LastErrorMessage { get; set; }
    }
}
