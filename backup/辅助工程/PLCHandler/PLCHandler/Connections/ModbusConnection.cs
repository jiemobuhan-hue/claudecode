using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.ModBus;
using PLCHandler.Models;

namespace PLCHandler
{
    public sealed class ModbusConnection : IPlcConnection
    {
        private ModbusTcpNet _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public ModbusConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new ModbusTcpNet(options.IpAddress, options.Port, options.Channel);
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
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
                State = result.IsSuccess ? ConnectionState.Connected : ConnectionState.Disconnected;
                return result.IsSuccess;
            }
            catch
            {
                State = ConnectionState.Disconnected;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() => { _plc.ConnectClose(); State = ConnectionState.Disconnected; });
        }

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);
        public OperateResult<bool[]> ReadBoolArray(string address, ushort length) => NotSupported<bool[]>();
        public OperateResult<short[]> ReadInt16Array(string address, ushort length) => NotSupported<short[]>();
        public OperateResult<int[]> ReadInt32Array(string address, ushort length) => NotSupported<int[]>();
        public OperateResult<byte[]> ReadByteArray(string address, ushort length) => NotSupported<byte[]>();
        public OperateResult Write(string address, byte[] data) => _plc.Write(address, data);
        public void Dispose() => _plc?.ConnectClose();

        private static OperateResult<T> NotSupported<T>() =>
            new() { IsSuccess = false, Message = "NotSupported" };

        public OperateResult WriteInt(string address, int data)
        {
            throw new NotImplementedException();
        }
    }
}
