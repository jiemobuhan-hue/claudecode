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

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                _plc.ConnectClose();
                State = ConnectionState.Disconnected;
            });
        }

        public OperateResult<bool> ReadBool(string address) => _plc.ReadBool(address);
        public OperateResult<short> ReadInt16(string address) => _plc.ReadInt16(address);
        //public OperateResult<short> ReadInt16(string address) => short.Parse(_plc.ReadInt16(address).Content.ToString());
        public OperateResult<ushort> ReadUInt16(string address) => _plc.ReadUInt16(address);
        public OperateResult<int> ReadInt32(string address) => _plc.ReadInt32(address);
        public OperateResult<uint> ReadUInt32(string address) => _plc.ReadUInt32(address);
        public OperateResult<long> ReadInt64(string address) => _plc.ReadInt64(address);
        public OperateResult<ulong> ReadUInt64(string address) => _plc.ReadUInt64(address);
        public OperateResult<float> ReadFloat(string address) => _plc.ReadFloat(address);
        public OperateResult<double> ReadDouble(string address) => _plc.ReadDouble(address);
        public OperateResult<string> ReadString(string address, ushort length) => _plc.ReadString(address, length);

        public OperateResult<bool[]> ReadBoolArray(string address, ushort length)
        {
            return _plc.ReadBool(address, length);
        }

        public OperateResult<short[]> ReadInt16Array(string address, ushort length)
        {
            return _plc.ReadInt16(address, length);
        }

        public OperateResult<int[]> ReadInt32Array(string address, ushort length)
        {
            return _plc.ReadInt32(address, length);
        }

        public OperateResult<byte[]> ReadByteArray(string address, ushort length)
        {
            return _plc.Read(address, length);
        }

        public OperateResult Write(string address, byte[] data)
        {
            return  _plc.Write(address, data);
        }
        public OperateResult WriteInt(string address, int data)
        {
            return _plc.Write(address,(short) data);
        }

        public void Dispose() => _plc?.ConnectClose();
    }
}
