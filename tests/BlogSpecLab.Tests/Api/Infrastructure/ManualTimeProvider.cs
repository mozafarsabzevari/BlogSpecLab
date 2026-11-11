namespace BlogSpecLab.Tests.Api.Infrastructure;

/// <summary>
/// A clock that only moves when a test moves it. Timers created through it, such as cancellation deadlines,
/// fire when the clock passes their due time.
/// </summary>
public sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _utcNow = utcNow.ToUniversalTime();

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _utcNow;
        }
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (_lock)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        FireDueTimers();
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
        {
            _utcNow += delta;
        }

        FireDueTimers();
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);

        lock (_lock)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    private void FireDueTimers()
    {
        while (true)
        {
            ManualTimer? due;

            lock (_lock)
            {
                due = _timers.FirstOrDefault(timer => timer.DueAt is { } at && at <= _utcNow);
                due?.Advance();
            }

            if (due is null)
            {
                return;
            }

            due.Invoke();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._lock)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._utcNow + dueTime;
                _period = period;
            }

            return true;
        }

        /// <summary>Called under the owner's lock.</summary>
        public void Advance() =>
            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : DueAt + _period;

        public void Invoke() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
