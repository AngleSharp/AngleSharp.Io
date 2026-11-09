namespace AngleSharp.Io;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Runs the writes of one key (e.g., an origin) one after another, such that a
/// handler never sees two writes for the same key at the same time. Writes for
/// different keys still run in parallel.
/// </summary>
internal sealed class WriteQueue
{
    // the last write queued per key; a key is removed again once its last write is done
    private readonly ConcurrentDictionary<String, Task> _tails = new ConcurrentDictionary<String, Task>(StringComparer.Ordinal);

    /// <summary>
    /// Runs the given write once all writes queued before for the same key are done.
    /// </summary>
    /// <param name="key">The key the write belongs to.</param>
    /// <param name="write">The write to run.</param>
    /// <returns>The task of the write, carrying its result or failure.</returns>
    public async Task RunAsync(String key, Func<Task> write)
    {
        key = key ?? String.Empty;
        var done = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = Enqueue(key, done.Task);

        try
        {
            // the tails always complete successfully, so a failed write does not stop the next one
            await previous.ConfigureAwait(false);
            await write.Invoke().ConfigureAwait(false);
        }
        finally
        {
            done.SetResult(true);

            // removes the key only if no other write was queued after this one
            ((ICollection<KeyValuePair<String, Task>>)_tails).Remove(new KeyValuePair<String, Task>(key, done.Task));
        }
    }

    private Task Enqueue(String key, Task tail)
    {
        while (true)
        {
            if (_tails.TryGetValue(key, out var previous))
            {
                if (_tails.TryUpdate(key, tail, previous))
                {
                    return previous;
                }
            }
            else if (_tails.TryAdd(key, tail))
            {
                return Task.CompletedTask;
            }
        }
    }
}
