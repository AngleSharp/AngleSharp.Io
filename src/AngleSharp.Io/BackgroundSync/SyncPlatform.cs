namespace AngleSharp.Io.BackgroundSyncApi;

using AngleSharp.Io.Dom;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a sync platform delegating to optional callbacks. Without
/// callbacks, tasks only fire when <see cref="SyncTask.FireAsync"/> is
/// called, and firing them succeeds without anything being run.
/// </summary>
public sealed class SyncPlatform : ISyncPlatform
{
    /// <summary>
    /// Gets or sets the callback invoked for planning a task.
    /// </summary>
    public Func<SyncTask, Task> Schedule { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked for forgetting a task.
    /// </summary>
    public Func<SyncTask, Task> Cancel { get; set; }

    /// <summary>
    /// Gets or sets the callback running a task in the given worker.
    /// </summary>
    public Func<ServiceWorker, SyncTask, Task> Dispatch { get; set; }

    /// <inheritdoc />
    public Task ScheduleAsync(SyncTask task) => Schedule?.Invoke(task) ?? Task.CompletedTask;

    /// <inheritdoc />
    public Task CancelAsync(SyncTask task) => Cancel?.Invoke(task) ?? Task.CompletedTask;

    /// <inheritdoc />
    public Task DispatchAsync(ServiceWorker worker, SyncTask task) => Dispatch?.Invoke(worker, task) ?? Task.CompletedTask;
}
