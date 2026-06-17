using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Siemens;
using PLCHandler.Models;

namespace PLCHandler
{
    public sealed class SiemensConnection : IPlcConnection
    {
        private SiemensS7Net _plc;
        private readonly PlcConnectionOptions _options;
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly object _lock = new object();

        public string Id => _options.Id;
        public ConnectionState State
        {
            get { lock (_lock) return _state; }
            private set { lock (_lock) _state = value; }
        }

        public SiemensConnection(PlcConnectionOptions options)
        {
            _options = options;
            _plc = new SiemensS7Net(SiemensPLCS.S1200)
            {
                IpAddress = options.IpAddress,
                Port = options.Port,
                Slot = options.Slot
            };
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
                    // Verify connection is truly alive by reading a test address
                    var verify = _plc.ReadInt16("DB1.DBW0");
                    if (verify.IsSuccess)
                    {
                        State = ConnectionState.Connected;
                        return true;
                    }
                    _plc.ConnectClose();
                    State = ConnectionState.Disconnected;
                    return false;
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

        // ---- Typed reads ----

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

        public OperateResult<bool[]> ReadBoolArray(string address, ushort length)
        {
            var result = _plc.Read(address, length);
            if (!result.IsSuccess)
                return new OperateResult<bool[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };

            if (result.Content is byte[] bytes)
            {
                var bools = new bool[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    bools[i] = bytes[i] != 0;
                return OperateResult.CreateSuccessResult(bools);
            }
            return new OperateResult<bool[]> { IsSuccess = false, Message = "Invalid content type" };
        }

        public OperateResult<short[]> ReadInt16Array(string address, ushort length)
        {
            var result = _plc.Read(address, (ushort)(length * 2));
            if (!result.IsSuccess)
                return new OperateResult<short[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return OperateResult.CreateSuccessResult(ToArray<short>(result.Content, length));
        }

        public OperateResult<int[]> ReadInt32Array(string address, ushort length)
        {
            var result = _plc.Read(address, (ushort)(length * 4));
            if (!result.IsSuccess)
                return new OperateResult<int[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return OperateResult.CreateSuccessResult(ToArray<int>(result.Content, length));
        }

        public OperateResult<byte[]> ReadByteArray(string address, ushort length)
        {
            var result = _plc.Read(address, length);
            if (!result.IsSuccess)
                return new OperateResult<byte[]> { IsSuccess = false, Message = result.Message, ErrorCode = result.ErrorCode };
            return result.Content is byte[] bytes
                ? OperateResult.CreateSuccessResult(bytes)
                : new OperateResult<byte[]> { IsSuccess = false, Message = "Invalid content type" };
        }

        public OperateResult Write(string address, byte[] data)
        {
            return _plc.Write(address, data);
        }

        private static T[] ToArray<T>(object content, ushort length) where T : struct
        {
            if (content is byte[] bytes)
            {
                var size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
                var count = System.Math.Min(length, bytes.Length / size);
                var result = new T[count];
                Buffer.BlockCopy(bytes, 0, result, 0, count * size);
                return result;
            }
            return Array.Empty<T>();
        }

        public void Dispose() => _plc?.ConnectClose();

        public OperateResult WriteInt(string address, int data)
        {
            throw new NotImplementedException();
        }
    }
}
