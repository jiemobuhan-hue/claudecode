using System;
using System.Threading.Tasks;
using PLCHandler.Models;

namespace  PLCHandler
{
    public sealed class SignalReader
    {
        private readonly IPlcConnection _plc;

        public SignalReader(IPlcConnection plc)
        {
            _plc = plc;
        }

        public async Task<Result<object>> ReadValueAsync(SignalData signal)
        {
            try
            {
                return await Task.Run(() => ReadValueCore(signal));
            }
            catch (Exception ex)
            {
                return Result<object>.Fail(ex.Message);
            }
        }

        private Result<object> ReadValueCore(SignalData signal)
        {
            switch (signal.DataType)
            {
                case DataTypeEnum.Bool:
                {
                    var r = _plc.ReadBool(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Short:
                {
                    var r = _plc.ReadInt16(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.UShort:
                {
                    var r = _plc.ReadUInt16(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Int:
                {
                    var r = _plc.ReadInt32(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.UInt:
                {
                    var r = _plc.ReadUInt32(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Long:
                {
                    var r = _plc.ReadInt64(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.ULong:
                {
                    var r = _plc.ReadUInt64(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Float:
                {
                    var r = _plc.ReadFloat(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Double:
                {
                    var r = _plc.ReadDouble(signal.Address);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.String:
                {
                    var len = signal.ArrayLength > 0 ? signal.ArrayLength : 16;
                    var r = _plc.ReadString(signal.Address, (ushort)len);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.BoolArray:
                {
                    var r = _plc.ReadBoolArray(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.ShortArray:
                {
                    var r = _plc.ReadInt16Array(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.IntArray:
                {
                    var r = _plc.ReadInt32Array(signal.Address, (ushort)signal.ArrayLength);
                    return r.IsSuccess ? r.Content : Result<object>.Fail(r.Message);
                }
                case DataTypeEnum.Byte:
                {
                    var r = _plc.ReadByteArray(signal.Address, 1);
                    return r.IsSuccess ? r.Content[0] : Result<object>.Fail(r.Message);
                }
                default:
                    return Result<object>.Fail($"Unsupported data type: {signal.DataType}");
            }
        }
    }
}
