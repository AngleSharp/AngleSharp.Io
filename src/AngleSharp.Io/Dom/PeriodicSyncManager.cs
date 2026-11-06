namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Io.BackgroundSyncApi;
using AngleSharp.Io.PermissionsApi;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the PeriodicSyncManager interface, which registers tasks that
/// repeatedly run in the service worker when the <see cref="ISyncPlatform"/>
/// says so.
/// </summary>
[DomName("PeriodicSyncManager")]
public sealed class PeriodicSyncManager
{
    private readonly SyncTaskList _tasks;

    internal PeriodicSyncManager(ServiceWorkerRegistration registration, ISyncPlatform platform)
    {
        _tasks = new SyncTaskList(registration, platform, SyncTaskKind.Periodic, PermissionNames.PeriodicBackgroundSync);
    }

    /// <summary>
    /// Registers a task. Registering a known tag again does not add a second
    /// task, but applies a different minimum interval.
    /// </summary>
    /// <param name="tag">The tag of the task.</param>
    /// <param name="options">The optional options of the task.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("register")]
    public async Task Register(String tag, BackgroundSyncOptions options = null)
    {
        var minInterval = options?.MinInterval ?? 0;

        // the spec's [EnforceRange] rejects these with a TypeError
        if (Double.IsNaN(minInterval) || minInterval < 0)
        {
            throw new ArgumentException("The minimum interval must not be negative.", nameof(options));
        }

        await _tasks.RegisterAsync(tag, (UInt64)minInterval).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the tags of the registered tasks.
    /// </summary>
    /// <returns>The tags.</returns>
    [DomName("getTags")]
    public Task<String[]> GetTags() => Task.FromResult(_tasks.GetTags());

    /// <summary>
    /// Removes the task with the given tag, if any.
    /// </summary>
    /// <param name="tag">The tag of the task.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("unregister")]
    public Task Unregister(String tag) => _tasks.UnregisterAsync(tag);

    internal Task TerminateAsync() => _tasks.TerminateAsync();
}

/// <summary>
/// Represents the options for registering a periodic task.
/// </summary>
[DomName("BackgroundSyncOptions")]
public sealed class BackgroundSyncOptions
{
    /// <summary>
    /// Gets or sets the minimum time between two firings in milliseconds.
    /// </summary>
    [DomName("minInterval")]
    public Double MinInterval { get; set; }
}
