using System;
using PLCHandler.Models;

namespace PLCHandler
{
    public static class PlcConnectionFactory
    {
        public static IPlcConnection Create(PlcConnectionOptions options)
        {
            return options.Brand switch
            {
                PLCBrand.Siemens => new SiemensConnection(options),
                PLCBrand.Omron => new OmronConnection(options),
                PLCBrand.Mitsubishi => new MitsubishiConnection(options),
                PLCBrand.ModbusTcp => new ModbusConnection(options),
                _ => throw new ArgumentException($"Unsupported PLC brand: {options.Brand}")
            };
        }
    }
}
