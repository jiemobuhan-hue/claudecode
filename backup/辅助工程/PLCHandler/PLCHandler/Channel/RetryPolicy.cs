using System;
using System.Threading;
using System.Threading.Tasks;

namespace PLCHandler
{
    public sealed class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly int _baseDelayMs;
        private readonly int _maxDelayMs;
        private int _retryCount;

        public int RetryCount => _retryCount;
        public bool IsExhausted => _retryCount >= _maxRetries;

        public RetryPolicy(int maxRetries = 10, int baseDelayMs = 500, int maxDelayMs = 30000)
        {
            _maxRetries = maxRetries;
            _baseDelayMs = baseDelayMs;
            _maxDelayMs = maxDelayMs;
        }

        public void Reset()
        {
            _retryCount = 0;
        }

        public async Task<bool> WaitForNextRetryAsync(CancellationToken ct = default)
        {
            if (_retryCount >= _maxRetries)
                return false;

            _retryCount++;
            var delay = Math.Min(_baseDelayMs * (int)Math.Pow(2, _retryCount - 1), _maxDelayMs);

            try
            {
                await Task.Delay(delay, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
