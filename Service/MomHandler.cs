using Newtonsoft.Json;
using RinKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.MOM;
using ZenergyBFSI.MOM;
using ZenergyBFSI.View;
using ParameterInfo = ZenergyBFSI.Model.ParameterInfo;

namespace ZenergyBFSI.Service
{
    public class MomHandler
    {
        #region Singleton

        private static readonly Lazy<MomHandler> _instance = new Lazy<MomHandler>(() => new MomHandler());
        public static MomHandler I => _instance.Value;

        private MomHandler() { }

        #endregion

        #region State

        private static readonly object _syncRoot = new object();
        private CancellationTokenSource _cts;
        private BlockingCollection<MomCommand> _dispatchQueue;

        private MomClient _client;
        private MomCircuitBreaker _circuitBreaker;
        private MomOfflineQueue _offlineQueue;
        private Task _dispatcherTask;
        private Task _heartbeatTask;
        private Task _offlineReplayTask;

        private volatile bool _initialized;
        private int _count;
        private List<ParameterInfo> _listParam = new List<ParameterInfo>();
        private List<MaterialUpLoad_MaterialInfo> _material = new List<MaterialUpLoad_MaterialInfo>();
        private List<CellData> _history = new List<CellData>();

        public long TS { get; set; } = -1;
        internal List<MaterialUpLoad_MaterialInfo> Material { get => _material; set => _material = value; }
        public bool IsOnline => _initialized && _circuitBreaker != null && _circuitBreaker.State != CircuitState.Open;

        #endregion

        #region Init / Close

