using System;
using System.Threading.Tasks;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 异步读取请求 — 承载电芯码 + TaskCompletionSource 回调
    /// 自动机通过 EnqueueReadAsync 入队后，后台 ReadConsumer 查询完成时
    /// 通过 Completion.SetResult() 唤醒自动机线程
    /// </summary>
    internal class DataReadRequest
    {
        /// <summary>电芯条码，用于查询 3 个 SQL Server 视觉库</summary>
        public string CellCode { get; set; }

        /// <summary>通道编号 (1-4)，用于日志追踪</summary>
        public int ChannelNo { get; set; }

        /// <summary>任务完成源，后台消费者通过 SetResult 返回聚合后的 CellData</summary>
        public TaskCompletionSource<CellData> Completion { get; set; }

        /// <summary>入队时间戳，用于 10s 整体超时保护</summary>
        public DateTime EnqueueTime { get; set; }
    }
}
