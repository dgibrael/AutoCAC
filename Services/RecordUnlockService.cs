using System.Collections.Concurrent;
using AutoCAC.Models;
namespace AutoCAC.Services;

public interface IRecordUnlockService
{
    IDisposable Subscribe(long recordId, Func<UnlockMessage, Task> onMessage);

    // Requester calls this. Returns:
    // - true  => confirmed/auto-confirmed (unlock)
    // - false => denied
    Task<bool> RequestUnlockAsync(long recordId, AuthUser requestedBy);

    // Lock-holder calls immediately when it receives the message (proves it's alive)
    void Ack(long recordId);

    // Lock-holder calls when user clicks confirm/deny
    void Respond(long recordId, bool unlockConfirmed);
}

public sealed class UnlockMessage
{
    public long RecordId { get; init; }
    public AuthUser RequestedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class RecordUnlockService : IRecordUnlockService
{
    // Hardcoded timings
    private static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(10);

    private sealed class Waiters
    {
        public TaskCompletionSource<bool> AckTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DecisionTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // one subscriber per recordId (simple + matches "who locked it doesn't matter")
    private readonly ConcurrentDictionary<long, Func<UnlockMessage, Task>> _subs = new();

    // current pending request per recordId
    private readonly ConcurrentDictionary<long, Waiters> _waiters = new();

    public IDisposable Subscribe(long recordId, Func<UnlockMessage, Task> onMessage)
    {
        if (recordId <= 0) throw new ArgumentOutOfRangeException(nameof(recordId));
        if (onMessage == null) throw new ArgumentNullException(nameof(onMessage));

        _subs[recordId] = onMessage;

        return new Unsubscriber(() =>
        {
            if (_subs.TryGetValue(recordId, out var existing) && existing == onMessage)
                _subs.TryRemove(recordId, out _);
        });
    }

    public async Task<bool> RequestUnlockAsync(long recordId, AuthUser requestedBy)
    {
        if (recordId <= 0) throw new ArgumentOutOfRangeException(nameof(recordId));

        // Nobody listening => unlock immediately
        if (!_subs.TryGetValue(recordId, out var handler))
            return true;

        // Replace any existing pending request for this record (simple rule: latest wins)
        var w = new Waiters();
        _waiters[recordId] = w;

        // Fire message to lock-holder (don’t block requester)
        _ = SafeInvoke(handler, new UnlockMessage
        {
            RecordId = recordId,
            RequestedBy = requestedBy,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // 1) wait for immediate ack; if no ack, treat as dead/stale => unlock now
        var ackWinner = await Task.WhenAny(w.AckTcs.Task, Task.Delay(AckTimeout)).ConfigureAwait(false);
        if (ackWinner != w.AckTcs.Task)
        {
            Cleanup(recordId, w);
            return true;
        }

        // 2) acked => wait for decision; if no decision in 10s => auto-confirm
        var decisionWinner = await Task.WhenAny(w.DecisionTcs.Task, Task.Delay(DecisionTimeout)).ConfigureAwait(false);

        bool unlockConfirmed = decisionWinner == w.DecisionTcs.Task
            ? w.DecisionTcs.Task.Result
            : true;

        Cleanup(recordId, w);
        return unlockConfirmed;
    }

    public void Ack(long recordId)
    {
        if (_waiters.TryGetValue(recordId, out var w))
            w.AckTcs.TrySetResult(true);
    }

    public void Respond(long recordId, bool unlockConfirmed)
    {
        if (_waiters.TryGetValue(recordId, out var w))
            w.DecisionTcs.TrySetResult(unlockConfirmed);
    }

    private void Cleanup(long recordId, Waiters w)
    {
        if (_waiters.TryGetValue(recordId, out var current) && ReferenceEquals(current, w))
            _waiters.TryRemove(recordId, out _);
    }

    private static async Task SafeInvoke(Func<UnlockMessage, Task> handler, UnlockMessage msg)
    {
        try { await handler(msg).ConfigureAwait(false); }
        catch { }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _dispose;
        private int _disposed;

        public Unsubscriber(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _dispose();
        }
    }
}
