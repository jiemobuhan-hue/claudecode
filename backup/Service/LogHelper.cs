using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 基于 NLog 的日志帮助类，提供静态调用方法。
    /// </summary>
    public static class LogHelper
    {
        // 获取当前类的 Logger 实例，你也可以为不同模块创建不同的 Logger
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 记录调试信息（通常仅在 Debug 模式下启用）
        /// </summary>
        public static void Debug(string message)
        {
            Logger.Debug(message);
        }

        /// <summary>
        /// 记录普通信息
        /// </summary>
        public static void Info(string message)
        {
            Logger.Info(message);
        }

        /// <summary>
        /// 记录警告信息
        /// </summary>
        public static void Warn(string message)
        {
            Logger.Warn(message);
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        public static void Error(string message)
        {
            Logger.Error(message);
        }

        /// <summary>
        /// 记录错误信息（附带异常对象）
        /// </summary>
        public static void Error(Exception ex, string message = null)
        {
            if (string.IsNullOrEmpty(message))
                Logger.Error(ex);
            else
                Logger.Error(ex, message);
        }

        /// <summary>
        /// 记录致命错误信息
        /// </summary>
        public static void Fatal(string message)
        {
            Logger.Fatal(message);
        }

        /// <summary>
        /// 记录致命错误信息（附带异常对象）
        /// </summary>
        public static void Fatal(Exception ex, string message = null)
        {
            if (string.IsNullOrEmpty(message))
                Logger.Fatal(ex);
            else
                Logger.Fatal(ex, message);
        }

        /// <summary>
        /// 强制刷新所有日志缓冲区（程序退出前调用）
        /// </summary>
        public static void Flush()
        {
            LogManager.Flush();
        }
    }
}
