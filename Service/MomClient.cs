using RinKit;
using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.MOM;

namespace ZenergyBFSI.Service
{
    internal class MomTransportException : Exception
    {
        public MomTransportException(string message, Exception inner = null)
            : base(message, inner) { }
    }

    internal class MomClient : IDisposable
    {
        private readonly string _endpointAddress;
        private readonly int _timeoutMsPerCall;
        private readonly int _maxRetries;
        private readonly object _clientLock = new object();
        private WsWcfServiceClient _client;

        public MomClient(string endpointAddress, int timeoutMsPerCall = 3000, int maxRetries = 3)
        {
            _endpointAddress = endpointAddress;
            _timeoutMsPerCall = timeoutMsPerCall;
            _maxRetries = maxRetries;
        }

        public async Task<MessageResponse> SendWithRetryAsync(MessageRequest request, CancellationToken ct)
        {
            Exception lastEx = null;

            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                if (attempt > 0)
                {
                    var delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
                    Rlog.Debug($"MOM重试 {request.CommandId} 第{attempt + 1}次, 等待{delayMs}ms");
                    await Task.Delay(delayMs, ct);
                }

                try
                {
                    var client = EnsureClient();
                    using (var timeoutCts = new CancellationTokenSource(_timeoutMsPerCall))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                    {
                        var task = client.SendMessageAsync(request);
                        var completed = await Task.WhenAny(task, Task.Delay(_timeoutMsPerCall, linkedCts.Token));
                        if (completed != task)
                            throw new TimeoutException($"MOM调用超时 {request.CommandId} ({_timeoutMsPerCall}ms)");

                        var response = await task;
                        Rlog.Debug($"MOM {request.CommandId} 成功 (尝试{attempt + 1})");
                        return response;
                    }
                }
                catch (TimeoutException ex)
                {
                    lastEx = ex;
                    Rlog.Debug($"MOM {request.CommandId} 超时 (尝试{attempt + 1}/{_maxRetries})");
                    InvalidateClient();
                }
                catch (CommunicationException ex)
                {
                    lastEx = ex;
                    Rlog.Debug($"MOM {request.CommandId} 通讯异常 (尝试{attempt + 1}/{_maxRetries}): {ex.Message}");
                    InvalidateClient();
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastEx = ex;
                    Rlog.Debug($"MOM {request.CommandId} 异常 (尝试{attempt + 1}/{_maxRetries}): {ex.Message}");
                }
            }

            throw new MomTransportException(
                $"MOM调用失败 {request.CommandId}: {_maxRetries}次重试后仍失败", lastEx);
        }

        private WsWcfServiceClient EnsureClient()
        {
            lock (_clientLock)
            {
                if (_client != null && _client.State == CommunicationState.Opened)
                    return _client;

                InvalidateClient();
                _client = new WsWcfServiceClient();
                _client.Endpoint.Address = new EndpointAddress(_endpointAddress);
                _client.Open();
                Rlog.Debug($"MOM WCF客户端已创建: {_endpointAddress}");
                return _client;
            }
        }

        private void InvalidateClient()
        {
            lock (_clientLock)
            {
                if (_client == null) return;
                try
                {
                    switch (_client.State)
                    {
                        case CommunicationState.Opened:
                            _client.Close();
                            break;
                        case CommunicationState.Faulted:
                            _client.Abort();
                            break;
                        default:
                            _client.Abort();
                            break;
                    }
                }
                catch
                {
                    try { _client.Abort(); } catch { /* 忽略关闭异常 */ }
                }
                _client = null;
            }
        }

        public void Dispose()
        {
            InvalidateClient();
        }
    }
}
