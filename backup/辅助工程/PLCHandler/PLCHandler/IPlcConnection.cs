using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;

namespace PLCHandler
{
    public interface IPlcConnection : IDisposable
    {
        string Id { get; }
        ConnectionState State { get; }

        /// <summary>
        /// Connect to PLC. Returns true on success. Thread-safe, can be called
        /// from any state — will close existing connection before reconnecting.
        /// </summary>
        Task<bool> ConnectAsync(CancellationToken ct = default);

        /// <summary>
        /// Disconnect from PLC.
        /// </summary>
        Task DisconnectAsync();

        // ---- 同步类型化读取 ----

        OperateResult<bool> ReadBool(string address);
        OperateResult<short> ReadInt16(string address);
        OperateResult<ushort> ReadUInt16(string address);
        OperateResult<int> ReadInt32(string address);
        OperateResult<uint> ReadUInt32(string address);
        OperateResult<long> ReadInt64(string address);
        OperateResult<ulong> ReadUInt64(string address);
        OperateResult<float> ReadFloat(string address);
        OperateResult<double> ReadDouble(string address);
        OperateResult<string> ReadString(string address, ushort length);
        OperateResult<bool[]> ReadBoolArray(string address, ushort length);
        OperateResult<short[]> ReadInt16Array(string address, ushort length);
        OperateResult<int[]> ReadInt32Array(string address, ushort length);
        OperateResult<byte[]> ReadByteArray(string address, ushort length);

        // ---- 写入（保留但非重点）----

        OperateResult Write(string address, byte[] data);
        OperateResult WriteInt(string address, int data);
    }
}
