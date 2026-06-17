using System;

namespace PLCHandler.Models
{
    public sealed class PlcConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public PLCBrand Brand { get; set; } = PLCBrand.Siemens;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 102;
        public byte Slot { get; set; } = 1;
        public byte Channel { get; set; } = 0;
        public string Group { get; set; } = "默认分组";
        public bool IsEnabled { get; set; } = true;
        public int PollingIntervalMs { get; set; } = 500;
        public int ConnectionTimeoutMs { get; set; } = 3000;
        public int ReconnectIntervalMs { get; set; } = 5000;
    }
}
