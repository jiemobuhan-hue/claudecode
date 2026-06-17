using DevExpress.ClipboardSource.SpreadsheetML;
using DevExpress.Data.Extensions;
using DevExpress.Internal.WinApi.Windows.UI.Notifications;
using DevExpress.Mvvm.Native;
using DevExpress.XtraRichEdit.Import.Html;
using PLCHandler;
using PLCHandler.Control.View;
using PLCHandler.Models;
using RinKit;
using System;
using System.Collections;
using System.Collections.Generic; 
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using ViewModels;
using ZenergyBFSI.Service;
using ZenergyBFSI.View;
using static ZenergyBFSI.Model.AutoRun;

namespace ZenergyBFSI.Model
{
    public sealed class AutoRun
    {
        /// <summary>
        /// 电芯数据
        /// </summary>
        //public List<CellState> ListState { get; set; } = new List<CellState>();
        public List<CellData> ListData { get; set; } = new List<CellData>();
        public int Flag_Error { get; set; } = 0;
        public int Power { get; set; } = 0;
        public int LossCount { get; set; } = 0;//注液偏差计数
        public long TS { get; set; } = -1; 
        public InspectionInfo Inspection { get; set; } = null;

        private Task Task_Run;
        private static AutoRun _instance;
        private static readonly object _plcLock = new object();
        private static readonly object _momLock = new object();
        private static readonly object _listDataLock = new object();
        private volatile bool _running = false;
        private CancellationTokenSource _cts;

        /// <summary>
        /// 通道状态（用于边缘检测和防止重复执行）
        /// </summary>
        /// <summary>
        /// 全局心跳状态（所有工站共享）
        /// </summary>
        public enum GlobalHeartbeatState
        {
            Healthy,     // 心跳正常
            Lost,        // 心跳丢失（暂停所有工站）
            Recovering   // 心跳恢复中（等待确认稳定）
        }

        private GlobalHeartbeatState _heartbeatState = GlobalHeartbeatState.Healthy;
        private DateTime _heartbeatLostTime;         // 心跳丢失时刻
        private int _heartbeatRecoveringConfirmMs = 2000; // 心跳恢复确认时间（毫秒）
        private volatile bool _globalPaused = false; // 全局暂停标志（所有工站共享）


        private PlcMonitor _monitor;


        #region DeepSeek 并行化框架

        /// <summary>
        /// 工位状态枚举
        /// </summary>
        public enum StationState
        {
            Idle,       // 空闲
            Running,    // 运行中
            Paused,     // 暂停（心跳丢失）
            Error       // 错误
        }

        /// <summary>
        /// 自动机全局状态
        /// </summary>
        public enum AutomatonState
        {
            Stopped,
            Running,
            Error
        }

        /// <summary>
        /// 工位处理器接口 - 每个工位的业务逻辑必须实现此接口
        /// </summary>
        public interface IStationHandler
        {
            /// <summary>
            /// 心跳检测函数。返回 true 表示工位健康。
            /// 框架自动施加 1 秒超时。
            /// </summary>
            Task<bool> CheckHeartbeatAsync(CancellationToken token);

            /// <summary>
            /// 一次性信号判断函数。
            /// 返回 true 表示捕获到信号，框架随后会自动执行 ExecuteActionAsync。
            /// </summary>
            Task<bool> WaitForSignalAsync(CancellationToken token);

            /// <summary>
            /// 信号捕获后执行的动作。
            /// </summary>
            Task ExecuteActionAsync(CancellationToken token);
        }

        /// <summary>
        /// 工位状态机 - 独立运行的心跳→信号等待→动作执行循环
        /// </summary>
        public class Station
        {
            private readonly AutoRun _owner;
            private readonly IStationHandler _handler;
            private readonly Action<int, StationState>? _onStateChanged;
            private int _isRunning = 0;

            public int Id { get; }
            public StationState State { get; private set; } = StationState.Idle;
            public string Name { get; }

            public Station(AutoRun owner, int id, string name, IStationHandler handler, Action<int, StationState>? onStateChanged = null)
            {
                _owner = owner;
                Id = id;
                Name = name;
                _handler = handler;
                _onStateChanged = onStateChanged;
            }

            public async Task RunAsync(CancellationToken token)
            {
                if (Interlocked.Exchange(ref _isRunning, 1) == 1)
                    throw new InvalidOperationException($"工位 {Id}({Name}) 已在运行中");

                try
                {
                    SetState(StationState.Running);
                    await MonitoringLoopAsync(token);
                }
                finally
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                    if (State == StationState.Running)
                        SetState(StationState.Idle);
                }
            }

            private async Task MonitoringLoopAsync(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    // --- 检查全局暂停状态 ---
                    if (_owner._globalPaused)
                    {
                        SetState(StationState.Paused);
                        // 等待恢复（定期检查）
                        while (_owner._globalPaused && !token.IsCancellationRequested)
                        {
                            await Task.Delay(100, token);
                        }
                        if (token.IsCancellationRequested) break;
                        SetState(StationState.Running);
                    }

                    // --- 心跳检测（1秒超时）- 使用 Task.WhenAny 兼容 .NET Framework ---
                    // 注意：心跳检测现在主要通过全局状态管理，这里主要处理工位自身的瞬时故障
                    try
                    {
                        var heartbeatTask = _handler.CheckHeartbeatAsync(token);
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1), token);
                        var completedTask = await Task.WhenAny(heartbeatTask, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            // 心跳超时不报错，只记录日志并等待
                            SetState(StationState.Idle);
                            await Task.Delay(500, token);
                            continue;
                        }

                        bool heartbeatOk = heartbeatTask.Result;
                        if (!heartbeatOk)
                        {
                            // 心跳返回false不报错，暂停并等待
                            SetState(StationState.Paused);
                            await Task.Delay(500, token);
                            continue;
                        }
                    }
                    catch (TimeoutException)
                    {
                        SetState(StationState.Idle);
                        await Task.Delay(500, token);
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        SetState(StationState.Error);
                        Rlog.Error($"工位 {Id}({Name}) 心跳异常: {ex.Message}");
                        await Task.Delay(500, token);
                        continue;
                    }

                    // --- 一次性信号判断 ---
                    bool signalDetected;
                    try
                    {
                        signalDetected = await _handler.WaitForSignalAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        SetState(StationState.Error);
                        Rlog.Error($"工位 {Id}({Name}) 信号判断异常: {ex.Message}");
                        await Task.Delay(500, token);
                        continue;
                    }