        public bool Init()
        {
            lock (_syncRoot)
            {
                if (_initialized) return true;
                try
                {
                    UC_Operation.I.WriteLog("MOM初始化...", "Debug");
                    Rdb.SelectList(out _listParam, "SELECT * FROM ParameterInfo WHERE Enable=1");
                    Rdb.SelectList(out List<CellData> list, @"Select * From CellData ");
                    _history = list.OrderBy(c => c.Id).ToList();

                    _cts = new CancellationTokenSource();
                    _dispatchQueue = new BlockingCollection<MomCommand>();
                    _circuitBreaker = new MomCircuitBreaker();
                    _offlineQueue = new MomOfflineQueue();
                    _offlineQueue.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                    _client = new MomClient(Settings.MOM地址);

                    _dispatcherTask = Task.Run(() => DispatcherLoopAsync(_cts.Token));
                    _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
                    _offlineReplayTask = Task.Run(() => OfflineReplayLoopAsync(_cts.Token));

                    _count = Settings.MOM联机计数 - 1;
                    _initialized = true;
                    UC_Operation.I.WriteLog("MOM初始化成功", "Info");
                    return true;
                }
                catch (Exception ex)
                {
                    UC_Operation.I.WriteLog($"MOM Init异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
                    return false;
                }
            }
        }

        public void Close()
        {
            try
            {
                _cts?.Cancel();
                _dispatchQueue?.CompleteAdding();
                _client?.Dispose();
                _initialized = false;
                UC_Operation.I.WriteLog("MomHandler Close", "Warn");
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"MomHandler Close异常: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Public API — Inbound/Outbound

        public Task<MomCheckResult> CheckInAsync(string serialNo)
        {
            if (!_initialized) return Task.FromResult(OfflineResult(serialNo));
            if (Settings.MOM在线 != 1) return Task.FromResult(OfflineResult(serialNo));

            var req = new CellInput_Request();
            req.SerialNos.Add(new CellInput_SerialNo(serialNo));
            var requestJson = JsonConvert.SerializeObject(req);

            return EnqueueAsync("CellInput", requestJson, persistable: true, parseFunc: responseJson =>
            {
                var res = JsonConvert.DeserializeObject<CellInput_Response>(responseJson);
                var item = res.SerialNos?.FirstOrDefault(s => s.SerialNo == serialNo);
                return new MomCheckResult
                {
                    SerialNo = serialNo,
                    Result = (item != null && item.Result) ? MomResultCode.Ok : MomResultCode.Ng
                };
            });
        }

        public Task<MomCheckResult> CheckOutAsync(CellData data)
        {
            if (!_initialized) return Task.FromResult(OfflineResult(data.电芯码));
            if (Settings.MOM在线 != 1) return Task.FromResult(OfflineResult(data.电芯码));

            var req = new CellOutput_Request();
            var cell = new CellOutput_SerialNo(data.电芯码, Settings.电芯型号, data);
            req.SerialNos.Add(cell);
            var requestJson = JsonConvert.SerializeObject(req);

            return EnqueueAsync("CellOutput", requestJson, persistable: true, parseFunc: responseJson =>
            {
                var res = JsonConvert.DeserializeObject<CellOutput_Response>(responseJson);
                return new MomCheckResult
                {
                    SerialNo = data.电芯码,
                    Result = res.ResultFlag ? MomResultCode.Ok : MomResultCode.Ng,
                    ErrorMessage = res.MOMMessage
                };
            });
        }

        #endregion

        #region Internal API — Device Reporting

        public Task<bool> ReportStatusAsync(string locationId, string statusCode, string reasonCode, string description, string startDate)
        {
            if (!_initialized) return Task.FromResult(false);

            var req = new EqptStatus_Request(locationId, statusCode, reasonCode, description, startDate);
            return EnqueueForBoolAsync("EqptStatus", JsonConvert.SerializeObject(req), persistable: true);
        }

        internal Task<bool> ReportAlertAsync(List<EqptAlert_AlertInfo> alerts)
        {
            if (!_initialized) return Task.FromResult(false);

            var req = new EqptAlert_Request { AlertInfo = alerts };
            return EnqueueForBoolAsync("EqptAlert", JsonConvert.SerializeObject(req), persistable: true);
        }

        internal Task<bool> CheckParametersAsync(List<ParameterCheck_ParameterInfo> parameters)
        {
            if (!_initialized) return Task.FromResult(false);

            var req = new ParameterCheck_Request { ParameterInfo = parameters };
            return EnqueueForBoolAsync("ParameterCheck", JsonConvert.SerializeObject(req), persistable: false);
        }

        internal Task<EqptAlive_Response> HeartbeatAsync()
        {
            if (!_initialized) return Task.FromResult(new EqptAlive_Response());

            var req = new EqptAlive_Request();
            return EnqueueAsync("EqptAlive", JsonConvert.SerializeObject(req), persistable: false, parseFunc: responseJson =>
            {
                return JsonConvert.DeserializeObject<EqptAlive_Response>(responseJson);
            });
        }

        internal Task<EqptRun_Response> EqptRunAsync()
        {
            if (!_initialized) return Task.FromResult(new EqptRun_Response());

            var req = new EqptRun_Request();
            return EnqueueAsync("EqptRun", JsonConvert.SerializeObject(req), persistable: false, parseFunc: responseJson =>
            {
                return JsonConvert.DeserializeObject<EqptRun_Response>(responseJson);
            });
        }

        #endregion

        #region Public API — Query (no WCF, thread-safe)

        public int ParameterCount()
        {
            return _listParam.Count > 0 ? _listParam.Count : 999;
        }

        public ParameterInfo GetParameter(string description)
        {
            if (_listParam == null) return null;
            return _listParam.FirstOrDefault(p => p.Description == description);
        }

        public List<ParameterInfo> AllParameter()
        {
            return _listParam;
        }

        public bool LimitCheck(string pName, float val)
        {
            var param = _listParam.FirstOrDefault(p => p.Description == pName);
            if (param == null)
            {
                UC_Operation.I.WriteLog($"MOM参数未获取:{pName}", "Error");
                return false;
            }
            if (!float.TryParse(param.UpperSpecificationsLimit.Trim(), out float up))
            {
                UC_Operation.I.WriteLog($"MOM参数Upper错误:{param.UpperSpecificationsLimit}", "Error");
                return false;
            }
            if (!float.TryParse(param.LowerSpecificationsLimit.Trim(), out float low))
            {
                UC_Operation.I.WriteLog($"MOM参数Lower错误:{param.LowerSpecificationsLimit}", "Error");
                return false;
            }
            if (val > up) { UC_Operation.I.WriteLog($"MOM参数LimitCheck:{val}>{up}", "Warn"); return false; }
            if (val < low) { UC_Operation.I.WriteLog($"MOM参数LimitCheck:{val}<{low}", "Warn"); return false; }
            return true;
        }

        public void UpdateHistory(CellData data)
        {
            lock (_syncRoot)
            {
                try
                {
                    _history.RemoveAt(0);
                    _history.Add(data);
                    UC_Operation.I.WriteLog($"UpdateHistory:{_history.First()?.Id} - {_history.Last()?.Id}", "Debug");
                }
                catch (Exception ex)
                {
                    UC_Operation.I.WriteLog($"UpdateHistory异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
                }
            }
        }

        public CellData GetHistoryQCZ(string 电芯码)
        {
            lock (_syncRoot)
            {
                return _history.FirstOrDefault(c => c.电芯码 == 电芯码);
            }
        }

        public CellData GetHistoryJDJC(string 电芯码)
        {
            lock (_syncRoot)
            {
                return _history.FirstOrDefault(c => c.电芯码 == 电芯码);
            }
        }

        #endregion

        #region Internal API — Parameter Check (local, no WCF)

        internal bool ParameterCheck(List<ParameterInfo> pre, List<EqptRun_ParameterInfo> now)
        {
            if (pre.Count != now.Count) return false;
            for (int i = 0; i < pre.Count; i++)
            {
                if (pre[i].ParameterCode != now[i].ParameterCode) return false;
                if (pre[i].ParameterType != now[i].ParameterType) return false;
                if (pre[i].TargetValue != now[i].TargetValue) return false;
                if (pre[i].UOMCode != now[i].UOMCode) return false;
                if (pre[i].UpperControlLimit != now[i].UpperControlLimit) return false;
                if (pre[i].LowerControlLimit != now[i].LowerControlLimit) return false;
                if (pre[i].UpperSpecificationsLimit != now[i].UpperSpecificationsLimit) return false;
                if (pre[i].LowerSpecificationsLimit != now[i].LowerSpecificationsLimit) return false;
                if (pre[i].Description != now[i].Description) return false;
            }
            return true;
        }

        public bool ParameterCheck(List<ParameterInfo> parameterInfo)
        {
            var list = new List<ParameterCheck_ParameterInfo>();
            foreach (var param in parameterInfo)
            {
                list.Add(new ParameterCheck_ParameterInfo(
                    param.ParameterCode, param.ParameterType, param.Value, param.TargetValue,
                    param.UOMCode, param.UpperControlLimit, param.LowerControlLimit,
                    param.UpperSpecificationsLimit, param.LowerSpecificationsLimit, param.Description));
            }
            return CheckParametersAsync(list).GetAwaiter().GetResult();
        }

        #endregion

        #region Private — Heartbeat Loop

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Settings.MOM心跳间隔, ct);

                    if (Settings.MOM在线 < 1) continue;

                    var aliveResp = await HeartbeatAsync();
                    if (aliveResp != null)
                    {
                        TS = DataHelper.TimeMS;
                        if (aliveResp.KeyFlag != "0" && WD_Alert.Alarmnums < 1)
                        {
                            UC_Operation.I.WriteLog($"MOM心跳:{aliveResp.KeyFlag}，{aliveResp.MOMMessage}", "Warn");
                            UC_Operation.I.Alert(aliveResp.MOMMessage);
                            if (uint.TryParse(aliveResp.KeyFlag, out uint flag))
                            {
                                bool isStop = DataHelper.UintToBits(flag)[5];
                                AutoRun.I.Alarm(aliveResp.MOMMessage, isStop ? 2 : 1);
                            }
                        }
                        else
                        {
                            AutoRun.I.Alarm("", 0);
                        }
                    }

                    _count++;
                    if (_count > Settings.MOM联机计数)
                    {
                        await MaterialUpLoadAsync();
                        ParameterPLC(_listParam);
                        await EqptRunAndSyncParamsAsync();
                        _count = 0;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("没有终结点在侦听可以接受消息的"))
                        UC_Operation.I.WriteLog("MOM通讯中断！", "Warn");
                    else if (ex.Message.Contains("database"))
                        UC_Operation.I.WriteLog("数据库繁忙，历史数据查询失败！" + ex.Message, "Warn");
                    else
                        UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");

                    await Task.Delay(Settings.错误等待, ct);
                }
            }
        }

