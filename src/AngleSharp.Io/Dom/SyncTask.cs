namespace AngleSharp.Io.Dom;

using AngleSharp.Io.BackgroundSyncApi;
using System;
using System.Threading.Tasks;

/// <summary>
/// The kinds of sync tasks.
/// </summary>
public enum SyncTaskKind
{
    /// <summary>
    /// The task is done after it fired successfully or ran out of attempts.
    /// </summary>
    OneOff,
    /// <summary>
    /// The task fires repeatedly until it is unregistered.
    /// </summary>
    Periodic,
}

/// <summary>
/// Represents a task registered via SyncManager or PeriodicSyncManager, as
/// seen by the <see cref="ISyncPlatform"/>.
/// </summary>
public sealed class SyncTask
{
    private readonly SyncTaskList _list;

    internal SyncTask(SyncTaskList list, String tag, UInt64 minInterval)
    {
        _list = list;
        Tag = tag;
        MinInterval = minInterval;
    }

    /// <summary>
    /// Gets the tag of the task.
    /// </summary>
    public String Tag { get; }

    /// <summary>
    /// Gets the kind of the task.
    /// </summary>
    public SyncTaskKind Kind => _list.Kind;

    /// <summary>
    /// Gets the registration the task belongs to.
    /// </summary>
    public ServiceWorkerRegistration Registration => _list.Registration;

    /// <summary>
    /// Gets the minimum time between two firings in milliseconds, which is
    /// 0 for one-off tasks.
    /// </summary>
    public UInt64 MinInterval { get; internal set; }

    /// <summary>
    /// Gets if a failure of the current attempt removes the task, which
    /// corresponds to the lastChance attribute of the sync event.
    /// </summary>
    public Boolean LastChance { get; internal set; }

    internal Int32 FailedAttempts { get; set; }

    internal SyncTaskState State { get; set; }

    /// <summary>
    /// Fires the task, i.e., lets the platform dispatch it to the active
    /// worker. To be called by the platform. A task that failed with attempts
    /// left, or a periodic task, is scheduled again.
    /// </summary>
    /// <returns>
    /// True if the event was handled successfully, otherwise false, which
    /// includes that the task is not pending, e.g., as it has been removed.
    /// </returns>
    public Task<Boolean> FireAsync() => _list.FireAsync(this);
}

enum SyncTaskState
{
    Pending,
    Firing,
    ReregisteredWhileFiring,
    Removed,
}
