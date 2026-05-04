using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
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
                return new DashboardData
                {
                    Total = _currentSnapshot.Total,
                    Ok = _currentSnapshot.Ok,
                    Ng = _currentSnapshot.Ng,
                    YieldRate = _currentSnapshot.YieldRate,
                    Hourly = _currentSnapshot.Hourly.ToList(),
                    NgTypes = _currentSnapshot.NgTypes.ToList(),
                    Recent = _currentSnapshot.Recent.ToList()
                };
            }
        }

        public void SetPage(int pageIndex) { _worker?.SetPage(pageIndex); }
        public void RequestRefresh() { _worker?.RequestRefresh(); }
        public void Reset() { _worker?.RequestRefresh(); }

        /// <summary>
        /// 启动模拟模式（定时生成随机测试数据）
        /// </summary>
        /// <param name="intervalSeconds">模拟间隔（秒），默认3秒</param>
        public void StartSimulation(int intervalSeconds = 3) { _worker?.StartSimulation(intervalSeconds); }

        /// <summary>
        /// 停止模拟模式
        /// </summary>
        public void StopSimulation() { _worker?.StopSimulation(); }

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