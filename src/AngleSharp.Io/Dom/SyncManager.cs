namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Io.BackgroundSyncApi;
using AngleSharp.Io.PermissionsApi;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the SyncManager interface, which registers one-off tasks that
/// run in the service worker when the <see cref="ISyncPlatform"/> says so.
/// </summary>
[DomName("SyncManager")]
public sealed class SyncManager
{
    private readonly SyncTaskList _tasks;

    internal SyncManager(ServiceWorkerRegistration registration, ISyncPlatform platform)
    {
        _tasks = new SyncTaskList(registration, platform, SyncTaskKind.OneOff, PermissionNames.BackgroundSync);
    }

    /// <summary>
    /// Registers a task. Registering a known tag again does not add a second
    /// task; if the task is firing it fires once more afterwards.
    /// </summary>
    /// <param name="tag">The tag of the task.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("register")]
    public Task Register(String tag) => _tasks.RegisterAsync(tag, 0);

    /// <summary>
    /// Gets the tags of the tasks that are not done yet.
    /// </summary>
    /// <returns>The tags.</returns>
    [DomName("getTags")]
    public Task<String[]> GetTags() => Task.FromResult(_tasks.GetTags());

    /// <summary>
    /// Removes the task with the given tag, if any. Unlike on the periodic
    /// manager, this is not part of the specification and hence only
    /// available to the host.
    /// </summary>
    /// <param name="tag">The tag of the task.</param>
    /// <returns>The task representing the operation.</returns>
    public Task Unregister(String tag) => _tasks.UnregisterAsync(tag);

    internal Task TerminateAsync() => _tasks.TerminateAsync();
}
