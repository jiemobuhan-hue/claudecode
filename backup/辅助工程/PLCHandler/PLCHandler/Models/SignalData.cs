using System;

namespace PLCHandler.Models
{
    public sealed class SignalData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PlcId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DataTypeEnum DataType { get; set; } = DataTypeEnum.Int;
        public int ArrayLength { get; set; } = 1;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
