using RinKit;
using System;
using System.Threading;

namespace ZenergyBFSI.Service
{
    internal enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    internal class MomCircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _cooldownPeriod;
        private readonly object _sync = new object();
        private CircuitState _state = CircuitState.Closed;
        private int _consecutiveFailures;
        private DateTime _openedAt;

        public MomCircuitBreaker(int failureThreshold = 5, int cooldownSeconds = 30)
        {
            _failureThreshold = failureThreshold;
            _cooldownPeriod = TimeSpan.FromSeconds(cooldownSeconds);
        }

        public CircuitState State
        {
            get { lock (_sync) return _state; }
        }

        public bool AllowRequest()
        {
            lock (_sync)
            {
                switch (_state)
                {
                    case CircuitState.Closed:
                        return true;
                    case CircuitState.HalfOpen:
                        return true;
                    case CircuitState.Open:
                        if (DateTime.UtcNow - _openedAt >= _cooldownPeriod)
                        {
                            _state = CircuitState.HalfOpen;
                            Rlog.Debug("MOM熔断器: Open → HalfOpen (探测中)");
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }
            }
        }

        public void RecordSuccess()
        {
            lock (_sync)
            {
                if (_state != CircuitState.Closed)
                {
                    Rlog.Debug($"MOM熔断器: {_state} → Closed (恢复)");
                }
                _state = CircuitState.Closed;
                _consecutiveFailures = 0;
            }
        }

        public void RecordFailure()
        {
            lock (_sync)
            {
                _consecutiveFailures++;
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;
                    Rlog.Debug($"MOM熔断器: HalfOpen → Open (探测失败，{_cooldownPeriod.TotalSeconds}s后重试)");
                }
                else if (_state == CircuitState.Closed && _consecutiveFailures >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;
                    Rlog.Debug($"MOM熔断器: Closed → Open ({_failureThreshold}次连续失败，{_cooldownPeriod.TotalSeconds}s后探测)");
                }
            }
        }
    }
}
