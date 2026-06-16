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
        private readonly int _permanentRetryIntervalMs;
        private int _retryCount;

        public int RetryCount => _retryCount;
        public bool IsExhausted => _retryCount >= _maxRetries;
        public bool IsDegraded => _retryCount > _maxRetries;

        public RetryPolicy(int maxRetries = 10, int baseDelayMs = 500, int maxDelayMs = 30000, int permanentRetryIntervalMs = 30000)
        {
            _maxRetries = maxRetries;
            _baseDelayMs = baseDelayMs;
            _maxDelayMs = maxDelayMs;
            _permanentRetryIntervalMs = permanentRetryIntervalMs;
        }

        public void Reset()
        {
            _retryCount = 0;
        }

        public async Task<bool> WaitForNextRetryAsync(CancellationToken ct = default)
        {
            _retryCount++;

            int delay;
            if (_retryCount <= _maxRetries)
            {
                delay = Math.Min(_baseDelayMs * (int)Math.Pow(2, _retryCount - 1), _maxDelayMs);
            }
            else
            {
                delay = _permanentRetryIntervalMs;
            }

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
