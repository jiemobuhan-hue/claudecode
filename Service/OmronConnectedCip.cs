using HslCommunication.Profinet.Omron;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Communication
{
    public class OmronConnectedCip :  IDisposable
    {
        private string _ip;  //plc的ip地址

        /// <summary>
        /// plc的IP地址
        /// </summary>
        public string IP => _ip;

        public bool Connected { get; set; }

        private int _port;  //端口号
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port => _port;


        /// <summary>
        /// 和PLC通讯的对象
        /// </summary>
        private OmronConnectedCipNet _connectedCip;

        private bool _isStringReverse;

        public OmronConnectedCip(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _connectedCip = new OmronConnectedCipNet(ip, port) {
                ReceiveTimeOut = 2000,
                ConnectTimeOut = 2000,
            };
            _connectedCip.ByteTransform.IsStringReverseByteWord = true;

        }

        public void Connect()
        {
            Connected = _connectedCip.ConnectServer().IsSuccess;
        }

        public void Reconnect()
        {
            Connect();
        }

        public void Close()
        {
            _connectedCip.ConnectClose();
        }

        public bool ReadBool(string valAddr)
        {
            return _connectedCip.ReadBool(valAddr).Content;
        }

        public bool[] ReadBoolArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadBool(valAddr, length).Content;
        }

        public byte ReadByte(string valAddr)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadByteArray(string valAddr, ushort length = 0)
        {
            throw new NotImplementedException();
        }

        public short ReadShort(string valAddr)
        {
            return _connectedCip.ReadInt16(valAddr).Content;
        }

        public short[] ReadShortArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadInt16(valAddr, length).Content;
        }

        public ushort ReadUShort(string valAddr)
        {
            return _connectedCip.ReadUInt16(valAddr).Content;
        }

        public ushort[] ReadUShortArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadUInt16(valAddr, length).Content;
        }

        public int ReadInt(string valAddr)
        {
            return _connectedCip.ReadInt32(valAddr).Content;
        }

        public int[] ReadIntArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadInt32(valAddr, length).Content;
        }

        public uint ReadUInt(string valAddr)
        {
            return _connectedCip.ReadUInt32(valAddr).Content;
        }

        public uint[] ReadUIntArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadUInt32(valAddr, length).Content;
        }

        public float ReadFloat(string valAddr)
        {
            return _connectedCip.ReadFloat(valAddr).Content;
        }

        public float[] ReadFloatArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadFloat(valAddr, length).Content;
        }

        public double ReadDouble(string valAddr)
        {
            return _connectedCip.ReadDouble(valAddr).Content;
        }

        public double[] ReadDoubleArray(string valAddr, ushort length = 0)
        {
            return _connectedCip.ReadDouble(valAddr, length).Content;
        }

        public string ReadString(string valAddr, ushort length, Encoding encoding)
        {
            return _connectedCip.ReadString(valAddr, length).Content;
        }

        public string ReadString(string valAddr)
        {
            var str = _connectedCip.ReadString(valAddr, 64).Content;
            return str;
        }

        public T Read<T>(string valAddr, ushort length = 0)
        {
            Type type = typeof(T);
            string typeName = type.FullName;
            if (typeName == typeof(bool).FullName) {
                object obj = ReadBool(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(byte).FullName) {
                object obj = ReadByte(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(short).FullName) {
                object obj = ReadShort(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(ushort).FullName) {
                object obj = ReadUShort(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(int).FullName) {
                object obj = ReadInt(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(uint).FullName) {
                object obj = ReadUInt(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(float).FullName) {
                object obj = ReadFloat(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(double).FullName) {
                object obj = ReadDouble(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(string).FullName) {
                object obj = ReadString(valAddr);
                return (T)obj;
            }
            else if (typeName == typeof(bool[]).FullName) {
                object obj = ReadBoolArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(byte[]).FullName) {
                object obj = ReadByteArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(short[]).FullName) {
                object obj = ReadShortArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(ushort[]).FullName) {
                object obj = ReadUShortArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(int[]).FullName) {
                object obj = ReadIntArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(uint[]).FullName) {
                object obj = ReadUIntArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(float[]).FullName) {
                object obj = ReadFloatArray(valAddr, length);
                return (T)obj;
            }
            else if (typeName == typeof(double[]).FullName) {
                object obj = ReadDoubleArray(valAddr, length);
                return (T)obj;
            }
            else {
                throw new NotImplementedException();
            }
        }

        public bool WriteBool(string valAddr, bool value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteBoolArray(string valAddr, bool[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteByte(string valAddr, byte value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteByteArray(string valAddr, byte[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteShort(string valAddr, short value)
        {
            var result = _connectedCip.Write(valAddr, value);
            return result.IsSuccess;
        }

        public bool WriteShortArray(string valAddr, short[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteUShort(string valAddr, ushort value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteUShortArray(string valAddr, ushort[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteInt(string valAddr, int value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteIntArray(string valAddr, int[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteUInt(string valAddr, uint value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteUIntArray(string valAddr, uint[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteFloat(string valAddr, float value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteFloatArray(string valAddr, float[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteDouble(string valAddr, double value)
        {
            return _connectedCip.Write(valAddr, value).IsSuccess;
        }

        public bool WriteDoubleArray(string valAddr, double[] values)
        {
            return _connectedCip.Write(valAddr, values).IsSuccess;
        }

        public bool WriteString(string valAddr, string value, int length = 64)
        {
            return _connectedCip.Write(valAddr, value, length).IsSuccess;
        }

        public bool Write<T>(string valAddr, T value, int length = 64)
        {
            Type type = typeof(T);
            string typeName = type.FullName;
            object obj = value;
            if (typeName == typeof(bool).FullName) {
                return WriteBool(valAddr, (bool)obj);
            }
            else if (typeName == typeof(byte).FullName) {
                return WriteByte(valAddr, (byte)obj);
            }
            else if (typeName == typeof(short).FullName) {
                return WriteShort(valAddr, (short)obj);
            }
            else if (typeName == typeof(ushort).FullName) {
                return WriteUShort(valAddr, (ushort)obj);
            }
            else if (typeName == typeof(int).FullName) {
                return WriteInt(valAddr, (int)obj);
            }
            else if (typeName == typeof(uint).FullName) {
                return WriteUInt(valAddr, (uint)obj);
            }
            else if (typeName == typeof(float).FullName) {
                return WriteFloat(valAddr, (float)obj);
            }
            else if (typeName == typeof(double).FullName) {
                return WriteDouble(valAddr, (double)obj);
            }
            else if (typeName == typeof(string).FullName) {
                return WriteString(valAddr, (string)obj, length);
            }
            else if (typeName == typeof(bool[]).FullName) {
                return WriteBoolArray(valAddr, (bool[])obj);
            }
            else if (typeName == typeof(byte[]).FullName) {
                return WriteByteArray(valAddr, (byte[])obj);
            }
            else if (typeName == typeof(short[]).FullName) {
                return WriteShortArray(valAddr, (short[])obj);
            }
            else if (typeName == typeof(ushort[]).FullName) {
                return WriteUShortArray(valAddr, (ushort[])obj);
            }
            else if (typeName == typeof(int[]).FullName) {
                return WriteIntArray(valAddr, (int[])obj);
            }
            else if (typeName == typeof(uint[]).FullName) {
                return WriteUIntArray(valAddr, (uint[])obj);
            }
            else if (typeName == typeof(float[]).FullName) {
                return WriteFloatArray(valAddr, (float[])obj);
            }
            else if (typeName == typeof(double[]).FullName) {
                return WriteDoubleArray(valAddr, (double[])obj);
            }
            else {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
            _connectedCip?.ConnectClose();
        }
    }
}
