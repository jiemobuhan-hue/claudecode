using System;
using HslCommunication;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Siemens;
using HslCommunication.Profinet.Omron;
using HslCommunication.Profinet.Melsec;
using HslCommunication.Profinet.Keyence;
using HslCommunication.Core.Net;
using PLCHandler.Models;

namespace PLCHandler
{
    public sealed class PlcConnectionOptions
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public PLCBrand Brand { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public byte Slot { get; set; } = 1;
        public byte Channel { get; set; } = 0;
        public string Group { get; set; } = "默认分组";
        public bool IsEnabled { get; set; } = true;
        public int PollingIntervalMs { get; set; } = 500;
        public int ConnectionTimeoutMs { get; set; } = 3000;
        public int ReconnectIntervalMs { get; set; } = 5000;
    }
}
