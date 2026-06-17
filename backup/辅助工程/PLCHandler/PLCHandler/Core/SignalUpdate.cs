using System;

namespace PLCHandler
{
    public sealed class SignalUpdate
    {
        public string SignalId { get; }
        public string PlcId { get; }
        public Result<object> Value { get; }
        public DateTime Timestamp { get; }

        public SignalUpdate(string signalId, string plcId, Result<object> value, DateTime timestamp)
        {
            SignalId = signalId;
            PlcId = plcId;
            Value = value;
            Timestamp = timestamp;
        }
    }
}
