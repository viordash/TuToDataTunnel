using System.Collections.Concurrent;

namespace TuToProxy.Core.Pipeline;

public class ResponseTracker {
    readonly ConcurrentDictionary<long, TaskCompletionSource<int>> pending = new();
    long totalTransmitted;
    long totalReceived;

    public long TotalTransmitted => Interlocked.Read(ref totalTransmitted);
    public long TotalReceived => Interlocked.Read(ref totalReceived);

    public void RecordReceived(int bytes) {
        Interlocked.Add(ref totalReceived, bytes);
    }

    public void RecordTransmitted(int bytes) {
        Interlocked.Add(ref totalTransmitted, bytes);
    }

    public Task<int> TrackRequest(long sequenceNumber) {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[sequenceNumber] = tcs;
        return tcs.Task;
    }

    public bool Complete(long sequenceNumber, int result) {
        if(pending.TryRemove(sequenceNumber, out var tcs)) {
            Interlocked.Add(ref totalTransmitted, result);
            tcs.SetResult(result);
            return true;
        }
        return false;
    }

    public bool Fail(long sequenceNumber, Exception ex) {
        if(pending.TryRemove(sequenceNumber, out var tcs)) {
            tcs.SetException(ex);
            return true;
        }
        return false;
    }

    public void CancelAll() {
        foreach(var kvp in pending) {
            if(pending.TryRemove(kvp.Key, out var tcs)) {
                tcs.TrySetCanceled();
            }
        }
    }

    public int PendingCount => pending.Count;
}
