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
                // Bug 6 �޸���
                // ԭʵ��δ�� DashboardSnapshot.TotalCount / PageIndex ���� DashboardData��
                // ����ǰ���޷���֪���ݿⴰ���ڵ���ʵ�ܼ�¼����_totalPages ��Զ��� 1��
                // TotalCount = ���ݿ�ʱ�䴰���� COUNT(*) �����QueryAndParse ���Ѳ�ѯ��
                // PageIndex  = Worker ��ǰҳ��������ҳ��ҳʱ�� SetPage() ���£�
                return new DashboardData
                {
                    Total = _currentSnapshot.Total,
                    Ok = _currentSnapshot.Ok,
                    Ng = _currentSnapshot.Ng,
                    YieldRate = _currentSnapshot.YieldRate,
                    Hourly = _currentSnapshot.Hourly.ToList(),
                    NgTypes = _currentSnapshot.NgTypes.ToList(),
                    Recent = _currentSnapshot.Recent.ToList(),
                    TotalCount = _currentSnapshot.TotalCount,   // �� ���������ݿ���ʵ���������ڷ�ҳ����
                    PageIndex = _currentSnapshot.PageIndex      // �� ��������ǰҳ��������ֹǰ��_currentPage����������
                };
            }
        }

        public void SetPage(int pageIndex) { _worker?.SetPage(pageIndex); }
        public void RequestRefresh() { _worker?.RequestRefresh(); }

        #region [TASK-REFACTOR-004] SetFilter — 班次+日期筛选转发 | 2026-05-15 | AI生成
        /// <summary>设置班次和日期筛选条件，触发 DashboardWorker 重新查询。</summary>
        public void SetFilter(string shift, DateTime? date = null)
        {
            _worker?.SetFilter(shift, date);
        }
        #endregion
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