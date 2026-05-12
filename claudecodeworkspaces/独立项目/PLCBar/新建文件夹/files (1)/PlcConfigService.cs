using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace PLCBar.Service
{
    /// <summary>
    /// 负责读写 plc_config.json。
    /// 文件与 signals.csv 同目录（应用程序根目录）。
    /// </summary>
    public class PlcConfigService
    {
        // ── 文件路径 ────────────────────────────────────────────
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plc_config.json");

        // JSON 选项：中文不转义 + 缩进美化
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented      = true,
            Encoder            = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        // ────────────────────────────────────────────────────────
        // 读取配置
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// 从 plc_config.json 加载 PLC 连接配置。
        /// 若文件不存在则返回空字典。
        /// </summary>
        public Dictionary<string, (string IpAddress, int Port)> LoadConfig()
        {
            var result = new Dictionary<string, (string, int)>();

            if (!File.Exists(ConfigPath))
                return result;

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var raw     = JsonSerializer.Deserialize<Dictionary<string, PlcConfigEntry>>(json);
                if (raw == null) return result;

                foreach (var (key, entry) in raw)
                {
                    if (!string.IsNullOrWhiteSpace(entry.IpAddress) && entry.Port > 0)
                        result[key] = (entry.IpAddress, entry.Port);
                }
            }
            catch (Exception ex)
            {
                // 配置文件损坏时优雅降级，调用方自行处理
                throw new InvalidOperationException($"读取 plc_config.json 失败: {ex.Message}", ex);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────
        // 保存配置
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// 将 PLC 连接配置序列化并写入 plc_config.json。
        /// </summary>
        public void SaveConfig(Dictionary<string, (string IpAddress, int Port)> configs)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));

            var raw = configs.ToDictionary(
                kv => kv.Key,
                kv => new PlcConfigEntry { IpAddress = kv.Value.IpAddress, Port = kv.Value.Port });

            string json = JsonSerializer.Serialize(raw, WriteOptions);
            File.WriteAllText(ConfigPath, json);
        }

        // ────────────────────────────────────────────────────────
        // 首次启动：用 CSV 中出现的 PlcId 生成默认配置
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// 若配置文件已存在则读取；否则为每个 plcId 生成默认条目并持久化。
        /// </summary>
        public Dictionary<string, (string IpAddress, int Port)> GetOrCreateConfig(
            IEnumerable<string> plcIds)
        {
            if (File.Exists(ConfigPath))
                return LoadConfig();

            // 生成默认值：IP 占位符 + 标准端口 9600
            var defaults = plcIds.Distinct()
                .ToDictionary(id => id, _ => ("192.168.1.101", 9600));

            SaveConfig(defaults);
            return defaults;
        }

        // ────────────────────────────────────────────────────────
        // 内部 DTO
        // ────────────────────────────────────────────────────────
        private class PlcConfigEntry
        {
            public string IpAddress { get; set; } = string.Empty;
            public int    Port      { get; set; }
        }
    }
}
