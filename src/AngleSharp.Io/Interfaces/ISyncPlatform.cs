namespace AngleSharp.Io.Dom;

using System.Threading.Tasks;

/// <summary>
/// Represents the platform deciding when the tasks of background sync and
/// periodic background sync run, and running them in their service worker.
/// </summary>
public interface ISyncPlatform
{
    /// <summary>
    /// Plans the given task, which is pending until the platform calls
    /// <see cref="SyncTask.FireAsync"/>, e.g., when the host is online or
    /// after the task's minimum interval. It is called once per firing: a
    /// periodic task or a task to retry is scheduled again after firing.
    /// </summary>
    /// <param name="task">The task to plan.</param>
    /// <returns>The task representing the operation.</returns>
    Task ScheduleAsync(SyncTask task);

    /// <summary>
    /// Forgets the given task, which has been removed before firing again.
    /// </summary>
    /// <param name="task">The task to forget.</param>
    /// <returns>The task representing the operation.</returns>
    Task CancelAsync(SyncTask task);

    /// <summary>
    /// Runs the sync or periodicsync event of the given worker. A faulted
    /// task marks the attempt as failed.
    /// </summary>
    /// <param name="worker">The active worker of the task's registration.</param>
    /// <param name="task">The task to run.</param>
    /// <returns>The task representing the attempt.</returns>
    Task DispatchAsync(ServiceWorker worker, SyncTask task);
}
