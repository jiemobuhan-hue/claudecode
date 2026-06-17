using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PLCHandler.Models;

namespace  PLCHandler
{
    public sealed class PlcConfigService
    {
        private readonly string _configPath;
        private readonly string _signalsPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public PlcConfigService(string configDir)
        {
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "plc_config.json");
            _signalsPath = Path.Combine(configDir, "signals_config.csv");
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
        }

        public List<PlcConfig> LoadPlcConfigs()
        {
            if (!File.Exists(_configPath))
                return new List<PlcConfig>();

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<List<PlcConfig>>(json, _jsonOptions) ?? new List<PlcConfig>();
        }

        public void SavePlcConfigs(IEnumerable<PlcConfig> configs)
        {
            var json = JsonSerializer.Serialize(configs, _jsonOptions);
            File.WriteAllText(_configPath, json);
        }

        public List<SignalData> LoadSignals()
        {
            if (!File.Exists(_signalsPath))
                return new List<SignalData>();

            var signals = new List<SignalData>();
            var lines = File.ReadAllLines(_signalsPath);

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 7) continue;

                signals.Add(new SignalData
                {
                    PlcId = fields[0].Trim(),
                    Group = fields[1].Trim(),
                    Address = fields[2].Trim(),
                    Name = fields[3].Trim(),
                    DataType = Enum.TryParse<DataTypeEnum>(fields[4].Trim(), out var dt) ? dt : DataTypeEnum.Int,
                    ArrayLength = int.TryParse(fields[5].Trim(), out var al) ? al : 1,
                    Description = fields[6].Trim()
                });
            }
            return signals;
        }

        public void SaveSignals(IEnumerable<SignalData> signals)
        {
            var lines = new List<string> { "PlcId,Group,Address,Name,DataType,ArrayLength,Description" };
            foreach (var s in signals)
                lines.Add($"{s.PlcId},{s.Group},{s.Address},{s.Name},{s.DataType},{s.ArrayLength},{s.Description}");
            File.WriteAllLines(_signalsPath, lines);
        }

        public string ConfigPath => _configPath;
        public string SignalsPath => _signalsPath;
    }
}
