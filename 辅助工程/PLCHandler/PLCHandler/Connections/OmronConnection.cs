using DevExpress.XtraRichEdit.Import.Html;
using HslCommunication;
using HslCommunication.Profinet.Omron;
using System;
using System.Threading;
using System.Threading.Tasks;
using PLCHandler.Models;

namespace PLCHandler
{
    public sealed class OmronConnection : IPlcConnection
    {
        private OmronFinsNet _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _ioSem = new SemaphoreSlim(1, 1);

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public OmronConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new OmronFinsNet(options.IpAddress, options.Port);
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            await _ioSem.WaitAsync(ct);
            try
            {
                State = ConnectionState.Connecting;

                try
                {
                    var connectTask = Task.Run(() => _plc.ConnectServer(), ct);
                    var timeoutTask = Task.Delay(5000, ct);
                    var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                    if (completed == timeoutTask)
                    {
                        _plc.ConnectClose();
                        State = ConnectionState.Disconnected;
                        return false;
                    }

                    var result = connectTask.Result;
                    if (result.IsSuccess)
                    {
                        State = ConnectionState.Connected;
                        return true;
                    }

                    State = ConnectionState.Disconnected;
                    return false;
                }
                catch (OperationCanceledException)
                {
                    _plc.ConnectClose();
                    State = ConnectionState.Disconnected;
                    return false;
                }
                catch
                {
                    State = ConnectionState.Disconnected;
                    return false;
                }
            }
            finally
            {
                _ioSem.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _ioSem.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    _plc.ConnectClose();
                    State = ConnectionState.Disconnected;
                });
            }
            finally
            {
                _ioSem.Release();
            }
        }

        public OperateResult<bool> ReadBool(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadBool(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<short> ReadInt16(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadInt16(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<ushort> ReadUInt16(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadUInt16(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<int> ReadInt32(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadInt32(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<uint> ReadUInt32(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadUInt32(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<long> ReadInt64(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadInt64(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<ulong> ReadUInt64(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadUInt64(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<float> ReadFloat(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadFloat(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<double> ReadDouble(string address)
        {
            _ioSem.Wait();
            try { return _plc.ReadDouble(address); }
            finally { _ioSem.Release(); }
        }
        public OperateResult<string> ReadString(string address, ushort length)
        {
            _ioSem.Wait();
            try { return _plc.ReadString(address, length); }
            finally { _ioSem.Release(); }
        }

        public OperateResult<bool[]> ReadBoolArray(string address, ushort length)
        {
            _ioSem.Wait();
            try { return _plc.ReadBool(address, length); }
            finally { _ioSem.Release(); }
        }

        public OperateResult<short[]> ReadInt16Array(string address, ushort length)
        {
            _ioSem.Wait();
            try { return _plc.ReadInt16(address, length); }
            finally { _ioSem.Release(); }
        }

        public OperateResult<int[]> ReadInt32Array(string address, ushort length)
        {
            _ioSem.Wait();
            try { return _plc.ReadInt32(address, length); }
            finally { _ioSem.Release(); }
        }

        public OperateResult<byte[]> ReadByteArray(string address, ushort length)
        {
            _ioSem.Wait();
            try { return _plc.Read(address, length); }
            finally { _ioSem.Release(); }
        }

        public OperateResult Write(string address, byte[] data)
        {
            _ioSem.Wait();
            try { return _plc.Write(address, data); }
            finally { _ioSem.Release(); }
        }
        public OperateResult WriteInt(string address, int data)
        {
            _ioSem.Wait();
            try { return _plc.Write(address, (short)data); }
            finally { _ioSem.Release(); }
        }

        public void Dispose()
        {
            _plc?.ConnectClose();
            _ioSem?.Dispose();
        }
    }
}