                    // --- 执行动作（仅当有信号）---
                    if (signalDetected)
                    {
                        try
                        {
                            await _handler.ExecuteActionAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            SetState(StationState.Error);
                            Rlog.Error($"工位 {Id}({Name}) 执行动作异常: {ex.Message}");
                            await Task.Delay(500, token);
                            continue;
                        }
                    }
                    else
                    {
                        // 无信号时短暂休眠，避免CPU空转
                        await Task.Delay(Settings.自动机循环等待 > 0 ? Settings.自动机循环等待 : 50, token);
                    }
                }
            }

            private void SetState(StationState newState)
            {
                State = newState;
                _onStateChanged?.Invoke(Id, newState);
            }
        }

        #endregion

        #region 配置初始化需要的字段
        private const string CONNECTION_STRINGA = "Data Source=DESKTOP-0F9L4KO\\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

        private HarnessMeasureRepository _localHarnessMeasureRepositoryA;
        private BlueFilmDetectionRepository _localBlueFilmDetectionRepositoryA;
        

        #endregion


        public static AutoRun I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_plcLock)
                    {
                        if (_instance == null) _instance = new AutoRun();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public bool Init()
        {
            try
            {
                UC_Operation.I.WriteLog("自动机初始化...", "Debug");

                //初始化数据库脚本
                //TODO
                //在这里初始化所有的视觉工控机的SQLserver连接
                _localHarnessMeasureRepositoryA = new HarnessMeasureRepository(CONNECTION_STRINGA);
                _localBlueFilmDetectionRepositoryA = new BlueFilmDetectionRepository(CONNECTION_STRINGA);
                var x = DashboardService.I;
                //初始化工站数据
                //TODO
                var PLCs = UC_PLCMonitor.PLC.DataContext as PLCBoardViewModel;
                _monitor = PLCs._monitor;
                Rdb.SelectList(out List<CellData> listData, "SELECT * from CellData ");
                ListData = listData;
                //foreach (var cell in listData)
                //{
                //    Rdb.SelectList(out List<CellState> list, $"SELECT * from CellState Where 电芯码 ='{cell.电芯码}' and 离开=0 ORDER BY Step Desc LIMIT 1");
                //    ListState.Add(list[0]);
                //}
                _running = true;
                _cts = new CancellationTokenSource();
                InspectionInit();
                Task.Run(() => Thread_Run());
                //ResetIO();
                UC_Operation.I.WriteLog($"自动机初始化成功", "Info");
                return true;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"自动机初始化异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
            return false;
        }

        /// <summary>
        /// 自动机运行逻辑 - 基于 DeepSeek 独立工位框架
        /// 每个通道的来料和分流工位独立运行，互不阻塞
        /// 心跳由主循环统一检测，所有工站共享心跳状态
        /// </summary>
        private async Task Thread_Run()
        {
            // 创建 8 个工位 Station (4通道 × 2工位)
            var stations = new List<Station>();
            for (int i = 1; i <= 4; i++)
            {
                // 来料工位
                var arriveHandler = new ProductArriveStationHandler(this, i);
                stations.Add(new Station(this, i, $"来料{i}", arriveHandler, OnStationStateChanged));

                // 分流工位
                var leadHandler = new ProductLeadStationHandler(this, i);
                stations.Add(new Station(this, i + 4, $"分流{i}", leadHandler, OnStationStateChanged));
            }

            // 使用 CancellationToken 启动所有工位
            _cts = new CancellationTokenSource();
            var stationTasks = stations.Select(s => Task.Run(() => s.RunAsync(_cts.Token), _cts.Token)).ToArray();

            UC_Operation.I.WriteLog($"自动机启动，8个工位独立运行", "Info");

            // 主循环：统一心跳检测（所有工站共享）
            while (_running && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // 心跳检测
                    bool heartbeatOk = CheckGlobalHeartbeat();

                    if (!heartbeatOk)
                    {
                        // 心跳丢失，暂停所有工站
                        _globalPaused = true;
                    }
                    else
                    {
                        // 心跳正常，恢复所有工站
                        _globalPaused = false;
                    }
                }
                catch (Exception ex)
                {
                    Rlog.Error($"心跳检测异常: {ex.Message}");
                }

                // 心跳检测间隔
                await Task.Delay(Settings.自动机循环等待 > 0 ? Settings.自动机循环等待 : 100, _cts.Token);
            }

            // 等待所有工位完成（正常停止或异常）
            try
            {
                await Task.WhenAll(stationTasks);
            }
            catch (OperationCanceledException)
            {
                UC_Operation.I.WriteLog($"自动机被取消", "Info");
            }
            catch (Exception ex)
            {
                Rlog.Error($"自动机异常: {ex.Message}");
            }

            UC_Operation.I.WriteLog($"自动机已停止", "Info");
        }

        /// <summary>
        /// 工位状态变化回调
        /// </summary>
        private void OnStationStateChanged(int stationId, StationState newState)
        {
            UC_Operation.I.WriteLog($"工位{stationId}状态 → {newState}", "Debug");
        }

        /// <summary>
        /// 全局心跳检测（由主循环执行）
        /// 所有工站共享此状态，心跳丢失时暂停所有工站
        /// </summary>
        /// <returns>设备是否在线</returns>
        private bool CheckGlobalHeartbeat()
        {
            bool deviceOk = DeviceLink();

            if (deviceOk)
            {
                if (_heartbeatState == GlobalHeartbeatState.Lost)
                {
                    _heartbeatState = GlobalHeartbeatState.Recovering;
                    UC_Operation.I.WriteLog($"心跳恢复，进入恢复确认状态", "Info");
                }
                else if (_heartbeatState == GlobalHeartbeatState.Recovering)
                {
                    // 检查恢复确认时间
                    if ((DateTime.Now - _heartbeatLostTime).TotalMilliseconds >= _heartbeatRecoveringConfirmMs)
                    {
                        _heartbeatState = GlobalHeartbeatState.Healthy;
                        UC_Operation.I.WriteLog($"心跳确认稳定，正常运行", "Info");
                    }
                }
                else
                {
                    _heartbeatState = GlobalHeartbeatState.Healthy;
                }
            }
            else
            {
                if (_heartbeatState == GlobalHeartbeatState.Healthy ||
                    _heartbeatState == GlobalHeartbeatState.Recovering)
                {
                    _heartbeatState = GlobalHeartbeatState.Lost;
                    _heartbeatLostTime = DateTime.Now;
                    UC_Operation.I.WriteLog($"心跳丢失，暂停所有工站", "Warn");
                }
            }

            return deviceOk;
        }

        /// <summary>
        /// 停止自动机
        /// </summary>
        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
        }
        private void InspectionInit()
        {
            //判断班次
            var date = DateTime.Now.Hour >= 8 ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 8, 0, 0) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day - 1, 8, 0, 0);
            var start = DataHelper.DateToTimeStamp(date, 86400);
            var end = DataHelper.DateToTimeStamp(date.AddDays(1), 86400);
            Rdb.SelectList(out List<InspectionInfo> list, $"SELECT * from InspectionInfo WHERE TimeStamp > {start} and TimeStamp <{end}");
            var InspectionData = list.FirstOrDefault();
            if (InspectionData != null)
            {
                Inspection = InspectionData;
            }
        }
        /// <summary>
        /// 设备链接状态检测 — 使用 PLCHandler 辅助工程
        /// 检查主 PLC (omron_1) 的连接状态，断联时只报警不影响其它流程
        /// </summary>
        int heartloop = 0;
        private bool DeviceLink()
        {
            if (_monitor != null && _monitor.IsConnected("omron_1") && _monitor.IsConnected("omron_2"))
            {
                if(heartloop == 0)
                {
                    SetInt_Plc($"PLC心跳响应", 0);
                    SetInt_Plc($"出站心跳", 0);
                    Thread.Sleep(1000);
                    heartloop = 1;
                }
                else
                {
                    SetInt_Plc($"PLC心跳响应", 1);
                    SetInt_Plc($"出站心跳", 1);
                    Thread.Sleep(1000);
                    heartloop = 0;
                }
                
      
                Main.uC_StatesBar.uC_StatesBarVM.IsMomConnected = true;
                Main.uC_StatesBar.uC_StatesBarVM.PlcStatusColor = Brushes.LimeGreen;
                return true;
            }
            else
            {
                Main.uC_StatesBar.uC_StatesBarVM.IsMomConnected = false;
                Main.uC_StatesBar.uC_StatesBarVM.PlcStatusColor = Brushes.Red;
                return false;
            }
        }
        /// <summary>
        /// 自动机动作分解
        /// 状态机动作
        /// </summary>
        #region Action

        ///
        private void test()
        {

        } 
        /// <summary>
        /// 传入蓝膜检测记录实体并返回已经检测过的信息short数组
        /// </summary>
        /// <param name="t_BlueFilmDetection"></param>
        /// <returns></returns>
        private short[] GetReloadres(T_BlueFilmDetection t_BlueFilmDetection)
        {
            short[] res = { 1, 2, 3, 4, 5, 6, 7, 8, 666, 0, 0, 0, 0, 0, 0 };

            //根据里面的属性string分割检测的字段的内容来确认检测过的部位
            //根据DetectionArea内容来确认所有的检测部位，填充数组的方法由视觉决定
            var location = t_BlueFilmDetection.DetectionArea.ToString().Split('|');
            foreach (var item in location) { }

            return res;

        }

        /// <summary>
        /// 来料查询结束后plc给到收到信号
        /// </summary>
        /// <param name="v"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ProductLeave(int no)
        {
            if (GetIO($"来料工位{no}离开"))
            {
                string code = "";
                //获取码
                code= GetCodeFromTunnal(no);
                //存储数据库
                //更新其它数据项
                Rdb.QueueIn($"UPDATE CellData SET 进站时间 = '{DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss")}' WHERE 电芯码 ='{code}'");
                var data = ListData.Find(i => i.电芯码 == code);
                data.进站时间 = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss");
                saveDB(data);
                ListData.RemoveAll(i => i.电芯码 == data.电芯码);
                SetIO($"来料工位{no}离开应答", false);
            }
            else
            {
                SetIO($"来料工位{no}离开应答", false);
            } 
        }
        private void saveDB(CellData data)
        {
            Rdb.QueueIn($"UPDATE CellData SET 进站时间 = '{data.进站时间}' WHERE 电芯码 ='{data.电芯码}'");

        }

        /// <summary>
        /// 视觉检测出站时给PLC应答信号、并存储本地数据渲染数据
        /// </summary>
        /// <param name="no"></param>
        private void ProductLeadArrive(int no)
        {
            //if (GetIO($"视觉检测工位{no}出站到位"))
            //DashboardService.I.RecordExit("12138", "OK", "ngTypes1");

            if (GetInt($"PLC通道{no}分流触发")==1||true)
            {
                var tempcode = "12138";
                //这里有个问题，如果PLC不存二维码这里无法获取有效二维码，存在绑错二维码的风险  
                tempcode = this._getCodeFromTunnal(no);
                List<CellData> t;
                //Rdb.SelectList(out List<CellData> t,$"SELECT * from CellData WHERE 电芯码 = {tempcode}");
                // 给数据库查询也加上锁，防止与自动机其他逻辑冲突
                tempcode = "12138";
                lock (_listDataLock)
                {
                    t = SQLiteGenericHelper.QueryRaw<CellData>($"SELECT * from CellData WHERE 电芯码 = {tempcode}", "CellData");
                }
                //List<CellData> t= SQLiteGenericHelper.QueryRaw<CellData>($"SELECT * from CellData WHERE 电芯码 = {tempcode}", "CellData");
                var data = t.FirstOrDefault(i => i.电芯码 == tempcode);
                //查询MYSQL数据库并赋值给该物料

                if(data is not null)
                {
                    lock (_listDataLock)
                    {
                        UpdateCellDataFromSQLserver(ref data);
                    }
                }
              
                //TODO



                //视觉排序算法
                int way = getlead(data);
                way = 1 ;
                switch (way)
                {
                    //TODO
                    case 1:
                        data.视觉检测结果 = "结果一"; break;
                    case 2:
                        data.视觉检测结果 = "结果二"; break;
                    case 3:
                        data.视觉检测结果 = "结果三"; break;
                    case 4:
                        data.视觉检测结果 = "结果四"; break;
                } 
                SetInt($"PLC通道{no}分流通道结果", way);
                #region 更新检测结果的数据库表
                List<CellData> temp = new List<CellData>();
                temp.Add(data);
                lock (_listDataLock)
                {
                    SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");
                }
                #endregion

                // 记录出站统计（看板数据更新）
                //string ngTypes = string.Join("|", new[] { data.Ng类型1, data.Ng类型2, data.Ng类型3, data.Ng类型4, data.Ng类型5, data.Ng类型6, data.Ng类型7, data.Ng类型8 }
                //    .Where(s => !string.IsNullOrEmpty(s)));
                string ngTypes = string.Join("|", new[] { 
                    "data.Ng类型1",
                    "data.Ng类型2", 
                    "data.Ng类型3",
                    "data.Ng类型4", 
                    "data.Ng类型5", 
                    "data.Ng类型6",
                    "data.Ng类型7", 
                    "data.Ng类型8" }
                    .Where(s => !string.IsNullOrEmpty(s)));
                //DashboardService.I.RecordExit(tempcode, "NG", ngTypes);
                //DashboardService.I.RecordExit(tempcode, data.视觉检测结果, ngTypes);
            }
            else
            {
                SetInt($"PLC通道{no}分流通道结果", 0);
            }
        }
        /// <summary>
        /// 这里处理SQLServer查询后更新物料信息
        /// 只查询关于检测到的所有NG
        /// </summary>
        /// <param name="data"></param>
        private void UpdateCellDataFromSQLserver(ref CellData data)
        {
            //此处处理视觉信息的装填  这里不负责具体的内容，因此存储信息的时候装入
            List<T_BlueFilmDetection> temp = new List<T_BlueFilmDetection>() ;
            //查询所有的三个工控机的所有信息
            //A工控机
            foreach (var item in _localBlueFilmDetectionRepositoryA.GetByCellCode(data.电芯码))
            {
                temp.Add(item);
            }
            //判断是否合法的数据，不合法就填入默认查询失败的数据
            if (temp[0].CellCode.Length > 5)
            {
                //data.视觉检测参数一 ="描述一:"+ temp.Width1.ToString();
                //data.视觉检测参数二 = "描述二:"+   temp.Width2.ToString();
                //data.视觉检测参数三 = "描述三:"+   temp.Width3.ToString();
                //data.视觉检测参数四 = "描述四:"+   temp.Width4.ToString();
                //data.视觉检测参数五 = "描述五:"+   temp.Width5.ToString();
                //data.视觉检测参数六 = "描述六:"+   temp.Width6.ToString();
                //data.视觉检测状态 =   "视觉检测状态:" +   temp.MarkNumber.ToString();
                //data.视觉检测结果 =   "结果:" +   temp.Result.ToString();
            }
 
            data.视觉检测参数一 = "视觉检测参数SQLServer";
            data.视觉检测参数二 = "视觉检测参数SQLServer";
            data.视觉检测参数三 = "视觉检测参数SQLServer";
            data.视觉检测参数四 = "视觉检测参数SQLServer";
            data.视觉检测参数五 = "视觉检测参数SQLServer";
            data.视觉检测参数六 = "视觉检测参数SQLServer";
            data.视觉检测状态 = "视觉检测参数SQLServer";
            data.Ng类型数量 = 0;
            //data.视觉检测结果 = temp.DetectionResults.ToString();  
            // TODO: 临时写死为NG以便测试饼图，正式应接真实检测结果
            //data.Ng类型数量 = int.Parse(temp.NGtypeNum.ToString());
            // TODO: 以下临时数据用于测试饼图显示，正式应接真实缺陷类型字段
            // 真实来源例如: temp.DefectType 或 temp.NgReason 等字段
            data.出站结果 = "OK";
            for (int i=0;i< temp.Count; i++)
            {
                switch (i)
                {
                    #region NG类型匹配 
                    case 0:
                        if (temp[0].DetectionResults != "OK")
                        {
                            data.Ng类型1 = temp[0].DetectionArea+ $"外观缺陷{temp[0].NGtype1}{temp[0].NGtype2}{temp[0].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break;
                        }
                        else break;
                    case 1:
                        if (temp[1].DetectionResults != "OK")
                        {
                            data.Ng类型2 = temp[1].DetectionArea + $"外观缺陷{temp[1].NGtype1}{temp[1].NGtype2}{temp[1].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break;
                        }
                        else break;
                    case 2:
                        if (temp[2].DetectionResults != "OK")
                        {
                            data.Ng类型3 = temp[2].DetectionArea + $"外观缺陷{temp[2].NGtype1}{temp[2].NGtype2}{temp[2].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break;
                        }
                        else break;
                    case 3:
                        if (temp[3].DetectionResults != "OK")
                        {
                            data.Ng类型4 = temp[3].DetectionArea + $"外观缺陷{temp[3].NGtype1}{temp[3].NGtype2}{temp[3].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break;
                        }
                        else break;
                    case 4:
                        if (temp[4].DetectionResults != "OK")
                        {
                            data.Ng类型5 = temp[4].DetectionArea + $"外观缺陷{temp[4].NGtype1}{temp[4].NGtype2}{temp[4].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break;
                        }
                        else break;
                    case 5:
                        if (temp[1].DetectionResults != "OK")
                        {
                            data.Ng类型6 = temp[5].DetectionArea + $"外观缺陷{temp[5].NGtype1}{temp[5].NGtype2}{temp[5].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG"; 
                            break;
                        }
                        else break;
                    case 6:
                        if (temp[1].DetectionResults != "OK")
                        {
                            data.Ng类型7 = temp[6].DetectionArea + $"外观缺陷{temp[6].NGtype1}{temp[6].NGtype2}{temp[6].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break; 
                        }
                        else break;
                    case 7:
                        if (temp[1].DetectionResults != "OK")
                        {
                            data.Ng类型8 = temp[7].DetectionArea + $"外观缺陷{temp[7].NGtype1}{temp[7].NGtype2}{temp[7].NGtype3}";
                            data.Ng类型数量++;
                            data.出站结果 = "NG";
                            break; 
                        }
                        else break;
                    #endregion
                }
            }
            //data.Ng类型1 = "外观缺陷";
            //data.Ng类型2 = "尺寸超差";
            //data.Ng类型3 = "性能不合格";
            //data.Ng类型4 = "";  // TODO: 接真实缺陷类型4
            //data.Ng类型5 = "";  // TODO: 接真实缺陷类型5
            //data.Ng类型6 = "";  // TODO: 接真实缺陷类型6
            //data.Ng类型7 = "";  // TODO: 接真实缺陷类型7
            //data.Ng类型8 = "";  // TODO: 接真实缺陷类型8
            //data.出站结果 = "NG";
            //data.视觉检测结果 = "视觉检测结果SQLServer";
        }

        private int getlead(CellData data)
        {
            //视觉检测核心算法
            //


            return 0;
        }
        /// <summary>
        /// 获取来料的电芯码
        /// </summary>
        /// <param name="no"></param>
        /// <returns></returns>
        private string GetCodeFromTunnal(int no)
        {
            string code = null;
            //预留出PLC处理逻辑，现在来料存在复投来料
            switch (no)
            {
                case 1:
                    code = GetString($"PLC通道{no}来料电芯码").Trim('\0');
                    //code = GetString($"扫码工位{no}二维码");
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码成功", "Success");
                    }
                    break;
                case 2:
                    code = GetString($"扫码工位{no}二维码").Trim('\0');
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码成功", "Success");
                    }
                    break;
                case 3:
                    code = GetString($"扫码工位{no}二维码").Trim('\0');
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码成功", "Success");
                    }
                    break;
                case 4:
                    code = GetString($"扫码工位{no}二维码").Trim('\0');
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"扫码工位{no}获取产品电芯码成功", "Success");
                    }
                    break;

            }
            return code;
        }

        private string _getCodeFromTunnal(int no)
        {
            string code = null;
            //预留出PLC处理逻辑，现在来料存在复投来料
            switch (no)
            {
                case 1:
                    code = GetString($"PLC通道{no}分流电芯码");
                    //code = GetString($"扫码工位{no}二维码");
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码成功", "Success");
                    }
                    break;
                case 2:
                    code = GetString($"PLC通道{no}分流电芯码").Trim('\0');
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码成功", "Success");
                    }
                    break; 
                case 3:
                    code = GetString($"PLC通道{no}分流电芯码");
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码成功", "Success");
                    }
                    break;
                case 4:
                    code = GetString($"PLC通道{no}分流电芯码");
                    if (string.IsNullOrEmpty(code))
                    {
                        //现在只判断电芯码是否存在，不判断有效性
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码异常", "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"分流工位{no}获取产品电芯码成功", "Success");
                    }
                    break;

            }
            return code;
        }
        /// <summary>
        /// 引流结束给到PLC的结束交互信号
        /// </summary>
        /// <param name="v"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ProductLeadLeave(int no)
        { 
            if (GetIO($"视觉检测工位{no}出站离开"))
            {
                string code = "";
                //这里有个问题，如果PLC不存二维码这里无法获取有效二维码，存在绑错二维码的风险
                code = GetCodeFromTunnal(no);
               Rdb.SelectList(out List<CellData> t, $"SELECT * from CellData WHERE 电芯码 = '{code}'");
                var data = t.FirstOrDefault(i => i.电芯码 == code);
                //本地存储操作视觉检测操作数据

                saveDB(data);
                //TODO

                ListData.RemoveAll(i => i.电芯码 == data.电芯码);

                SetIO($"视觉检测工位{no}出站离开应答", true);
            }
            else
            {
                SetIO($"视觉检测工位{no}出站离开应答", false);
            }
        } 
        /// <summary>
        /// 检查码是否合法
        /// </summary>
        /// <param name="code"></param>
        /// <returns>
        /// true表示合法、false表示不合法
        /// </returns>
        bool LegalCode(string code)
        {
            return !string.IsNullOrEmpty(code);
        }

        /// <summary>
        /// short[] → byte[] 转换，用于 PLCHandler Write（逐元素小端序）
        /// </summary>
        private static byte[] ShortArrayToBytes(int[] values)
        {
            var bytes = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                var pair = BitConverter.GetBytes(values[i]);
                bytes[i * 2] = pair[0];
                bytes[i * 2 + 1] = pair[1];
            }
            return bytes;
        }

        /// <summary>
        /// 设备PLC交互心跳
        /// </summary>
        private void HeartBeat()
        {
            if (GetInt("PLC心跳获取")==1)
            {
                SetInt("PLC心跳响应", 1); 
            }
            else
            {
                SetInt("PLC心跳响应", 0);
            }
        }

        private void Replenish()
        {
         
        }

        private void ClearALL()
        {
 
        }

        private void StationInit(string station)
        {
            if (GetIO($"{station}初始化"))
            {
                bool flag = true;
 
                if (flag)
                {
                    if (ClearCache())
                        SetIO($"{station}初始化应答", true);
                }
            }
            else
            {
                SetIO($"{station}初始化应答", false);
            }
        }
  
        #endregion
        #region Method
        public void Alarm(string msg, int stop)
        {
            if (msg.Length > 50) { msg = msg.Substring(0, 50); }
        
        }  
        private bool ClearCache()
        {
            bool res = true;
            UC_Operation.I.WriteLog($"清理缓存", "Info");
            for (int i = 0; i < ListData.Count; i++)
            {
                
            }
            return res;
        }
        /// <summary>
        /// 出站上传
        /// </summary>
        public void UpMOM()
        {
            for (int no = 1; no <= 4; no++)
            {
                
            }
            ClearCache();
        }  
        public void ResetPcToPlc()
        {
            return;

            for (int i = 1; i <= 4; i++)
            {
                if (GetIO($"扫码工位{i}离开"))
                {
                    SetIO($"扫码工位{i}离开应答", true);
                }
                else
                {
                    SetIO($"扫码工位{i}离开应答", false);
                }

                if (GetIO($"拔钉工位{i}到位"))
                {
                    SetIO($"拔钉工位{i}到位应答", true);
                }
                else
                {
                    SetIO($"拔钉工位{i}到位应答", false);
                }

                if (GetIO($"前称重{i}到位"))
                {
                    SetIO($"前称重{i}到位应答", true);
                }
                else
                {
                    SetIO($"前称重{i}到位应答", false);
                }

                if (GetIO($"注液上料机械手夹爪{i}到位"))
                {
                    SetIO($"注液上料机械手夹爪{i}到位应答", true);
                }
                else
                {
                    SetIO($"注液上料机械手夹爪{i}到位应答", false);
                }

                if (GetIO($"注液下料机械手夹爪{i}到位"))
                {
                    SetIO($"注液下料机械手夹爪{i}到位应答", true);
                }
                else
                {
                    SetIO($"注液下料机械手夹爪{i}到位应答", false);
                }

                if (GetIO($"后称重{i}到位"))
                {
                    SetIO($"后称重{i}到位应答", true);
                }
                else
                {
                    SetIO($"后称重{i}到位应答", false);
                }

                if (GetIO($"检测{i}到位"))
                {
                    SetIO($"检测{i}到位应答", true);
                }
                else
                {
                    SetIO($"检测{i}到位应答", false);
                }

                if (GetIO($"检测{i}离开"))
                {
                    SetIO($"检测{i}离开应答", true);
                }
                else
                {
                    SetIO($"检测{i}离开应答", false);
                }

                SetInt($"扫码结果{i}", 0);
                SetInt($"拔钉结果{i}", 0);
                SetInt($"前称重结果{i}", 0);
                SetInt($"后称重结果{i}", 0);
                SetInt($"真空检测结果{i}", 0);
                SetInt($"胶钉检测结果{i}", 0);
            }

            for (int i = 1; i < 7; i++)
            {
                for (int j = 1; j < 9; j++)
                {
                    if (GetIO($"注液{i}通道{j}启动"))
                    {
                        SetIO($"注液{i}通道{j}启动应答", true);
                    }
                    else
                    {
                        SetIO($"注液{i}通道{j}启动应答", false);
                    }
                }
            }

        }

        /// <summary>读取 PLC 布尔信号（从缓存），UShort 值 1=true, 0=false</summary>
        public bool GetIO(string name)
        {
            if (_monitor != null && _monitor.TryGetLatestByName(name, out var r) && r.IsOk)
                return Convert.ToInt32(r.Value) == 1;
            UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
            Flag_Error++;
            return false;
        }

        /// <summary>读取 PLC 浮点信号（从缓存）</summary>
        public float GetFloat(string name)
        {
            if (_monitor != null && _monitor.TryGetLatestByName(name, out var r) && r.IsOk)
                return Convert.ToSingle(r.Value);
            UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
            Flag_Error++;
            return 0;
        }

        /// <summary>读取 PLC 整数信号（从缓存）</summary>
        public int GetInt(string name)
        {
            if (_monitor != null && _monitor.TryGetLatestByName(name, out var r) && r.IsOk)
                return Convert.ToInt32(r.Value);
            UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
            Flag_Error++;
            return 0;
        }

        /// <summary>读取 PLC 字符串信号（从缓存）</summary>
        public string GetString(string name)
        {
            if (_monitor != null && _monitor.TryGetLatestByName(name, out var r) && r.IsOk)
                return r.Value?.ToString() ?? "";
            UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
            Flag_Error++;
            return null;
        }

        /// <summary>信号同步状态 — PLCHandler 缓存始终同步，始终返回 true</summary>
        public bool Sync(string name)
        {
            if (_monitor != null && _monitor.TryGetLatestByName(name, out var r) && r.IsOk)
                return true;
            UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
            Flag_Error++;
            return false;
        }
        /// <summary>向 PLC 写入整数（UShort），使用 PLCHandler WriteByNameAsync</summary>
        public bool SetInt(string name, int value)
        {
            if (_monitor == null || !_monitor.TryGetSignalByName(name, out _))
            {
                UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
                Flag_Error++;
                return false;
            }
            try
            {
                var data = BitConverter.GetBytes((ushort)value);
                var result = _monitor.WriteByNameAsync(name, data).GetAwaiter().GetResult();
                if (result.IsOk) { UC_Operation.I.WriteLog($"{name}=>{value}", "Info"); return true; }
                UC_Operation.I.WriteLog($"写入PLC失败 {name}: {result.Error}", "Error");
                return false;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"写入PLC异常 {name}: {ex.Message}", "Error");
                Flag_Error++;
                return false;
            }
        }

        /// <summary>向 PLC 写入字符串，使用 PLCHandler WriteByNameAsync</summary>
        public bool SetString(string name, string value)
        {
            if (_monitor == null || !_monitor.TryGetSignalByName(name, out _))
            {
                UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
                Flag_Error++;
                return false;
            }
            try
            {
                var data = System.Text.Encoding.ASCII.GetBytes(value ?? "");
                var result = _monitor.WriteByNameAsync(name, data).GetAwaiter().GetResult();
                if (result.IsOk) { UC_Operation.I.WriteLog($"{name}=>{value}", "Info"); return true; }
                UC_Operation.I.WriteLog($"写入PLC失败 {name}: {result.Error}", "Error");
                return false;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"写入PLC异常 {name}: {ex.Message}", "Error");
                Flag_Error++;
                return false;
            }
        }

        /// <summary>向 PLC 写入布尔值（0/1 字节），使用 PLCHandler WriteByNameAsync</summary>
        public bool SetIO(string name, bool value)
        {
            if (_monitor == null || !_monitor.TryGetSignalByName(name, out _))
            {
                UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
                Flag_Error++;
                return false;
            }
            try
            {
                var data = new byte[] { (byte)(value ? 1 : 0) };
                var result = _monitor.WriteByNameAsync(name, data).GetAwaiter().GetResult();
                if (result.IsOk)
                {
                    if (name == "心跳应答") { UC_Operation.I.WriteLog($"{name}=>{value}"); TS = DataHelper.TimeMS; }
                    else UC_Operation.I.WriteLog($"{name}=>{value}", "Debug");
                    return true;
                }
                UC_Operation.I.WriteLog($"写入PLC失败 {name}: {result.Error}", "Error");
                return false;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"写入PLC异常 {name}: {ex.Message}", "Error");
                Flag_Error++;
                return false;
            }
        }
        #endregion
        #region Thread-Safe PLC/MOM Wrappers (PLCHandler)

        /// <summary>
        /// PLC 整数直接读取 — 不走缓存，PLCHandler 内部线程安全
        /// </summary>
        private async Task<int> GetInt_Plc(string name, int timeoutMs = 3000)
        {
            if (_monitor == null) return 0;
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var result = await _monitor.ReadOnceByNameAsync(name);
                if (result.IsOk)
                    return Convert.ToInt32(result.Value);
                Rlog.Error($"PLC读取失败 [{name}]: {result.Error}");
                Flag_Error++;
                return -1;
            }
            catch (OperationCanceledException)
            {
                Rlog.Error($"PLC读取超时 [{name}]");
                Flag_Error++;
                return -1;
            }
            catch (Exception ex)
            {
                Rlog.Error($"PLC读取异常 [{name}]: {ex.Message}");
                Flag_Error++;
                return -1;
            }
        }

        /// <summary>
        /// PLC 字符串直接读取 — 不走缓存，线程安全
        /// </summary>
        private async Task<string> GetString_Plc(string name)
        {
            if (_monitor == null) return null;
            try
            {
                var result = await _monitor.ReadOnceByNameAsync(name);
                if (result.IsOk)
                    return result.Value?.ToString() ?? "";
                Rlog.Error($"PLC字符串读取失败 [{name}]: {result.Error}");
                Flag_Error++;
                return "";
            }
            catch (Exception ex)
            {
                Rlog.Error($"PLC字符串读取异常 [{name}]: {ex.Message}");
                Flag_Error++;
                return "";
            }
        }

        /// <summary>
        /// PLC 整数写入（UShort），使用 PLCHandler WriteByNameAsync，线程安全
        /// </summary>
        private async Task<bool> SetInt_Plc(string name, int value)
        {
            if (_monitor == null || !_monitor.TryGetSignalByName(name, out _))
            {
                UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
                Flag_Error++;
                return false;
            }
            try
            {
                var data = BitConverter.GetBytes((int)value);
                var result = await _monitor.WriteByNameAsync(name, value);
                if (result.IsOk) { UC_Operation.I.WriteLog($"{name}=>{value}", "Info"); return true; }
                UC_Operation.I.WriteLog($"写入PLC失败 [{name}]: {result.Error}", "Error");
                return false;
            }
            catch (Exception ex)
            {
                Rlog.Error($"PLC写入异常 [{name}]: {ex.Message}");
                Flag_Error++;
                return false;
            }
        }

        /// <summary>
        /// PLC 布尔写入（0/1 字节），使用 PLCHandler WriteByNameAsync，线程安全
        /// </summary>
        private async Task<bool> SetIO_Plc(string name, bool value)
        {
            if (_monitor == null || !_monitor.TryGetSignalByName(name, out _))
            {
                UC_Operation.I.WriteLog($"无法找到信号.{name}", "Error");
                Flag_Error++;
                return false;
            }
            try
            {
                var data = new byte[] { (byte)(value ? 1 : 0) };
                var result = await _monitor.WriteByNameAsync(name, data);
                if (result.IsOk)
                {
                    if (name == "心跳应答") { UC_Operation.I.WriteLog($"{name}=>{value}"); TS = DataHelper.TimeMS; }
                    else UC_Operation.I.WriteLog($"{name}=>{value}", "Debug");
                    return true;
                }
                UC_Operation.I.WriteLog($"写入PLC失败 [{name}]: {result.Error}", "Error");
                return false;
            }
            catch (Exception ex)
            {
                Rlog.Error($"PLC bool写入异常 [{name}]: {ex.Message}");
                Flag_Error++;
                return false;
            }
        }

        /// <summary>
        /// 线程安全MOM查询
        /// </summary>
        private dynamic MomCheckIn_Safe(string code)
        {
            lock (_momLock)
            {
                try
                {
                    return MomHandler.I.MomCheckIn(code);
                }
                catch (Exception ex)
                {
                    Rlog.Error($"MOM查询异常 [{code}]: {ex.Message}");
                    Flag_Error++;
                    return null;
                }
            }
        }

        #endregion

        #region 工位处理器实现 (基于 DeepSeek 框架)

        /// <summary>
        /// 通道状态（用于边缘检测和防止重复执行）- 每个通道独立的来料和分流状态
        /// </summary>
        private class ChannelEdgeState
        {
            public int TriggerLast { get; set; } = 0;      // 上次触发值
            public bool Processing { get; set; } = false; // 是否正在处理中
            public string LastCode { get; set; } = "";     // 上次处理的电芯码
            public long LastProcessTime { get; set; } = 0;  // 上次处理时间戳
        }

        /// <summary>
        /// 来料工位处理器 - 通道编号 1~4
        /// </summary>
        private class ProductArriveStationHandler : IStationHandler
        {
            private readonly AutoRun _owner;
            private readonly int _channelNo;
            private readonly ChannelEdgeState _state = new ChannelEdgeState();

            public ProductArriveStationHandler(AutoRun owner, int channelNo)
            {
                _owner = owner;
                _channelNo = channelNo;
            }

            public async Task<bool> CheckHeartbeatAsync(CancellationToken token)
            {
                // 心跳已由主循环统一管理，此处只返回true
                // 全局暂停由 Station.MonitoringLoopAsync 处理
                await Task.CompletedTask;
                return true;
            }

            public async Task<bool> WaitForSignalAsync(CancellationToken token)
            {
                var trigger = await _owner.GetInt_Plc($"PLC通道{_channelNo}来料触发");

 


                if (trigger <= 0) 
                { 
                    _state.Processing = false;
                    await _owner.SetInt_Plc($"PLC通道{_channelNo}来料结果",0);
                    this._state.TriggerLast = trigger;
                    return false;
                }
                if (trigger == _state.TriggerLast)
                {
                    _state.TriggerLast = trigger;
                    return false;
                }
                  

                _state.TriggerLast = trigger;

                // 状态锁存：防止重复执行
                if (_state.Processing) return false;
                _state.Processing = true;

                return trigger == 1;
            }

            public async Task ExecuteActionAsync(CancellationToken token)
            {
                try
                {
                    var codeRaw = await _owner.GetString_Plc($"PLC通道{_channelNo}来料电芯码");
                    string code = codeRaw?.Trim('\0') ?? "";
                    code = code?.Replace("\0", "") ?? "";
                    if (string.IsNullOrEmpty(code)&& code.Length>=23)
                    {
                        UC_Operation.I.WriteLog($"扫码工位{_channelNo}获取产品电芯码异常", "Warn");
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}来料结果", 2);
                        return;
                    }

                    // 复投逻辑
                    var setreload = await _owner.GetInt_Plc($"PLC通道{_channelNo}来料复投触发");
                    if (setreload == 1)
                    {
                        var reloadSQLres = new List<T_BlueFilmDetection>();
                        short[] reloadres = { 1, 2, 3, 4, 5, 6, 7, 8, (short)_channelNo, 0, 0, 0, 0, 0, 0 };
                        //switch (_channelNo)
                        //{
                        //    case 1: reloadSQLres = _owner._localBlueFilmDetectionRepositoryA.GetByCellCode(code); break;
                        //}

                        // 原始数据（根据你之前代码中的 reloadres 应该是 short[]）
                        short[] shortArray = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

                        // 转换为大端字节数组（每个 short 占 2 字节）
                        byte[] result = new byte[shortArray.Length * 2];
                        for (int i = 0; i < shortArray.Length; i++)
                        {
                            byte[] bytes = BitConverter.GetBytes(shortArray[i]);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(bytes);   // 转为大端
                            Array.Copy(bytes, 0, result, i * 2, 2);
                        }

                        // 写入 PLC（地址应为字地址，如 DBW0）
                        await _owner._monitor.WriteByNameAsync($"PLC通道{_channelNo}来料复投信息", result);


                        if (reloadSQLres.Count >= 1)
                            reloadres = _owner.GetReloadres(reloadSQLres.First());
                        else
                            UC_Operation.I.WriteLog($"无法找到复投电芯码{code}", "Warn");

                        // 复投信息写入 PLC — 使用 PLCHandler
                        //var reloadData = ShortArrayToBytes(reloadres);
                        //var writeResult = await _owner._monitor.WriteByNameAsync(
                        //    $"PLC通道{_channelNo}来料复投信息", reloadData);
                        //if (!writeResult.IsOk)
                        //{
                        //    UC_Operation.I.WriteLog($"写入复投信息失败: {writeResult.Error}", "Error");
                        //    _owner.Flag_Error++;
                        //}
                    }

                    // MOM查询
                    var MOMRes = "OK";
                重新查询:
                    var check = _owner.MomCheckIn_Safe(code);
                    switch (check?.Result)
                    {
                        case -1: UC_Operation.I.WriteLog($"MOM入站通讯等待...{code}"); break;/* goto 重新查询;*/
                        case 0: UC_Operation.I.WriteLog($"MOM入站通讯开始...{code}"); break; /*goto 重新查询;*/
                        case 1: MOMRes = "OK"; UC_Operation.I.WriteLog($"MOM入站OK{code}", "Success"); break;
                        case 2:
                        case 3: MOMRes = "NG"; UC_Operation.I.WriteLog($"MOM入站NG{code}", "Warn"); break;
                        case 4: MOMRes = "离线"; UC_Operation.I.WriteLog($"MOM离线！{code}", "Warn"); break;
                        default: MOMRes = "NG"; break;
                    }
                    MOMRes = "OK";
                    if (MOMRes == "OK")
                    {
                        UC_Operation.I.WriteLog($"扫码工位{_channelNo}获取物料{code}MOM返回{MOMRes}", "Success");
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}来料结果", 1);
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"扫码工位{_channelNo}获取物料{code}MOM返回{MOMRes}", "Warn");
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}来料结果", 2);
                    }

                    var data = new CellData()
                    {
                        电芯码 = code,
                        检验位置 = $"工位{_channelNo}",
                        MOM查询来料状态 = MOMRes,
                        进站时间 = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                        入站结果 = MOMRes,
                        是否复投 = (setreload == 1)?"是":"否",
                    };
                    var temp = new List<CellData> { data };
                    SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");

                    #region 模拟数据
                    // 在真实入站记录之外额外插入模拟记录，让看板"检测中"计数和时段分布有数据可看。
                    // 正式环境删除此 region。
                    var simData = new CellData()
                    {
                        电芯码 = $"SIM-{_channelNo}-{DateTime.Now:HHmmss}",
                        检验位置 = $"工位{_channelNo}",
                        进站时间 = DateTime.Now.AddMinutes(-new Random().Next(1, 60)).ToString("yyyy/MM/dd HH:mm:ss"),
                        入站结果 = "OK",
                        MOM查询来料状态 = "模拟-OK",
                        是否复投 = "否",
                    };
                    var simList = new List<CellData> { simData };
                    //SQLiteGenericHelper.BulkUpsert<CellData>(simList, "电芯码", "CellData");
                    #endregion

                    _state.LastCode = code;
                    _state.LastProcessTime = DateTime.Now.Ticks;
                }
                catch (Exception ex)
                {
                    Rlog.Error($"工位{_channelNo} ProductArrive异常: {ex.Message}");
                    _owner.Flag_Error++;
                }
                finally
                {
                    _state.Processing = false;
                }
            }
        }

        /// <summary>
        /// 分流工位处理器 - 通道编号 1~4
        /// </summary>
        private class ProductLeadStationHandler : IStationHandler
        {
            private readonly AutoRun _owner;
            private readonly int _channelNo;
            private readonly ChannelEdgeState _state = new ChannelEdgeState();

            public ProductLeadStationHandler(AutoRun owner, int channelNo)
            {
                _owner = owner;
                _channelNo = channelNo;
            }

            public async Task<bool> CheckHeartbeatAsync(CancellationToken token)
            {
                // 心跳已由主循环统一管理，此处只返回true
                // 全局暂停由 Station.MonitoringLoopAsync 处理
                await Task.CompletedTask;
                return true;
            }

            public async Task<bool> WaitForSignalAsync(CancellationToken token)
            {
                //var trigger = await _owner.GetInt_Plc($"PLC通道{_channelNo}分流触发");
                //if (trigger <= 0) { _state.Processing = false; return false; }
                //if (trigger == _state.TriggerLast) return false;
                //_state.TriggerLast = trigger;

                //// 状态锁存：防止重复执行
                //if (_state.Processing) return false;
                //_state.Processing = true;

                //return true;

                var trigger = await _owner.GetInt_Plc($"PLC通道{_channelNo}分流触发");

                if (trigger <= 0)
                {
                    _state.Processing = false;
                    await _owner.SetInt_Plc($"PLC通道{_channelNo}分流NG状态", 0);
                    await _owner.SetInt_Plc($"PLC通道{_channelNo}分流出站结果", 0);
                    this._state.TriggerLast = trigger;
                    return false;
                }
                
                //if (trigger == _state.TriggerLast)
                //{
                //    _state.TriggerLast = trigger;
                //    return false;
                //}


                _state.TriggerLast = trigger;

                // 状态锁存：防止重复执行
                if (_state.Processing) return false;
                _state.Processing = true;

                return trigger == 1;
            }

            public async Task ExecuteActionAsync(CancellationToken token)
            {
                try
                 {
                    var code = await _owner.GetString_Plc($"PLC通道{_channelNo}分流电芯码");
                    string tempcode = code?.Trim('\0') ?? "";
                     tempcode = tempcode?.Replace("\0","") ?? "";
                    if (string.IsNullOrEmpty(tempcode)&& tempcode.Length>=23)
                    {
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流NG状态", 2);
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流出站结果", 2);
                        return;
                    }
                    
                        var t = SQLiteGenericHelper.QueryRaw<CellData>($"SELECT * from CellData WHERE 电芯码 = '{@tempcode}'", "CellData");
                        var data = t.FirstOrDefault(i => i.电芯码 == tempcode);
                  
                    
                    

                    if (data != null)
                    {
                        //lock (_listDataLock)
                        //{
                        //    _owner.UpdateCellDataFromSQLserver(ref data);
                        //}

                       
                        #region 模拟数据
                        // 用随机生成的视觉检测参数替换 SQL Server 查询结果，模拟 OK/NG 分布（约 85% OK）。
                        // 正式环境删除此 region。
                        var rng = new Random();
                        bool isNg = rng.Next(100) < 15;
                        string[] ngPool = { "外观缺陷", "尺寸超差", "性能不合格", "极片划痕", "焊接不良", "气孔", "异物", "极耳缺陷" };

                        data.视觉检测参数一 = $"Width1:{rng.Next(95, 105)}mm";
                        data.视觉检测参数二 = $"Width2:{rng.Next(95, 105)}mm";
                        data.视觉检测参数三 = $"Width3:{rng.Next(95, 105)}mm";
                        data.视觉检测参数四 = $"Width4:{rng.Next(95, 105)}mm";
                        data.视觉检测参数五 = $"Width5:{rng.Next(95, 105)}mm";
                        data.视觉检测参数六 = $"Width6:{rng.Next(95, 105)}mm";
                        data.视觉检测状态 = "模拟完成";
                        data.视觉检测结果 = isNg ? "NG" : "OK";
                        data.出站结果 = isNg ? "NG" : "OK";
                        data.出站时间 = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                        if (isNg)
                        {
                            int ngCount = rng.Next(1, 4);
                            var shuffled = ngPool.OrderBy(_ => rng.Next()).Take(ngCount).ToArray();
                            data.Ng类型数量 = ngCount;
                            data.Ng类型1 = shuffled[0];
                            data.Ng类型2 = ngCount > 1 ? shuffled[1] : "";
                            data.Ng类型3 = ngCount > 2 ? shuffled[2] : "";
                        }
                        #endregion

                        int way = _owner.getlead(data);
                        way = 1; // TODO: 后续移除硬编码
                        switch (way)
                        {
                            case 1: data.视觉检测结果 = "结果一"; break;
                            case 2: data.视觉检测结果 = "结果二"; break;
                            case 3: data.视觉检测结果 = "结果三"; break;
                            case 4: data.视觉检测结果 = "结果四"; break;
                        }
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流NG状态", way);
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流出站结果", way);
                        var temp = new List<CellData> { data };
                        SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");

                        string ngTypes = string.Join("|", new[]
                        {
                            data.Ng类型1, data.Ng类型2, data.Ng类型3, data.Ng类型4,
                            data.Ng类型5, data.Ng类型6, data.Ng类型7, data.Ng类型8
                        }.Where(s => !string.IsNullOrEmpty(s)));
                        //DashboardService.I.RecordExit(tempcode, "NG", ngTypes);

                        _state.LastCode = tempcode;
                        _state.LastProcessTime = DateTime.Now.Ticks;
                    }
                    else
                    {
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流NG状态", 1);
                        await _owner.SetInt_Plc($"PLC通道{_channelNo}分流出站结果", 1);
                    }
                }
                catch (Exception ex)
                {
                    Rlog.Error($"工位{_channelNo} ProductLeadArrive异常: {ex.Message}");
                    _owner.Flag_Error++;
                }
                finally
                {
                    _state.Processing = false;
                }
            }
        }

        #endregion

            }
}
