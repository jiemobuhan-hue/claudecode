using HslCommunication;
using HslCommunication.Profinet.Omron;
using Microsoft.IdentityModel.Logging;
using RinKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model.Device
{
    /// <summary>欧姆龙 PLC 通讯（FINS UDP）</summary>
    public class PLCOmronFins:IDisposable
    {
        /// <summary>
        /// 使用 HslCommunication 的 OmronFinsUdp，通过 UDP 与 PLC 通讯
        /// </summary>
        //public OmronFinsUdp omronFinsNet;
        public OmronFinsNet omronFinsNet;

        public OperateResult conect;

        /// <summary>构造函数，默认 DA1=10/SA1=20，可按现场调整</summary>
        public PLCOmronFins(string PLCAddress, int PortNo, int number = 0, string description = "", int DA1 = 10, int SA1 = 20) : base()
        {
            try
            {
                //omronFinsNet = new OmronFinsUdp 
                //{
                //    IpAddress = PLCAddress,
                //    Port = PortNo,
                //    SA1 = (byte)SA1,
                //    DA1 = (byte)DA1,
                //    DA2 = 0
                //};
                //omronFinsNet.ByteTransform.DataFormat = HslCommunication.Core.DataFormat.CDAB;
                omronFinsNet = new OmronFinsNet
                {
                    IpAddress = PLCAddress,   //IP地址
                    Port = PortNo,   //端口号
                    SA1 = (Byte)SA1,//上位机节点地址
                    DA1 = (Byte)DA1,//PLC的节点地址
                    DA2 = 0//PLC单元号
                };
                omronFinsNet.ByteTransform.DataFormat = HslCommunication.Core.DataFormat.CDAB;
                //omronFinsNet.IsStringReverse = true;
                
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Fatal($"PLC实例化出现异常\n{ex.Message}");
            }
        }

        /// <summary>UDP 下通过读操作验证连接，超时 500ms</summary>
        public  bool ConnectionState()
        {
            try
            {
                if (omronFinsNet == null) return false;
                var readTask = Task.Run(() =>
                {
                    try { return omronFinsNet.ReadInt16("D0").IsSuccess; }
                    catch { return false; }
                });
                if (readTask.Wait(500))
                    return readTask.Result;
                //TODO
                Rlog.Warn("检查PLC连接状态超时（500ms），可能未连接");
                return false;
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Warn($"检查PLC连接状态时发生异常: {ex.Message}");
                return false;
            }
        }

        internal  bool Connect()
        {
            try
            {
                return ConnectionState();
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Fatal($"连接欧姆龙PLC出现异常\n{ex.Message}");
                return false;
            }
        }

        /// <summary>UDP 无连接模式，释放前尝试关闭连接并释放引用</summary>
        public  void Dispose()
        {
            try
            {
                if (omronFinsNet != null)
                {
                    var disposable = omronFinsNet as IDisposable;
                    disposable?.Dispose();
                    omronFinsNet = null;
                }
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Warn($"释放欧姆龙PLC资源时发生异常: {ex.Message}");
                omronFinsNet = null;
            }
        }

        public   bool Read<T>(string TagName, out T TagValue)
        {
            TagValue = default(T);
            object obj = null;
            if (TagName != null)
            {
                try
                {
                    Type type = typeof(T);
                    if (type == typeof(int))
                    {
                        var result = omronFinsNet.ReadInt16(TagName);
                        if (result.IsSuccess)
                        {
                            obj = result.Content;
                        }
                        else
                        {
                            //TODO
                            Rlog.Error($"读取PLC地址 {TagName} 失败: {result.Message}");
                            return false;
                        }
                    }
                    else if (type == typeof(float))
                    {
                        var result = omronFinsNet.ReadFloat(TagName);
                        if (result.IsSuccess)
                        {
                            obj = result.Content;
                        }
                        else
                        {
                            //TODO
                            Rlog.Error($"读取PLC地址 {TagName} 失败: {result.Message}");
                            return false;
                        }
                    }
                    else if (type == typeof(double))
                    {
                        var result = omronFinsNet.ReadDouble(TagName);
                        if (result.IsSuccess)
                        {
                            obj = result.Content;
                        }
                        else
                        {

                            Rlog.Error($"读取PLC地址 {TagName} 失败: {result.Message}");
                            return false;
                        }
                    }
                    else
                    {
                        Rlog.Error($"不支持的读取类型: {type.Name}");
                        return false;
                    }

                }
                catch (Exception ex)
                {
                    Rlog.Fatal($"向欧姆龙PLC读取{TagName}信号时出现异常:\n{ex.Message}");
                    return false;
                }
            }
            else
            {
                Rlog.Error("TagName不能为null");
                return false;
            }

            if (obj != null)
            {
                try
                {
                    TagValue = (T)Convert.ChangeType(obj, typeof(T));
                    return true;
                }
                catch (Exception ex)
                {
                    Rlog.Error($"类型转换失败: {obj.GetType().Name} -> {typeof(T).Name}\nex:{ex.Message}");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public   bool Write<T>(string TagName, T TagValue)
        {
            try
            {
                if (string.IsNullOrEmpty(TagName))
                {
                    //TODO
                    Rlog.Error("TagName不能为空");
                    return false;
                }

                // 使用typeof(T)而不是TagValue.GetType()，更安全
                Type type = typeof(T);
                OperateResult result;

                if (type == typeof(int))
                {
                    int value = Convert.ToInt32(TagValue);
                    result = omronFinsNet.Write(TagName, value);
                }
                else if (type == typeof(float))
                {
                    float value = Convert.ToSingle(TagValue);
                    result = omronFinsNet.Write(TagName, value);
                }
                else if (type == typeof(double))
                {
                    double value = Convert.ToDouble(TagValue);
                    result = omronFinsNet.Write(TagName, value);
                }
                else
                {
                    //TODO
                    Rlog.Error($"不支持的写入类型: {type.Name}");
                    return false;
                }

                if (!result.IsSuccess)
                {
                    //TODO
                    Rlog.Error($"写入PLC地址 {TagName} 失败: {result.Message}");
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Fatal($"向欧姆龙PLC写入{TagName}信号时出现异常\n{ex.Message}");
                return false;
            }
        }

        public   bool ReadMultiTagValue<T>(string[] TagNameList, out T[] TagValueList)
        {
            int Len = TagNameList.Length;
            Hashtable retVals = null;
            TagValueList = new T[Len];
            try
            {
                //retVals = omronFinsNet.ReadString(TagNameList,20);
            }
            catch (Exception ex)
            {
                //TODO
                Rlog.Fatal($"向欧姆龙PLC读取多信号时出现异常\n{ex.Message}");
                return false;
            }

            if (retVals != null)

            {
                for (int i = 0; i < Len; i++)
                {
                    TagValueList[i] = (T)retVals[TagNameList[i]];
                }
                return true;
            }
            else
                return false;
        }

    }
}
