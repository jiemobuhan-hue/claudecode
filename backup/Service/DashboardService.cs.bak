using System;
using System.Linq;
using DevExpress.Mvvm;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.Messages;
using static ZenergyBFSI.Model.InspectionUtils;

namespace ZenergyBFSI.Service
{
    public sealed class DashboardService : IDisposable
    {
        private static DashboardService _instance;
        private static readonly object _syncRoot = new object();

        private DashboardWorker _worker;
        private DashboardSnapshot _currentSnapshot;
        private bool _disposed = false;

        public static DashboardService I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new DashboardService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DashboardService()
        {
            _currentSnapshot = DashboardSnapshot.Empty;
            _worker = new DashboardWorker();
            _worker.SnapshotReady += OnSnapshotReady;
            _worker.Start();
        }

        public DashboardSnapshot CurrentSnapshot
        {
            get { lock (_syncRoot) { return _currentSnapshot; } }
        }

        public DashboardData GetDashboardData()
        {
            lock (_syncRoot)
            {
                // Bug 6 修复：
                // 原实现未将 DashboardSnapshot.TotalCount / PageIndex 传入 DashboardData，
                // 导致前端无法得知数据库窗口内的真实总记录数，_totalPages 永远算出 1。
                // TotalCount = 数据库时间窗口内 COUNT(*) 结果（QueryAndParse 中已查询）
                // PageIndex  = Worker 当前页索引（分页翻页时由 SetPage() 更新）
                return new DashboardData
                {
                    Total = _currentSnapshot.Total,
                    Ok = _currentSnapshot.Ok,
                    Ng = _currentSnapshot.Ng,
                    YieldRate = _currentSnapshot.YieldRate,
                    Hourly = _currentSnapshot.Hourly.ToList(),
                    NgTypes = _currentSnapshot.NgTypes.ToList(),
                    Recent = _currentSnapshot.Recent.ToList(),
                    TotalCount = _currentSnapshot.TotalCount,   // ← 新增：数据库真实总数，用于分页计算
                    PageIndex = _currentSnapshot.PageIndex      // ← 新增：当前页索引，防止前端_currentPage被错误重置
                };
            }
        }

        public void SetPage(int pageIndex) { _worker?.SetPage(pageIndex); }
        public void RequestRefresh() { _worker?.RequestRefresh(); }
        public void Reset() { _worker?.RequestRefresh(); }

        private void OnSnapshotReady(object sender, DashboardSnapshot snapshot)
        {
            bool shouldNotify = false;
            lock (_syncRoot)
            {
                if (_currentSnapshot.SequenceNumber != snapshot.SequenceNumber)
                {
                    shouldNotify = true;
                    _currentSnapshot = snapshot;
                }
            }
            if (shouldNotify)
            {
                Messenger.Default.Send(new DashboardUpdateMessage(GetDashboardData()));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _worker?.Dispose();
            _worker = null;
        }
    }
}