        private async Task MaterialUpLoadAsync()
        {
            try
            {
                var req = new MaterialUpLoad_Request();
                var resp = await EnqueueForResponseAsync("MaterialUpLoad", JsonConvert.SerializeObject(req));
                if (resp == null) return;

                var resData = JsonConvert.DeserializeObject<MaterialUpLoad_Response>(resp);
                if (!resData.ResultFlag)
                {
                    UC_Operation.I.WriteLog(resData.MOMMessage, "Warn");
                    return;
                }
                Material = resData.MaterialInfo;
                foreach (var item in Material)
                {
                    UC_Operation.I.WriteLog($"{item.Location} | {item.ProductNo} | {item.LabelNo} | {item.Quantity} {item.UomCode}", "Debug");
                }
                UC_Operation.I.WriteLog("MOM原材料上机.完成", "Info");
                Rlog.Debug(resp);
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"MaterialUpLoad异常: {ex.Message}", "Error");
            }
        }

        private async Task EqptRunAndSyncParamsAsync()
        {
            try
            {
                var resp = await EqptRunAsync();
                if (resp == null || !resp.ResultFlag)
                {
                    if (resp != null) UC_Operation.I.WriteLog(resp.MOMMessage, "Warn");
                    return;
                }
                UC_Operation.I.WriteLog("MOM联机:OK", "Info");
                Rlog.Debug(JsonConvert.SerializeObject(resp));

                if (resp.ParameterInfo.Count > 0)
                {
                    if (!ParameterCheck(_listParam, resp.ParameterInfo))
                    {
                        UC_Operation.I.WriteLog("MOM参数不一致", "Warn");
                        Rdb.Do("UPDATE ParameterInfo SET Enable = 0");
                        var list = new List<ParameterInfo>();
                        foreach (var item in resp.ParameterInfo)
                        {
                            var param = new ParameterInfo(item);
                            list.Add(param);
                            Rdb.Insert(param, true);
                        }
                        _listParam = list;
                        Settings.Save();
                        UC_Operation.I.WriteLog("MOM参数已更新", "Info");
                    }
                }
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"EqptRun异常: {ex.Message}", "Error");
            }
        }

        private void ParameterPLC(List<ParameterInfo> listParam)
        {
            // 本地参数处理，无 WCF 调用。模板逻辑保留供后续扩展。
        }

        private void VersionUpLoad()
        {
            // TODO
        }

        #endregion

        #region Private — Offline Replay Loop

        private async Task OfflineReplayLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, ct);

                    if (_circuitBreaker.State == CircuitState.Open) continue;

                    var pending = await _offlineQueue.DequeuePendingAsync(50);
                    foreach (var record in pending)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var result = await EnqueueAsyncCore(record.CommandId, record.RequestJson, ct);
                            if (result.Success)
                            {
                                await _offlineQueue.MarkCompletedAsync(record.Id);
                                _circuitBreaker.RecordSuccess();
                            }
                            else
                            {
                                record.RetryCount++;
                                if (record.RetryCount >= 10)
                                    await _offlineQueue.MarkFailedAsync(record.Id, result.ErrorMessage);
                                else
                                    await _offlineQueue.MarkFailedAsync(record.Id, result.ErrorMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            record.RetryCount++;
                            if (record.RetryCount >= 10)
                                await _offlineQueue.MarkFailedAsync(record.Id, ex.Message);
                            else
                                await _offlineQueue.MarkFailedAsync(record.Id, ex.Message);
                        }
                    }

                    await _offlineQueue.CleanupExpiredAsync(7);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Rlog.Error($"OfflineReplay异常: {ex.Message}");
                }
            }
        }

        #endregion

        #region Private — Dispatcher Loop

        private async Task DispatcherLoopAsync(CancellationToken ct)
        {
            foreach (var command in _dispatchQueue.GetConsumingEnumerable(ct))
            {
                try
                {
                    var result = await EnqueueAsyncCore(command.CommandId, command.RequestJson, ct);

                    if (result.Success)
                    {
                        _circuitBreaker.RecordSuccess();
                    }
                    else
                    {
                        _circuitBreaker.RecordFailure();
                        if (command.Persistable)
                            await _offlineQueue.EnqueueAsync(command.CommandId, command.RequestJson);
                    }
                    command.TCS.TrySetResult(result);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _circuitBreaker.RecordFailure();
                    if (command.Persistable)
                        await _offlineQueue.EnqueueAsync(command.CommandId, command.RequestJson);
                    command.TCS.TrySetResult(new MomCommandResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        }

        private async Task<MomCommandResult> EnqueueAsyncCore(string commandId, string requestJson, CancellationToken ct)
        {
            if (!_circuitBreaker.AllowRequest())
            {
                return new MomCommandResult
                {
                    Success = false,
                    ErrorMessage = "熔断器已打开，MOM不可达"
                };
            }

            var msgReq = new MessageRequest
            {
                CommandId = commandId,
                CommandRequestJson = requestJson,
                RequestDate = DateTime.UtcNow,
                MessageGuid = Guid.NewGuid()
            };

            Rlog.Debug($"{commandId}_Request:{requestJson}");

            try
            {
                var response = await _client.SendWithRetryAsync(msgReq, ct);
                Rlog.Debug($"{commandId}_Response:{response.CommandResponseJson}");
                return new MomCommandResult
                {
                    Success = response.Success,
                    ResponseJson = response.CommandResponseJson,
                    ErrorCode = response.ErrorCode,
                    ErrorMessage = response.ErrorMessage
                };
            }
            catch (MomTransportException ex)
            {
                Rlog.Error($"MOM {commandId} 传输失败: {ex.Message}");
                return new MomCommandResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        #endregion

        #region Private — Enqueue Helpers

        private async Task<T> EnqueueAsync<T>(string commandId, string requestJson, bool persistable,
            Func<string, T> parseFunc)
        {
            try
            {
                var result = await EnqueueAndWaitAsync(commandId, requestJson, persistable);
                if (result.Success && !string.IsNullOrEmpty(result.ResponseJson))
                    return parseFunc(result.ResponseJson);

                return default;
            }
            catch (Exception ex)
            {
                Rlog.Error($"EnqueueAsync {commandId} 异常: {ex.Message}");
                return default;
            }
        }

        private async Task<bool> EnqueueForBoolAsync(string commandId, string requestJson, bool persistable)
        {
            try
            {
                var result = await EnqueueAndWaitAsync(commandId, requestJson, persistable);
                if (result.Success && !string.IsNullOrEmpty(result.ResponseJson))
                {
                    var resp = JsonConvert.DeserializeObject<BaseResponse>(result.ResponseJson);
                    if (resp != null)
                    {
                        if (!resp.ResultFlag) UC_Operation.I.WriteLog(resp.MOMMessage, "Warn");
                        return resp.ResultFlag;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Rlog.Error($"EnqueueForBoolAsync {commandId} 异常: {ex.Message}");
                return false;
            }
        }

        private async Task<string> EnqueueForResponseAsync(string commandId, string requestJson)
        {
            var result = await EnqueueAndWaitAsync(commandId, requestJson, persistable: false);
            return result.Success ? result.ResponseJson : null;
        }

        private Task<MomCommandResult> EnqueueAndWaitAsync(string commandId, string requestJson, bool persistable)
        {
            if (!_circuitBreaker.AllowRequest())
            {
                return Task.FromResult(new MomCommandResult
                {
                    Success = false,
                    ErrorMessage = "熔断器已打开，MOM不可达"
                });
            }

            var command = new MomCommand(commandId, requestJson, persistable);
            _dispatchQueue.Add(command);
            return command.TCS.Task;
        }

        private static MomCheckResult OfflineResult(string serialNo)
        {
            return new MomCheckResult { SerialNo = serialNo, Result = MomResultCode.Offline };
        }

        #endregion

        #region Private — Internal Types

        private class MomCommand
        {
            public string CommandId { get; }
            public string RequestJson { get; }
            public bool Persistable { get; }
            public TaskCompletionSource<MomCommandResult> TCS { get; }

            public MomCommand(string commandId, string requestJson, bool persistable)
            {
                CommandId = commandId;
                RequestJson = requestJson;
                Persistable = persistable;
                TCS = new TaskCompletionSource<MomCommandResult>();
            }
        }

        private class MomCommandResult
        {
            public bool Success { get; set; }
            public string ResponseJson { get; set; }
            public string ErrorCode { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }
}
