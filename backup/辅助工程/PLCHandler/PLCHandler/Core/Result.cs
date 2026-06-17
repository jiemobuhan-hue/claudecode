using System;

namespace PLCHandler
{
    public readonly struct Result<T>
    {
        public bool IsOk { get; }
        public T Value { get; }
        public string Error { get; }

        private Result(bool isOk, T value, string error)
        {
            IsOk = isOk;
            Value = value;
            Error = error;
        }

        public static Result<T> Ok(T value) => new(true, value, null);
        public static Result<T> Fail(string error) => new(false, default, error);

        public static implicit operator Result<T>(T value) => Ok(value);

        public override string ToString() => IsOk ? Value?.ToString() ?? "null" : $"Error: {Error}";
    }
}
